#!/usr/bin/env bash
# Run PaymentGateway API locally in WSL
set -u

cd "$(dirname "$0")/.."

export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS='http://+:5000'

# PostgreSQL on dedicated DB server (.52)
export ConnectionStrings__DefaultConnection='Host=spsql.home.arpa;Port=5432;Database=demo-gateway;User Id=app;Password=Admin@123;'

# MongoDB on K8s cluster (.51)
export ConnectionStrings__MongoDb='mongodb://admin:Admin%40123@mongodb.home.arpa:27017/?authSource=admin'

# Redis on K8s cluster (.51 NodePort 30379)
export ConnectionStrings__Redis='redis.home.arpa:30379,password=Admin@123,abortConnect=false,connectTimeout=5000,syncTimeout=3000'
export Redis__ConnectionString='redis.home.arpa:30379,password=Admin@123,abortConnect=false,connectTimeout=5000,syncTimeout=3000'

# DDS - use real CycloneDDS for inter-process communication
export Dds__UseRealDds=true
export Dds__EnableGracefulFallback=true
export LD_LIBRARY_PATH="/mnt/e/TI/git/tese/PaymentGateway_dds_demo/artifacts/native/linux-x64:${LD_LIBRARY_PATH:-}"
# CycloneDDS config for WSL (unicast peer discovery)
export CYCLONEDDS_URI="file:///mnt/e/TI/git/tese/PaymentGateway_dds_demo/configs/cyclonedds-local.xml"

# OpenRouter AI for fraud detection
export OpenRouter__ApiKey='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'
export OpenRouter__BaseUrl='https://openrouter.ai/api/v1'
export OpenRouter__Model='minimax/minimax-m2.5'

# Authentik - allow self-signed certs for local
export Authentik__RequireHttpsMetadata=false
export Authentik__Authority='https://authentik.home.arpa/application/o/payment-gateway-web/'

# Elasticsearch logging via Serilog + OpenTelemetry APM
export Telemetry__EnableElasticsearchLogging=true
export Telemetry__ElasticsearchUrl='https://elasticsearch.home.arpa/'
export Telemetry__SkipTlsValidation=true
export Telemetry__AllowSelfSignedCertificates=true
export Telemetry__EnableOtlp=false
export Telemetry__EnableConsoleExporter=false

# MinIO
export Minio__Endpoint='minio-s3.home.arpa'
export Minio__AccessKey='admin'
export Minio__SecretKey='Admin@123'
export Minio__BucketName='demo-gateway'
export Minio__UseSsl=true

echo "=== PaymentGateway API - Local Dev ==="
echo "PostgreSQL:    spsql.home.arpa:5432/demo-gateway"
echo "MongoDB:       mongodb.home.arpa:27017"
echo "Redis:         redis.home.arpa:30379"
echo "Elasticsearch: https://elasticsearch.home.arpa/"
echo "Authentik:     https://authentik.home.arpa"
echo "OpenRouter:    minimax/minimax-m2.5"
echo "API URL:       http://localhost:5000"
echo "======================================="

# Run the pre-built DLL directly (faster startup, no rebuild)
DLL="src/PaymentGateway.Api/bin/Release/net8.0/PaymentGateway.Api.dll"
if [ -f "$DLL" ]; then
    exec dotnet "$DLL"
else
    echo "DLL not found, building..."
    exec dotnet run --project src/PaymentGateway.Api/PaymentGateway.Api.csproj --no-launch-profile
fi
