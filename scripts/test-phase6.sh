#!/bin/bash
# ============================================
# Phase 6: Run Load & Resilience Tests
# ============================================
# Run on: VM3 (192.168.1.63)
# Time: 2-4 hours (Days 3-4)
# Purpose: Validate system performance and resilience

set -e

echo "================================"
echo "Phase 6: Running Load Tests"
echo "================================"
echo "Target: VM3 (192.168.1.63)"
echo "Load Profile: 1 VU → 10 VU → 100 VU"
echo ""

# Check if k6 is installed
if ! command -v k6 &> /dev/null; then
    echo "❌ k6 not installed!"
    echo ""
    echo "Install k6:"
    echo "  sudo apt install -y k6"
    echo ""
    exit 1
fi

echo "k6 version: $(k6 --version)"
echo ""

# Verify load test script exists
if [ ! -f "load-test.js" ]; then
    echo "❌ load-test.js not found!"
    echo "Run from: ~/applications/PaymentGateway"
    exit 1
fi

# Get API endpoint
API_ENDPOINT="${1:-http://192.168.1.61:5000}"

echo "API Endpoint: $API_ENDPOINT"
echo ""

# Verify API is reachable
echo "Verifying API connectivity..."
if ! curl -s "$API_ENDPOINT/health" > /dev/null; then
    echo "❌ Cannot reach API at $API_ENDPOINT"
    echo ""
    echo "Troubleshooting:"
    echo "  1. Check API is running: curl http://192.168.1.61:5000/health"
    echo "  2. Verify network: ping 192.168.1.61"
    echo "  3. Check firewall: sudo ufw status"
    exit 1
fi

echo "✓ API is reachable"
echo ""

# Phase 6.1: Baseline Test (1 VU)
echo "============================================"
echo "Phase 6.1: Baseline Test (1 Virtual User)"
echo "============================================"
echo "Duration: 30 seconds"
echo "Expected: p99 <200ms, RPS >100, error rate <1%"
echo ""
read -p "Press ENTER to start baseline test..."

k6 run \
    --vus 1 \
    --duration 30s \
    --summary-export=results-baseline.json \
    load-test.js

echo ""
echo "✓ Baseline test complete"
echo "Results: results-baseline.json"
echo ""

# Phase 6.2: Light Load (10 VU)
echo "============================================"
echo "Phase 6.2: Light Load Test (10 Virtual Users)"
echo "============================================"
echo "Duration: 60 seconds"
echo "Expected: RPS >1000, p95 <200ms, error rate <1%"
echo ""
read -p "Press ENTER to start light load test..."

k6 run \
    --vus 10 \
    --duration 60s \
    --summary-export=results-light.json \
    load-test.js

echo ""
echo "✓ Light load test complete"
echo "Results: results-light.json"
echo ""

# Phase 6.3: Medium Load (100 VU)
echo "============================================"
echo "Phase 6.3: Medium Load Test (100 Virtual Users)"
echo "============================================"
echo "Duration: 120 seconds"
echo "Expected: RPS >5000, error rate <2%"
echo ""
read -p "Press ENTER to start medium load test..."

k6 run \
    --vus 100 \
    --duration 120s \
    --summary-export=results-medium.json \
    load-test.js

echo ""
echo "✓ Medium load test complete"
echo "Results: results-medium.json"
echo ""

# Summary
echo "============================================"
echo "✅ LOAD TESTS COMPLETE"
echo "============================================"
echo ""
echo "Results saved:"
echo "  • Baseline: results-baseline.json"
echo "  • Light load: results-light.json"
echo "  • Medium load: results-medium.json"
echo ""
echo "Review results:"
echo "  k6 show results-baseline.json"
echo "  k6 show results-light.json"
echo "  k6 show results-medium.json"
echo ""
echo "Next steps:"
echo "1. Check latency metrics:"
echo "   - p99 baseline: should be <200ms"
echo "   - p95 light: should be <200ms"
echo "   - p95 medium: should be <500ms"
echo ""
echo "2. Check throughput:"
echo "   - Baseline RPS: should be >100"
echo "   - Light RPS: should be >1000"
echo "   - Medium RPS: should be >5000"
echo ""
echo "3. Check error rates:"
echo "   - All should be <2%"
echo ""
echo "4. Save results for report:"
echo "   mkdir -p /tmp/phase6-results-$(date +%Y%m%d)"
echo "   cp results-*.json /tmp/phase6-results-$(date +%Y%m%d)/"
echo ""
