#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOG_ROOT="$ROOT_DIR/.tmp/smoke"
mkdir -p "$LOG_ROOT"

TIMESTAMP="$(date +"%Y%m%d-%H%M%S")"
API_LOG="$LOG_ROOT/api-$TIMESTAMP.log"

API_PORT="${API_PORT:-5000}"
API_URL="${API_URL:-http://127.0.0.1:${API_PORT}}"
KEEP_API_RUNNING="${KEEP_API_RUNNING:-false}"
REUSE_API="${REUSE_API:-false}"
DDS_STRICT="${DDS_STRICT:-true}"
STOP_STARTED_CONTAINERS="${STOP_STARTED_CONTAINERS:-false}"

PG_CONTAINER="${PG_CONTAINER:-pgw-postgres}"
MONGO_CONTAINER="${MONGO_CONTAINER:-pgw-mongo}"
REDIS_CONTAINER="${REDIS_CONTAINER:-pgw-redis}"

PG_PORT="${PG_PORT:-55432}"
MONGO_PORT="${MONGO_PORT:-27018}"
REDIS_PORT="${REDIS_PORT:-6380}"

PG_DB="${PG_DB:-demo-gateway}"
PG_USER="${PG_USER:-app}"
PG_PASSWORD="${PG_PASSWORD:-Admin@123}"

MONGO_USER="${MONGO_USER:-admin}"
MONGO_PASSWORD="${MONGO_PASSWORD:-Admin@123}"
MONGO_DB="${MONGO_DB:-payment-gateway}"

REDIS_PASSWORD="${REDIS_PASSWORD:-Admin@123}"

MERCHANT_ID="${MERCHANT_ID:-11111111-1111-1111-1111-111111111111}"
MERCHANT_API_KEY="${MERCHANT_API_KEY:-sk_test_smoke_merchant}"
OTHER_MERCHANT_ID="${OTHER_MERCHANT_ID:-22222222-2222-2222-2222-222222222222}"
OTHER_MERCHANT_API_KEY="${OTHER_MERCHANT_API_KEY:-sk_test_smoke_other}"

API_PID=""
API_STARTED="false"
STARTED_CONTAINERS=()

log() {
    echo "[smoke] $*"
}

fail() {
    echo "[smoke] ERROR: $*" >&2
    exit 1
}

usage() {
    cat <<'EOF'
Usage: scripts/smoke-e2e-wsl.sh [options]

Options:
  --api-port <port>             API port (default: 5000)
  --api-url <url>               API base URL (default: http://127.0.0.1:<port>)
  --reuse-api                   Reuse an existing API process if /health is already reachable
  --keep-api-running            Do not stop API process started by this script
  --no-dds-strict               Do not fail if DDS fallback appears in API logs
  --stop-started-containers     Stop only containers started by this script on exit
  --help                        Show this help

Environment overrides are supported for container names/ports, credentials and merchant seed IDs.
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --api-port)
            shift
            API_PORT="${1:-}"
            [[ -n "$API_PORT" ]] || fail "--api-port requires a value"
            API_URL="http://127.0.0.1:${API_PORT}"
            ;;
        --api-url)
            shift
            API_URL="${1:-}"
            [[ -n "$API_URL" ]] || fail "--api-url requires a value"
            ;;
        --reuse-api)
            REUSE_API="true"
            ;;
        --keep-api-running)
            KEEP_API_RUNNING="true"
            ;;
        --no-dds-strict)
            DDS_STRICT="false"
            ;;
        --stop-started-containers)
            STOP_STARTED_CONTAINERS="true"
            ;;
        --help|-h)
            usage
            exit 0
            ;;
        *)
            fail "Unknown option: $1"
            ;;
    esac
    shift
done

require_cmd() {
    command -v "$1" >/dev/null 2>&1 || fail "Command not found: $1"
}

url_encode() {
    local raw="$1"
    python3 - "$raw" <<'PY'
import sys
import urllib.parse

print(urllib.parse.quote(sys.argv[1], safe=""))
PY
}

cleanup() {
    local exit_code=$?

    if [[ "$API_STARTED" == "true" && "$KEEP_API_RUNNING" != "true" ]]; then
        if [[ -n "$API_PID" ]] && kill -0 "$API_PID" >/dev/null 2>&1; then
            log "Stopping API process (PID $API_PID)"
            kill "$API_PID" >/dev/null 2>&1 || true
            wait "$API_PID" 2>/dev/null || true
        fi
    fi

    if [[ "$STOP_STARTED_CONTAINERS" == "true" ]]; then
        for container in "${STARTED_CONTAINERS[@]}"; do
            log "Stopping container $container"
            docker stop "$container" >/dev/null 2>&1 || true
        done
    fi

    if [[ $exit_code -ne 0 ]]; then
        log "API log: $API_LOG"
        if [[ -f "$API_LOG" ]]; then
            tail -n 80 "$API_LOG" || true
        fi
    fi

    exit "$exit_code"
}

trap cleanup EXIT INT TERM

ensure_container_running() {
    local name="$1"
    shift

    if docker ps --format '{{.Names}}' | grep -qx "$name"; then
        log "Container $name is already running"
        return
    fi

    if docker ps -aq -f "name=^${name}$" | grep -q .; then
        log "Starting existing container $name"
        docker start "$name" >/dev/null
        STARTED_CONTAINERS+=("$name")
        return
    fi

    log "Creating container $name"
    docker run -d --name "$name" "$@" >/dev/null
    STARTED_CONTAINERS+=("$name")
}

wait_for_postgres() {
    local timeout=90
    local elapsed=0

    while [[ $elapsed -lt $timeout ]]; do
        if docker exec "$PG_CONTAINER" pg_isready -U "$PG_USER" -d "$PG_DB" >/dev/null 2>&1; then
            return
        fi
        sleep 1
        elapsed=$((elapsed + 1))
    done

    fail "Postgres was not ready after ${timeout}s"
}

wait_for_mongo() {
    local timeout=90
    local elapsed=0

    while [[ $elapsed -lt $timeout ]]; do
        if docker exec "$MONGO_CONTAINER" mongosh --quiet \
            --username "$MONGO_USER" \
            --password "$MONGO_PASSWORD" \
            --authenticationDatabase admin \
            --eval 'db.adminCommand({ ping: 1 }).ok' | grep -q "1"; then
            return
        fi
        sleep 1
        elapsed=$((elapsed + 1))
    done

    fail "MongoDB was not ready after ${timeout}s"
}

wait_for_redis() {
    local timeout=90
    local elapsed=0

    while [[ $elapsed -lt $timeout ]]; do
        if docker exec "$REDIS_CONTAINER" redis-cli -a "$REDIS_PASSWORD" ping 2>/dev/null | grep -q "^PONG$"; then
            return
        fi
        sleep 1
        elapsed=$((elapsed + 1))
    done

    fail "Redis was not ready after ${timeout}s"
}

ensure_postgres_database() {
    log "Ensuring PostgreSQL database exists"
    docker exec -i "$PG_CONTAINER" psql -v ON_ERROR_STOP=1 -U "$PG_USER" -d postgres <<SQL
SELECT 'CREATE DATABASE "${PG_DB}"'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = '${PG_DB}')
\gexec
SQL
}

is_api_reachable() {
    curl -fsS "${API_URL}/health" >/dev/null 2>&1
}

wait_for_api() {
    local timeout=120
    local elapsed=0

    while [[ $elapsed -lt $timeout ]]; do
        if is_api_reachable; then
            return
        fi

        if [[ "$API_STARTED" == "true" ]] && [[ -n "$API_PID" ]] && ! kill -0 "$API_PID" >/dev/null 2>&1; then
            fail "API process exited unexpectedly. Check $API_LOG"
        fi

        sleep 1
        elapsed=$((elapsed + 1))
    done

    fail "API was not reachable after ${timeout}s at ${API_URL}"
}

start_api() {
    local mongo_user_encoded
    local mongo_password_encoded

    mongo_user_encoded="$(url_encode "$MONGO_USER")"
    mongo_password_encoded="$(url_encode "$MONGO_PASSWORD")"

    if is_api_reachable; then
        if [[ "$REUSE_API" == "true" ]]; then
            log "Reusing existing API at ${API_URL}"
            return
        fi
        fail "API already reachable at ${API_URL}. Use --reuse-api to keep it."
    fi

    log "Starting PaymentGateway API (log: $API_LOG)"
    (
        cd "$ROOT_DIR"
        ASPNETCORE_URLS="http://0.0.0.0:${API_PORT}" \
        ASPNETCORE_ENVIRONMENT="Development" \
        ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=${PG_PORT};Database=${PG_DB};Username=${PG_USER};Password=${PG_PASSWORD}" \
        ConnectionStrings__MongoDb="mongodb://${mongo_user_encoded}:${mongo_password_encoded}@127.0.0.1:${MONGO_PORT}/${MONGO_DB}?authSource=admin" \
        ConnectionStrings__Redis="127.0.0.1:${REDIS_PORT},password=${REDIS_PASSWORD}" \
        Redis__ConnectionString="127.0.0.1:${REDIS_PORT},password=${REDIS_PASSWORD}" \
        Dds__UseRealDds="true" \
        dotnet run --no-launch-profile --project src/PaymentGateway.Api/PaymentGateway.Api.csproj
    ) >"$API_LOG" 2>&1 &

    API_PID=$!
    API_STARTED="true"
}

run_http_smoke() {
    log "Running HTTP smoke checks (merchant scope + core endpoints)"

    API_URL="$API_URL" \
    MERCHANT_ID="$MERCHANT_ID" \
    MERCHANT_API_KEY="$MERCHANT_API_KEY" \
    OTHER_MERCHANT_ID="$OTHER_MERCHANT_ID" \
    python3 - <<'PY'
import json
import os
import sys
import uuid
import urllib.error
import urllib.request

base_url = os.environ["API_URL"].rstrip("/")
merchant_id = os.environ["MERCHANT_ID"]
merchant_api_key = os.environ["MERCHANT_API_KEY"]
other_merchant_id = os.environ["OTHER_MERCHANT_ID"]

def request(method, path, expected_status, body=None, headers=None):
    url = f"{base_url}{path}"
    payload = None
    req_headers = dict(headers or {})
    if body is not None:
        payload = json.dumps(body).encode("utf-8")
        req_headers["Content-Type"] = "application/json"

    req = urllib.request.Request(url, data=payload, headers=req_headers, method=method)

    try:
        with urllib.request.urlopen(req, timeout=30) as response:
            status = response.status
            raw_body = response.read().decode("utf-8")
    except urllib.error.HTTPError as exc:
        status = exc.code
        raw_body = exc.read().decode("utf-8")
    except Exception as exc:
        raise RuntimeError(f"{method} {path} failed: {exc}") from exc

    if status != expected_status:
        raise AssertionError(
            f"{method} {path}: expected {expected_status}, got {status}. Body: {raw_body}"
        )

    parsed = None
    if raw_body:
        try:
            parsed = json.loads(raw_body)
        except json.JSONDecodeError:
            parsed = raw_body

    print(f"[smoke] OK {method} {path} -> {status}")
    return parsed

auth_headers = {
    "X-API-Key": merchant_api_key
}

request("GET", "/health", 200)
request("GET", "/api/v1/payments", 401)
request("GET", "/api/v1/payments", 401, headers={"Authorization": "Bearer invalid-token"})
request("GET", f"/api/v1/merchants/{merchant_id}", 200, headers=auth_headers)
request("GET", f"/api/v1/merchants/{other_merchant_id}", 403, headers=auth_headers)

request("GET", f"/api/v1/payments?merchantId={merchant_id}", 200, headers=auth_headers)
request("GET", f"/api/v1/payments?merchantId={other_merchant_id}", 403, headers=auth_headers)

payment_body = {
    "merchantId": merchant_id,
    "amount": 159.99,
    "currency": "BRL",
    "method": "credit_card",
    "customer": {
        "email": "smoke-customer@example.test",
        "name": "Smoke Customer",
        "document": "12345678901",
        "ip": "127.0.0.1",
        "phone": "+5511999999999"
    },
    "description": "Smoke E2E payment",
    "metadata": {
        "suite": "smoke-e2e-wsl"
    }
}

payment_headers = dict(auth_headers)
payment_headers["Idempotency-Key"] = str(uuid.uuid4())
create_payment_response = request("POST", "/api/v1/payments", 202, body=payment_body, headers=payment_headers)

if not isinstance(create_payment_response, dict):
    raise AssertionError("Create payment response is not a JSON object")

payment_id = create_payment_response.get("paymentId")
if not payment_id:
    raise AssertionError(f"Missing paymentId in create payment response: {create_payment_response}")

request("GET", f"/api/v1/payments/{payment_id}", 200, headers=auth_headers)
payments = request("GET", f"/api/v1/payments?merchantId={merchant_id}", 200, headers=auth_headers)

items = payments.get("items") if isinstance(payments, dict) else None
if not isinstance(items, list):
    raise AssertionError(f"Unexpected payments payload shape: {payments}")
if not any(item.get("id") == payment_id for item in items if isinstance(item, dict)):
    raise AssertionError(f"Created payment {payment_id} not found in list response")

request(
    "POST",
    "/api/v1/payments",
    403,
    body={**payment_body, "merchantId": other_merchant_id},
    headers={**auth_headers, "Idempotency-Key": str(uuid.uuid4())},
)

request("GET", f"/api/v1/statistics?merchantId={merchant_id}", 200, headers=auth_headers)
request("GET", f"/api/v1/statistics?merchantId={other_merchant_id}", 403, headers=auth_headers)

create_webhook_response = request(
    "PUT",
    "/api/v1/webhooks",
    201,
    body={
        "merchantId": merchant_id,
        "url": "https://webhook-smoke.example.test/payment",
        "events": ["payment.created", "payment.approved"],
        "secret": "smoke-webhook-secret",
        "active": True
    },
    headers=auth_headers,
)

if not isinstance(create_webhook_response, dict):
    raise AssertionError("Create webhook response is not a JSON object")

webhook_id = create_webhook_response.get("webhookId")
if not webhook_id:
    raise AssertionError(f"Missing webhookId in create webhook response: {create_webhook_response}")

webhooks = request("GET", f"/api/v1/webhooks?merchantId={merchant_id}", 200, headers=auth_headers)
if not isinstance(webhooks, list):
    raise AssertionError(f"Unexpected webhooks payload shape: {webhooks}")
if not any(isinstance(w, dict) and w.get("webhookId") == webhook_id for w in webhooks):
    raise AssertionError(f"Created webhook {webhook_id} not found in list response")

request("GET", f"/api/v1/webhooks?merchantId={other_merchant_id}", 403, headers=auth_headers)
request("DELETE", f"/api/v1/webhooks/{webhook_id}", 204, headers=auth_headers)

print("[smoke] All HTTP checks passed.")
PY
}

check_dds_mode() {
    if [[ "$DDS_STRICT" != "true" ]]; then
        return
    fi

    if [[ ! -f "$API_LOG" ]]; then
        log "DDS strict check skipped: API log is not available (likely reused API process)."
        return
    fi

    # Strict mode: fail if fallback was used at startup or during publish.
    if grep -Eqi "fallback mode|Failed to publish to DDS topic|\\[DDS\\] Publishing to" "$API_LOG"; then
        fail "DDS fallback detected in API log. Strict mode requires real CycloneDDS."
    fi
}

main() {
    require_cmd docker
    require_cmd dotnet
    require_cmd curl
    require_cmd python3

    log "Root: $ROOT_DIR"
    log "API URL: $API_URL"

    ensure_container_running "$PG_CONTAINER" \
        -e POSTGRES_DB="$PG_DB" \
        -e POSTGRES_USER="$PG_USER" \
        -e POSTGRES_PASSWORD="$PG_PASSWORD" \
        -p "127.0.0.1:${PG_PORT}:5432" \
        postgres:16

    ensure_container_running "$MONGO_CONTAINER" \
        -e MONGO_INITDB_ROOT_USERNAME="$MONGO_USER" \
        -e MONGO_INITDB_ROOT_PASSWORD="$MONGO_PASSWORD" \
        -p "127.0.0.1:${MONGO_PORT}:27017" \
        mongo:7

    ensure_container_running "$REDIS_CONTAINER" \
        -p "127.0.0.1:${REDIS_PORT}:6379" \
        redis:7-alpine \
        redis-server --requirepass "$REDIS_PASSWORD"

    log "Waiting for infrastructure readiness"
    wait_for_postgres
    wait_for_mongo
    wait_for_redis

    ensure_postgres_database

    start_api
    wait_for_api

    run_http_smoke
    check_dds_mode

    log "SMOKE OK"
    if [[ -f "$API_LOG" ]]; then
        log "API log: $API_LOG"
    else
        log "API log: not generated by this run (reused API process)."
    fi

    if [[ "$API_STARTED" == "true" && "$KEEP_API_RUNNING" == "true" ]]; then
        log "API process left running (PID $API_PID)"
    fi
}

main "$@"
