#!/usr/bin/env bash
# Start PaymentGateway microservices locally in WSL
cd "$(dirname "$0")/.."

export ASPNETCORE_ENVIRONMENT=Development
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

PROCESSOR_DLL="src/services/PaymentGateway.Services.PaymentProcessor/bin/Release/net8.0/PaymentGateway.Services.PaymentProcessor.dll"
FRAUD_DLL="src/services/PaymentGateway.Services.FraudDetector/bin/Release/net8.0/PaymentGateway.Services.FraudDetector.dll"
SYNC_DLL="src/services/PaymentGateway.Services.MongoSync/bin/Release/net8.0/PaymentGateway.Services.MongoSync.dll"

echo "=== Starting PaymentProcessor ==="
dotnet "$PROCESSOR_DLL" &
PID1=$!

echo "=== Starting FraudDetector ==="
dotnet "$FRAUD_DLL" &
PID2=$!

echo "=== Starting MongoSync ==="
dotnet "$SYNC_DLL" &
PID3=$!

echo "Services PIDs: Processor=$PID1, FraudDetector=$PID2, MongoSync=$PID3"
echo "Press Ctrl+C to stop all."

trap "kill $PID1 $PID2 $PID3 2>/dev/null; exit" INT TERM
wait
