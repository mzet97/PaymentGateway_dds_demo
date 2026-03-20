# Payment Gateway DDS Demo

High-performance payment gateway using **CycloneDDS.NET** as message broker between .NET 8 microservices. Built to validate [CycloneDDS.NET](https://github.com/matheuslaidler/CycloneDds.NET) Linux support as a real-world demo.

## Architecture

```
Client ──► Next.js :3000 ──► .NET API :5000 ──► CycloneDDS ──► Microservices
                                  │                                    │
                                  ▼                                    ▼
                             MongoDB (write)                    FraudDetector (AI)
                             Redis (cache)                      PaymentProcessor
                             PostgreSQL (read) ◄── MongoSync    NotificationService
                                                                SettlementService
                                                                TransactionHistory
```

**Pattern:** CQRS + Event Sourcing + DDD with DDS pub/sub between services.

The API is intentionally "dumb" — it validates, writes to MongoDB, and publishes commands to DDS. All processing happens asynchronously in dedicated console app services.

## Tech Stack

| Layer | Technology |
|-------|------------|
| API | .NET 8 Minimal API, Paramore Brighter/Darker (CQRS) |
| Message Broker | CycloneDDS.NET (zero-allocation DDS bindings) |
| Write Store | MongoDB (high-throughput buffer) |
| Read Store | PostgreSQL + EF Core (consolidated queries) |
| Cache | Redis (sessions + data) |
| Frontend | Next.js 16, TypeScript, Tailwind |
| Webhooks | NestJS receiver with HMAC-SHA256 validation |
| AI Fraud Detection | OpenRouter / MiniMax M2.5 |
| Auth | Authentik OIDC + API Key (X-API-Key) |
| Files | MinIO (receipts, documents) |
| Observability | OpenTelemetry, Elasticsearch + Kibana |
| Scheduler | Quartz.NET (MongoDB → PostgreSQL sync every 2 min) |

## Project Structure

```
├── src/
│   ├── PaymentGateway.Api/              # Minimal API (REST + WebSocket)
│   ├── PaymentGateway.Application/      # CQRS commands, queries, handlers
│   ├── PaymentGateway.Domain/           # Entities, aggregates, value objects
│   ├── PaymentGateway.Infrastructure/   # MongoDB, Redis, DDS, EF Core, OpenRouter
│   └── services/
│       ├── PaymentProcessor/            # Payment lifecycle (create/approve/reject/refund)
│       ├── FraudDetector/               # AI-powered fraud analysis
│       ├── NotificationService/         # Email, SMS, webhook dispatch
│       ├── SettlementService/           # Daily batch reconciliation
│       ├── MongoSyncService/            # MongoDB → PostgreSQL bulk sync
│       └── TransactionHistory/          # Event store and analytics
├── web/payment_gateway_web/             # Next.js frontend
├── webhook-receiver/                    # NestJS webhook endpoint
├── tests/
│   ├── PaymentGateway.UnitTests/
│   ├── PaymentGateway.IntegrationTests/
│   └── PaymentGateway.Benchmarks/
├── scripts/                             # Start, benchmark, deploy scripts
├── configs/                             # CycloneDDS XML configurations
├── artifacts/                           # NuGet packages + native libs (libddsc.so)
├── docker-compose.yml
└── docs/
```

## Quick Start

### Prerequisites

- .NET 8 SDK
- Node.js 22+ (frontend/webhooks)
- Linux or WSL2 (CycloneDDS native libs)
- Docker (optional, for containerized deployment)

### Run Locally (WSL)

```bash
# Build all .NET projects
dotnet build PaymentGateway.sln -c Release

# Start all services (API + 6 microservices + frontend + webhook receiver)
bash scripts/start-all-bg.sh

# Or start individually:
dotnet run --project src/PaymentGateway.Api
dotnet run --project src/services/PaymentGateway.Services.PaymentProcessor
dotnet run --project src/services/PaymentGateway.Services.FraudDetector
# ... etc
```

### Run with Docker

```bash
# Build all 7 images
docker compose build

# Start all services
docker compose up -d
```

### Test the API

```bash
# Health check
curl http://localhost:5000/health

# Create a payment
curl -X POST http://localhost:5000/api/v1/payments \
  -H "Content-Type: application/json" \
  -H "X-API-Key: sk_test_smoke_merchant" \
  -d '{
    "merchantId": "11111111-1111-1111-1111-111111111111",
    "amount": 150.00,
    "currency": "BRL",
    "method": "pix",
    "customer": {
      "email": "customer@example.com",
      "name": "Test Customer",
      "document": "12345678901"
    }
  }'

# List payments
curl http://localhost:5000/api/v1/payments?merchantId=11111111-1111-1111-1111-111111111111 \
  -H "X-API-Key: sk_test_smoke_merchant"
```

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Health check |
| POST | `/api/v1/payments` | Create payment |
| GET | `/api/v1/payments` | List payments (paginated) |
| GET | `/api/v1/payments/{id}` | Get payment by ID |
| POST | `/api/v1/payments/{id}/refund` | Refund payment |
| POST | `/api/v1/payments/{id}/capture` | Capture payment |
| POST | `/api/v1/payments/{id}/cancel` | Cancel payment |
| GET | `/api/v1/merchants/{id}` | Get merchant |
| GET | `/api/v1/statistics` | Payment statistics |
| GET | `/api/v1/webhooks` | List webhooks |
| PUT | `/api/v1/webhooks` | Register webhook |
| WS | `/ws/payments` | Real-time payment updates |

## DDS Topics

Commands flow from API to services; events flow between services:

| Topic | Direction | Producer | Consumer |
|-------|-----------|----------|----------|
| `payment.create` | Command | API | PaymentProcessor |
| `payment.created` | Event | PaymentProcessor | FraudDetector, History |
| `fraud.check` | Command | PaymentProcessor | FraudDetector |
| `fraud.checked` | Event | FraudDetector | PaymentProcessor |
| `payment.approved` | Event | PaymentProcessor | NotificationService |
| `payment.rejected` | Event | PaymentProcessor | NotificationService |
| `payment.refunded` | Event | PaymentProcessor | NotificationService |
| `settlement.process` | Command | Quartz | SettlementService |

## Benchmark Results

k6 load test against a single instance on WSL2 (100 concurrent VUs, ~2 min):

| Metric | POST /payments | GET /health + /merchants |
|--------|---------------|--------------------------|
| **Throughput** | 2,813 req/s | 6,628 req/s |
| **Success Rate** | 100% | 100% |
| **Latency avg** | 20.8ms | 8.8ms |
| **Latency p50** | 16.2ms | 8.2ms |
| **Latency p95** | 47.9ms | 15.5ms |
| **Latency p99** | 77.7ms | 23.3ms |
| **Total Requests** | 281,303 | 662,826 |

Each POST request performs: input validation, MongoDB insert, DDS publish, idempotency check (Redis).

Run benchmarks yourself:

```bash
k6 run scripts/k6-bench-post.js
k6 run scripts/k6-bench-get.js
```

## Services

| Port | Service |
|------|---------|
| 5000 | PaymentGateway.Api |
| 3000 | Next.js Frontend |
| 4000 | Webhook Receiver (NestJS) |

Microservices (PaymentProcessor, FraudDetector, Notification, Settlement, MongoSync, TransactionHistory) are headless console apps that communicate exclusively via DDS topics.

## Configuration

Services are configured via environment variables (override `appsettings.json`):

```bash
export ConnectionStrings__DefaultConnection="Host=...;Database=demo-gateway;..."
export ConnectionStrings__MongoDb="mongodb://admin:pass@host:27017/..."
export Redis__ConnectionString="host:6379,password=..."
export OpenRouter__ApiKey="sk-or-..."
export CYCLONEDDS_URI="file:///path/to/cyclonedds-local.xml"
export LD_LIBRARY_PATH="/path/to/artifacts/native/linux-x64"
```

See `scripts/start-all-bg.sh` for a complete example.

## CI/CD

GitHub Actions builds and pushes all 7 Docker images to GitHub Container Registry on every push to `main`:

```
ghcr.io/<owner>/paymentgateway-dds-api:latest
ghcr.io/<owner>/paymentgateway-dds-processor:latest
ghcr.io/<owner>/paymentgateway-dds-fraud-detector:latest
ghcr.io/<owner>/paymentgateway-dds-notification:latest
ghcr.io/<owner>/paymentgateway-dds-settlement:latest
ghcr.io/<owner>/paymentgateway-dds-mongo-sync:latest
ghcr.io/<owner>/paymentgateway-dds-transaction-history:latest
```

## Documentation

| File | Description |
|------|-------------|
| [docs/architecture.md](docs/architecture.md) | System architecture with Mermaid diagrams |
| [docs/business-logic.md](docs/business-logic.md) | Business rules and domain model |
| [docs/cqrs-events.md](docs/cqrs-events.md) | Event flows and sequence diagrams |
| [docs/deployment.md](docs/deployment.md) | Deployment guide |
| [docs/execution.md](docs/execution.md) | Execution instructions |
| [docs/observability.md](docs/observability.md) | Logging, OpenTelemetry, Elasticsearch |
| [docs/authentik-setup-demo.md](docs/authentik-setup-demo.md) | Authentik OIDC setup |

## Tests

```bash
# Unit tests
dotnet test tests/PaymentGateway.UnitTests -c Release

# Integration tests
dotnet test tests/PaymentGateway.IntegrationTests -c Release

# Benchmarks
dotnet run --project tests/PaymentGateway.Benchmarks -c Release
```

## License

See [LICENSE](LICENSE).
