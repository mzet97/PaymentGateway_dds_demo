#!/usr/bin/env bash
# Start ALL PaymentGateway services as background daemons (survive shell exit)
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BASE_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
LOG_DIR="/tmp/pg-logs"
mkdir -p "$LOG_DIR"

# ── Environment ──
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS='http://+:5000'
export ConnectionStrings__DefaultConnection='Host=spsql.home.arpa;Port=5432;Database=demo-gateway;User Id=app;Password=Admin@123;'
export ConnectionStrings__MongoDb='mongodb://admin:Admin%40123@mongodb.home.arpa:27017/?authSource=admin'
export ConnectionStrings__Redis='redis.home.arpa:30379,password=Admin@123,abortConnect=false,connectTimeout=5000,syncTimeout=3000'
export Redis__ConnectionString='redis.home.arpa:30379,password=Admin@123,abortConnect=false,connectTimeout=5000,syncTimeout=3000'
export Dds__UseRealDds=true
export Dds__EnableGracefulFallback=true
export LD_LIBRARY_PATH="/mnt/e/TI/git/tese/PaymentGateway_dds_demo/artifacts/native/linux-x64:${LD_LIBRARY_PATH:-}"
export CYCLONEDDS_URI="file:///mnt/e/TI/git/tese/PaymentGateway_dds_demo/configs/cyclonedds-local.xml"
export OpenRouter__ApiKey='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'
export OpenRouter__BaseUrl='https://openrouter.ai/api/v1'
export OpenRouter__Model='minimax/minimax-m2.5'
export Telemetry__EnableElasticsearchLogging=true
export Telemetry__ElasticsearchUrl='https://elasticsearch.home.arpa/'
export Telemetry__SkipTlsValidation=true
export Telemetry__AllowSelfSignedCertificates=true
export Telemetry__EnableOtlp=false
export Minio__Endpoint='minio-s3.home.arpa'
export Minio__AccessKey='admin'
export Minio__SecretKey='Admin@123'
export Minio__BucketName='demo-gateway'
export Minio__UseSsl=true
export WEBHOOK_SECRET='webhook-secret-demo-2026'
export DISABLE_RATE_LIMIT="${DISABLE_RATE_LIMIT:-false}"

# ── Helper: launch a dotnet service as daemon ──
launch_dotnet() {
    local name="$1"
    local dll_path="$2"
    local dir
    dir="$(dirname "$dll_path")"
    echo -n "  $name ... "
    (cd "$dir" && setsid dotnet "$(basename "$dll_path")" </dev/null &>"$LOG_DIR/$name.log" &)
    echo "started (log: $LOG_DIR/$name.log)"
}

# ── Kill previous instances ──
echo "Stopping old processes..."
pkill -f 'PaymentGateway\.' 2>/dev/null || true
pkill -f 'node dist/main' 2>/dev/null || true
pkill -f 'next start' 2>/dev/null || true
sleep 2

# ── .NET Services ──
echo "Starting .NET services..."

launch_dotnet "api" \
    "$BASE_DIR/src/PaymentGateway.Api/bin/Release/net8.0/PaymentGateway.Api.dll"

launch_dotnet "processor" \
    "$BASE_DIR/src/services/PaymentGateway.Services.PaymentProcessor/bin/Release/net8.0/PaymentGateway.Services.PaymentProcessor.dll"

launch_dotnet "fraud" \
    "$BASE_DIR/src/services/PaymentGateway.Services.FraudDetector/bin/Release/net8.0/PaymentGateway.Services.FraudDetector.dll"

launch_dotnet "sync" \
    "$BASE_DIR/src/services/PaymentGateway.Services.MongoSync/bin/Release/net8.0/PaymentGateway.Services.MongoSync.dll"

launch_dotnet "notification" \
    "$BASE_DIR/src/services/PaymentGateway.Services.Notification/bin/Release/net8.0/PaymentGateway.Services.Notification.dll"

launch_dotnet "settlement" \
    "$BASE_DIR/src/services/PaymentGateway.Services.Settlement/bin/Release/net8.0/PaymentGateway.Services.Settlement.dll"

launch_dotnet "history" \
    "$BASE_DIR/src/services/PaymentGateway.Services.TransactionHistory/bin/Release/net8.0/PaymentGateway.Services.TransactionHistory.dll"

# ── Node.js Services ──
echo "Starting Node.js services..."

echo -n "  webhook-receiver (port 4000) ... "
(cd "$BASE_DIR/webhook-receiver" && setsid node dist/main.js </dev/null &>"$LOG_DIR/webhook-receiver.log" &)
echo "started"

echo -n "  payment_gateway_web (port 3000) ... "
(cd "$BASE_DIR/web/payment_gateway_web" && setsid npx next start -p 3000 </dev/null &>"$LOG_DIR/web.log" &)
echo "started"

# ── Verify ──
echo ""
echo "Waiting for services to initialize..."
sleep 8

echo ""
echo "=== Process Check ==="
pgrep -af 'PaymentGateway\.' | grep -v grep || echo "(no dotnet processes found)"
pgrep -af 'node|next' | grep -v grep || echo "(no node processes found)"

echo ""
echo "=== Health Check ==="
curl -s http://localhost:5000/health -w " (HTTP %{http_code})" 2>/dev/null && echo "" || echo "API: not responding"
curl -s http://localhost:4000/health -w " (HTTP %{http_code})" 2>/dev/null && echo "" || echo "Webhook: not responding"
curl -s http://localhost:3000 -o /dev/null -w "Web: HTTP %{http_code}" 2>/dev/null && echo "" || echo "Web: not responding"

echo ""
echo "Logs: $LOG_DIR/"
echo "Done."
