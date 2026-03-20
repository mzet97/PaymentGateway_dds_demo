#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

ensure_local_redis() {
  if ! command -v redis-server >/dev/null 2>&1 || ! command -v redis-cli >/dev/null 2>&1; then
    echo "[verify] ERROR: redis-server/redis-cli not found. Install with: apt-get install -y redis-server"
    exit 1
  fi

  if redis-cli -a Admin@123 ping >/dev/null 2>&1; then
    export ConnectionStrings__Redis="localhost:6379,password=Admin@123,abortConnect=false"
    export Redis__ConnectionString="localhost:6379,password=Admin@123,abortConnect=false"
    echo "[verify] Redis ready on localhost:6379 (password auth)."
    return
  fi

  if redis-cli ping >/dev/null 2>&1; then
    export ConnectionStrings__Redis="localhost:6379,abortConnect=false"
    export Redis__ConnectionString="localhost:6379,abortConnect=false"
    echo "[verify] Redis ready on localhost:6379 (no auth)."
    return
  fi

  echo "[verify] Starting local Redis on localhost:6379..."
  redis-server --port 6379 --save "" --appendonly no --requirepass Admin@123 --daemonize yes >/dev/null 2>&1
  sleep 1

  if ! redis-cli -a Admin@123 ping >/dev/null 2>&1; then
    echo "[verify] ERROR: unable to start Redis on localhost:6379 with expected password."
    exit 1
  fi

  export ConnectionStrings__Redis="localhost:6379,password=Admin@123,abortConnect=false"
  export Redis__ConnectionString="localhost:6379,password=Admin@123,abortConnect=false"
  echo "[verify] Redis started and ready."
}

ensure_local_redis

echo "[verify] Building PaymentGateway projects (Release)..."
dotnet build src/PaymentGateway.Migrations/PaymentGateway.Migrations.csproj -c Release --nologo
dotnet build src/PaymentGateway.Api/PaymentGateway.Api.csproj -c Release --nologo
dotnet build src/services/PaymentGateway.Services.FraudDetector/PaymentGateway.Services.FraudDetector.csproj -c Release --nologo
dotnet build src/services/PaymentGateway.Services.Settlement/PaymentGateway.Services.Settlement.csproj -c Release --nologo
dotnet build src/services/PaymentGateway.Services.MongoSync/PaymentGateway.Services.MongoSync.csproj -c Release --nologo
dotnet build tests/PaymentGateway.UnitTests/PaymentGateway.UnitTests.csproj -c Release --nologo
dotnet build tests/PaymentGateway.IntegrationTests/PaymentGateway.IntegrationTests.csproj -c Release --nologo

echo "[verify] Checking EF migrations tooling..."
dotnet ef migrations list \
  --project src/PaymentGateway.Persistence/PaymentGateway.Persistence.csproj \
  --startup-project src/PaymentGateway.Migrations/PaymentGateway.Migrations.csproj \
  >/dev/null

echo "[verify] Running unit tests..."
dotnet test tests/PaymentGateway.UnitTests/PaymentGateway.UnitTests.csproj -c Release --no-build --nologo

echo "[verify] Running integration tests..."
dotnet test tests/PaymentGateway.IntegrationTests/PaymentGateway.IntegrationTests.csproj -c Release --no-build --nologo

echo "[verify] OK"
