#!/usr/bin/env bash
set -euo pipefail

API="http://localhost:5000"
KEY="sk_test_smoke_merchant"
MID="11111111-1111-1111-1111-111111111111"

echo "=== Creating payment ==="
RESULT=$(curl -s -X POST "$API/api/v1/payments" \
  -H "Content-Type: application/json" \
  -H "X-API-Key: $KEY" \
  -d "{
    \"merchantId\": \"$MID\",
    \"amount\": 50.00,
    \"currency\": \"BRL\",
    \"method\": \"pix\",
    \"customer\": {
      \"email\": \"manual-e2e@test.com\",
      \"name\": \"Manual E2E\",
      \"document\": \"12345678901\"
    }
  }")

echo "Response: $RESULT"
PID=$(echo "$RESULT" | python3 -c 'import sys,json; print(json.load(sys.stdin)["paymentId"])')
echo "PaymentId: $PID"

echo ""
echo "=== Polling status (max 60s) ==="
for i in $(seq 1 12); do
    sleep 5
    DATA=$(curl -s "$API/api/v1/payments/$PID" -H "X-API-Key: $KEY")
    STATUS=$(echo "$DATA" | python3 -c 'import sys,json; d=json.load(sys.stdin); print("status=%s fraud=%s decision=%s" % (d.get("status"), d.get("fraudScore"), d.get("fraudDecision")))')
    echo "  [$i] $STATUS"

    if echo "$STATUS" | grep -qE "approved|rejected|captured|refunded"; then
        echo ""
        echo "=== Payment processed! ==="
        echo "$DATA" | python3 -m json.tool
        exit 0
    fi
done

echo ""
echo "=== TIMEOUT - payment still pending after 60s ==="
echo "Check logs: /tmp/pg-logs/"
exit 1
