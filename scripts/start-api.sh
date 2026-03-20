#!/usr/bin/env bash
# Start pre-built PaymentGateway API
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS='http://+:5000'
export ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=demo-gateway;Username=app;Password=Admin@123'
export ConnectionStrings__MongoDb='mongodb://admin:Admin%40123@192.168.1.51:27017/?authSource=admin'
export ConnectionStrings__Redis='192.168.1.51:30379,password=Admin@123,abortConnect=false,connectTimeout=5000,syncTimeout=3000'
export Redis__ConnectionString='192.168.1.51:30379,password=Admin@123,abortConnect=false,connectTimeout=5000,syncTimeout=3000'
export Dds__UseRealDds=false
export Dds__EnableGracefulFallback=true
export OpenRouter__ApiKey='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'
export Authentik__RequireHttpsMetadata=false
export Authentik__Authority='https://authentik.home.arpa/application/o/payment-gateway-web/'
export Telemetry__EnableElasticsearchLogging=false
export Telemetry__EnableOtlp=false
export Telemetry__EnableConsoleExporter=false
export Minio__UseSsl=false

DLL="/mnt/e/TI/git/tese/PaymentGateway_dds_demo/src/PaymentGateway.Api/bin/Release/net8.0/PaymentGateway.Api.dll"
exec setsid dotnet "$DLL" >> /tmp/pgw-api.log 2>&1
