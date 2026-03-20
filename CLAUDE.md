# Payment Gateway - Project Instructions

This file provides guidance to Claude Code when working with this project.

## Project Overview

High-performance payment gateway demo using CycloneDDS.NET on Linux. Architecture: CQRS + Event Sourcing + DDD with DDS as message broker. Designed for millions of requests per second with sub-50ms latency.

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                         PRESENTATION LAYER                          │
│  ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐   │
│  │ Next.js  │    │ Authentik│    │ WebSocket│    │  MinIO   │   │
│  │ Frontend │    │   OIDC   │    │  Updates │    │  Files   │   │
│  └────┬─────┘    └────┬─────┘    └────┬─────┘    └────┬─────┘   │
│       │                │                │                │          │
│       └────────────────┼────────────────┼────────────────┘          │
│                        ▼                                          │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                    Minimal API (Dumb)                       │   │
│  │         Only validates and publishes to DDS                 │   │
│  └──────────────────────────────────────────────────────────────┘   │
└──────────────────────────────┬──────────────────────────────────────┘
                               │
                               ▼
┌──────────────────────────────────────────────────────────────────────┐
│                       APPLICATION LAYER                             │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────┐ │
│  │   CQRS Handler  │  │   Event Publisher│  │  MediatR Pipeline   │ │
│  │   (Commands)    │  │   (to DDS)       │  │  (Validation)       │ │
│  └────────┬────────┘  └────────┬────────┘  └──────────┬──────────┘ │
└───────────┼─────────────────────┼──────────────────────┼────────────┘
            │                     │                      │
            ▼                     ▼                      ▼
┌──────────────────────────────────────────────────────────────────────┐
│                      INFRASTRUCTURE LAYER                           │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐          │
│  │CycloneDDS│  │ MongoDB  │  │PostgreSQL│  │  Redis   │          │
│  │ Message  │  │  Buffer  │  │ Read DB  │  │  Cache   │          │
│  │  Broker  │  │(Write)   │  │(Query)   │  │          │          │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘          │
│       │             │              │              │                  │
│       └─────────────┼──────────────┼──────────────┘                  │
│                     ▼                                               │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │              Quartz.NET Sync Job (every 2 min)              │    │
│  │              MongoDB → PostgreSQL bulk sync                 │    │
│  └─────────────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌──────────────────────────────────────────────────────────────────────┐
│                    SERVICES (Console Apps)                           │
│  ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────────┐│
│  │PaymentProcessor  │ │ FraudDetector    │ │ NotificationService  ││
│  │ • create         │ │ • AI Analysis    │ │ • Email              ││
│  │ • approve        │ │ • Risk Score     │ │ • SMS                ││
│  │ • reject         │ │ • Decision       │ │ • Webhook            ││
│  │ • refund         │ │                  │ │                      ││
│  └──────────────────┘ └──────────────────┘ └──────────────────────┘│
│  ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────────┐│
│  │SettlementService │ │ MongoSyncService │ │ TransactionHistory   ││
│  │ • Daily Batch    │ │ • Bulk Copy     │ │ • Event Store        ││
│  │ • Reconciliation│ │ • Cleanup       │ │ • Analytics          ││
│  │ • Daily Closer   │ │                  │ │                      ││
│  └──────────────────┘ └──────────────────┘ └──────────────────────┘│
└──────────────────────────────────────────────────────────────────────┘
```

## Tech Stack

| Layer | Technology | Purpose |
|-------|------------|---------|
| Frontend | Next.js 14, TypeScript, Tailwind | Web UI |
| API | .NET 8 Minimal API | REST entry point |
| CQRS | MediatR | Command/Query separation |
| Domain | DDD + Event Sourcing | Business logic |
| Message Broker | CycloneDDS.NET | High-performance pub/sub |
| Write Buffer | MongoDB | High-throughput writes |
| Read DB | PostgreSQL + EF Core | Consolidated reads |
| Cache | Redis | Session + data cache |
| Files | MinIO | Receipts, documents |
| Logs/APM | Elasticsearch + Kibana | Observability |
| AI | OpenRouter (MiniMax M2.5) | Fraud detection |
| Auth | Authentik OIDC | Authentication |
| Scheduler | Quartz.NET | Batch jobs |

## Quick Start

### Prerequisites
- .NET 8 SDK
- Docker/Docker Compose (for infrastructure)
- Linux or WSL2 (recommended)

### Start Infrastructure
```bash
# Start all infrastructure services
docker-compose up -d
```

### Build and Run
```bash
# Build all projects
dotnet build PaymentGateway.sln -c Release

# Validate EF Core tooling and list pending migrations
dotnet ef migrations list \
  --project src/PaymentGateway.Persistence/PaymentGateway.Persistence.csproj \
  --startup-project src/PaymentGateway.Migrations/PaymentGateway.Migrations.csproj

# Run API (terminal 1)
dotnet run --project src/PaymentGateway.Api

# Run Services (separate terminals)
dotnet run --project src/services/PaymentGateway.Services.PaymentProcessor
dotnet run --project src/services/PaymentGateway.Services.FraudDetector
dotnet run --project src/services/PaymentGateway.Services.Notification
```

### Run Tests
```bash
dotnet test tests/PaymentGateway.Tests -c Release
```

## Project Structure

```
demo/PaymentGateway/
├── src/
│   ├── PaymentGateway.Api/           # Minimal API entry point
│   │   ├── Controllers/              # Minimal API endpoints
│   │   ├── Commands/                 # Request DTOs
│   │   ├── Queries/                  # Query DTOs
│   │   ├── Program.cs                # Entry point + DI setup
│   │   └── appsettings.json          # Configuration
│   │
│   ├── PaymentGateway.Application/   # CQRS Handlers
│   │   ├── Commands/                 # Command definitions
│   │   │   ├── CreatePaymentCommand.cs
│   │   │   ├── ApprovePaymentCommand.cs
│   │   │   └── RefundPaymentCommand.cs
│   │   ├── Queries/                  # Query definitions
│   │   │   └── GetPaymentQueries.cs
│   │   ├── Handlers/                # Command/Query handlers
│   │   │   └── PaymentHandlers.cs
│   │   └── DTOs/                    # Data transfer objects
│   │
│   ├── PaymentGateway.Domain/       # DDD Domain
│   │   ├── Entities/
│   │   │   ├── Payment.cs            # Payment entity
│   │   │   ├── Merchant.cs           # Merchant entity
│   │   │   └── Transaction.cs        # Transaction entity
│   │   ├── Aggregates/
│   │   │   └── PaymentAggregate.cs   # Payment aggregate root
│   │   ├── Events/
│   │   │   └── DomainEvents.cs      # Domain events
│   │   ├── ValueObjects/
│   │   │   ├── Money.cs              # Money value object
│   │   │   ├── CustomerInfo.cs       # Customer info
│   │   │   └── FraudCheckResult.cs   # Fraud result
│   │   ├── Enums/
│   │   │   ├── PaymentStatus.cs      # Payment status enum
│   │   │   ├── PaymentMethod.cs      # Payment method enum
│   │   │   └── FraudDecision.cs      # Fraud decision enum
│   │   └── Services/
│   │
│   ├── PaymentGateway.Persistence/    # EF Core + PostgreSQL + migrations model
│   │   ├── Configuration/
│   │   ├── Entities/
│   │   ├── Mappings/
│   │   ├── Migrations/
│   │   └── Repositories/
│   │
│   ├── PaymentGateway.Migrations/     # EF tooling startup/design-time factory
│   │   ├── Program.cs
│   │   └── PaymentDbContextFactory.cs
│   │
│   ├── PaymentGateway.Infrastructure/ # Infrastructure
│   │   ├── Persistence/
│   │   │   ├── MongoDbContext.cs      # MongoDB context
│   │   │   ├── Entities/              # Mongo entities
│   │   │   └── Repositories/          # Mongo repository implementations
│   │   ├── Redis/
│   │   │   ├── RedisService.cs        # Redis service
│   │   │   └── RedisServiceExtensions.cs
│   │   ├── DDS/
│   │   │   ├── DdsPublisher.cs        # DDS publisher
│   │   │   └── DdsSubscriber.cs      # DDS subscriber
│   │   └── Services/
│   │       └── OpenRouterFraudService.cs # AI fraud detection
│   │
│   └── services/                     # Console Apps (DDS Handlers)
│       ├── PaymentGateway.Services.PaymentProcessor/
│       │   ├── Program.cs
│       │   ├── Commands/              # Command handlers
│       │   └── Services/              # Business logic
│       │
│       ├── PaymentGateway.Services.FraudDetector/
│       │   ├── Program.cs
│       │   ├── Services/
│       │   │   ├── FraudAnalyzer.cs   # AI analysis
│       │   │   └── DecisionEngine.cs  # Risk decision
│       │   └── Config/
│       │
│       ├── PaymentGateway.Services.Notification/
│       │   ├── Program.cs
│       │   ├── EmailSender.cs
│       │   ├── SmsGateway.cs
│       │   └── WebhookDispatcher.cs
│       │
│       ├── PaymentGateway.Services.Settlement/
│       │   ├── Program.cs
│       │   ├── BatchProcessor.cs
│       │   ├── Reconciler.cs
│       │   └── DailyCloser.cs
│       │
│       └── PaymentGateway.Services.MongoSync/
│           ├── Program.cs
│           └── SyncJob.cs              # Quartz job
│
├── web/
│   └── PaymentGateway.Web/           # Next.js Frontend
│       ├── app/                      # Next.js 14 app router
│       ├── components/               # React components
│       ├── lib/                      # Utilities
│       └── public/                   # Static assets
│
├── tests/
│   └── PaymentGateway.Tests/
│       ├── Unit/                     # Unit tests
│       └── Integration/              # Integration tests
│
├── docs/
│   ├── architecture.md              # Detailed architecture
│   ├── business-logic.md            # Business rules
│   └── cqrs-events.md              # Event flows
│
├── scripts/
│   └── benchmark.ps1                 # Performance benchmarks
│
├── docker-compose.yml                # Infrastructure services
├── PaymentGateway.sln
├── PaymentGateway.slnx               # Optional IDE solution format
└── CLAUDE.md
```

## Key Design Patterns

### CQRS (Command Query Responsibility Segregation)

**Commands (Write):**
- `CreatePaymentCommand` - Create new payment
- `ApprovePaymentCommand` - Approve pending payment
- `RejectPaymentCommand` - Reject payment
- `RefundPaymentCommand` - Refund payment
- `CapturePaymentCommand` - Capture authorized payment

**Queries (Read):**
- `GetPaymentByIdQuery` - Get payment by ID
- `GetPaymentsByMerchantQuery` - Get merchant payments
- `GetPaymentsByCustomerQuery` - Get customer payments

### Event Sourcing

All state changes are stored as events:
- `PaymentCreatedEvent` - Payment created
- `PaymentApprovedEvent` - Payment approved
- `PaymentRejectedEvent` - Payment rejected
- `FraudCheckedEvent` - Fraud check completed
- `PaymentRefundedEvent` - Payment refunded

**Benefits:**
- Complete audit trail
- Event replay for debugging
- Temporal queries
- Scalable event storage

### DDS Topics

Commands flow to handlers, events flow to subscribers:

| Topic | Type | Producer | Consumer |
|-------|------|----------|----------|
| `payment.create` | Command | API | PaymentProcessor |
| `payment.created` | Event | PaymentProcessor | FraudDetector, History |
| `fraud.check` | Command | PaymentProcessor | FraudDetector |
| `fraud.checked` | Event | FraudDetector | PaymentProcessor |
| `payment.approved` | Event | PaymentProcessor | NotificationService |
| `payment.rejected` | Event | PaymentProcessor | NotificationService |
| `payment.capture` | Command | API | PaymentProcessor |
| `payment.refund` | Command | API | PaymentProcessor |
| `payment.refunded` | Event | PaymentProcessor | NotificationService |
| `settlement.process` | Command | Quartz | SettlementService |
| `notification.send` | Command | Multiple | NotificationService |

### SOLID Principles

Each console app has a single responsibility:
- **PaymentProcessor**: Handles payment lifecycle (create, approve, reject, refund, capture)
- **FraudDetector**: Analyzes transactions using AI
- **NotificationService**: Sends emails, SMS, webhooks
- **SettlementService**: Daily batch settlement
- **MongoSyncService**: Syncs MongoDB to PostgreSQL

## API Endpoints

### Payments

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /payments | Create payment |
| GET | /payments/{id} | Get payment by ID |
| GET | /payments | List payments (paginated) |
| POST | /payments/{id}/approve | Approve payment |
| POST | /payments/{id}/reject | Reject payment |
| POST | /payments/{id}/refund | Refund payment |
| POST | /payments/{id}/capture | Capture payment |

### Merchants

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /merchants | Register merchant |
| GET | /merchants/{id} | Get merchant |
| GET | /merchants/{id}/payments | Merchant payments |

### WebSocket

```javascript
// Subscribe to payment updates
const ws = new WebSocket('ws://localhost:5000/ws/payments');
ws.onmessage = (event) => {
  const payment = JSON.parse(event.data);
  console.log('Payment update:', payment);
};
```

## Configuration

### appsettings.json
```json
{
  "Database": {
    "PostgreSQL": {
      "Host": "spsql.home.arpa",
      "Port": 5432,
      "Database": "demo-gateway",
      "Username": "app",
      "Password": "Admin@123"
    },
    "MongoDB": {
      "Host": "mongodb.home.arpa",
      "Port": 27017,
      "Database": "demo-gateway"
    }
  },
  "Redis": {
    "Host": "redis.home.arpa",
    "Port": 6379
  },
  "CycloneDDS": {
    "Domain": 0,
    "ParticipantName": "PaymentGateway"
  },
  "OpenRouter": {
    "ApiKey": "sk-or-...",
    "Model": "minimax/minimax-m2.5"
  }
}
```

### Environment Variables
```bash
# Database
export POSTGRES_HOST=spsql.home.arpa
export POSTGRES_PASSWORD=Admin@123
export MONGO_HOST=mongodb.home.arpa

# Redis
export REDIS_HOST=redis.home.arpa
export REDIS_PASSWORD=Admin@123

# CycloneDDS
export CYCLONEDDS_URI=file://cyclonedds.xml

# OpenRouter
export OPENROUTER_API_KEY=sk-or-...
```

## Performance Targets

| Metric | Target |
|--------|--------|
| RPS (Requests/sec) | 1,000,000+ |
| API Latency p99 | < 50ms |
| DDS Latency | < 5ms |
| Database Write | < 10ms |
| Cache Hit Rate | > 95% |

## Benchmarking

Run performance benchmarks:
```bash
# Performance test
pwsh scripts/benchmark.ps1

# Or use dotnet script
dotnet script scripts/benchmark.csx
```

Metrics captured:
- RPS (Requests per second)
- Latency p50, p95, p99
- DDS publish/subscribe latency
- Memory allocation
- GC collections

## Dependencies Graph

```
PaymentGateway.Domain
    └── (no dependencies)

PaymentGateway.Application
    └── PaymentGateway.Domain

PaymentGateway.Infrastructure
    ├── PaymentGateway.Domain
    └── PaymentGateway.Application

PaymentGateway.Api
    ├── PaymentGateway.Application
    └── PaymentGateway.Infrastructure

Services (Console Apps)
    ├── PaymentGateway.Domain
    ├── PaymentGateway.Application
    └── PaymentGateway.Infrastructure
```

## Testing Strategy

### Unit Tests
- Domain logic (aggregates, entities, value objects)
- Command/Query handlers
- Application services

### Integration Tests
- API endpoints
- Database operations
- Redis caching
- DDS pub/sub

### Load Tests
- k6 scripts
- Benchmark.ps1

## Observability

### OpenTelemetry
All services export metrics via OpenTelemetry:
- Request latency
- DDS latency
- Database query time
- Cache hit rate
- Custom business metrics

### Elasticsearch + Kibana
- Log aggregation
- APM traces
- Custom dashboards

### Health Checks
```bash
# Check API health
curl http://localhost:5000/health

# Check service status
curl http://localhost:5000/health/live
curl http://localhost:5000/health/ready
```

## Common Tasks

### Add New Command
1. Create command class in `PaymentGateway.Application/Commands/`
2. Create handler in `PaymentGateway.Application/Handlers/`
3. Add endpoint in `PaymentGateway.Api/Controllers/`

### Add New DDS Topic
1. Define event class in `PaymentGateway.Domain/Events/`
2. Update publisher in `PaymentGateway.Infrastructure/DDS/`
3. Add subscriber in relevant service

### Add New Service
1. Create new console app project
2. Reference Domain, Application, Infrastructure
3. Implement Program.cs with DDS subscriber
4. Add to docker-compose.yml

### EF Core Migrations
```bash
# List migrations
dotnet ef migrations list \
  --project src/PaymentGateway.Persistence/PaymentGateway.Persistence.csproj \
  --startup-project src/PaymentGateway.Migrations/PaymentGateway.Migrations.csproj

# Add migration
dotnet ef migrations add <MigrationName> \
  --project src/PaymentGateway.Persistence/PaymentGateway.Persistence.csproj \
  --startup-project src/PaymentGateway.Migrations/PaymentGateway.Migrations.csproj

# Apply migration
dotnet ef database update \
  --project src/PaymentGateway.Persistence/PaymentGateway.Persistence.csproj \
  --startup-project src/PaymentGateway.Migrations/PaymentGateway.Migrations.csproj
```

## Troubleshooting

### DDS Connection Issues
```bash
# Check CycloneDDS configuration
export CYCLONEDDS_URI=file://cyclonedds.xml

# Verify domain is accessible
dotnet run --project PaymentGateway.Services.PaymentProcessor -- --verify
```

### Database Connection
```bash
# Test PostgreSQL
dotnet run --project PaymentGateway.Infrastructure -- --test-pg

# Test MongoDB
dotnet run --project PaymentGateway.Infrastructure -- --test-mongo
```

### Performance Issues
1. Check Redis cache hit rate
2. Verify MongoDB indexes
3. Monitor DDS topic backlog
4. Review Elasticsearch slow queries

## Notes

- API is "dumb" - only validates and publishes to DDS
- All processing happens async via Console Apps
- MongoDB buffer for high-throughput writes
- Quartz Job syncs to PostgreSQL every 2 minutes
- Use WebSocket for real-time payment updates
- All monetary values use decimal type (Money value object)
- Fraud detection uses MiniMax M2.5 via OpenRouter API


<claude-mem-context>
# Recent Activity

### Mar 17, 2026

| ID | Time | T | Title | Read |
|----|------|---|-------|------|
| #641 | 10:22 AM | 🔵 | PaymentGateway demo targets 1+ million RPS with sub-50ms latency using event-sourced CQRS architecture | ~559 |
</claude-mem-context>
