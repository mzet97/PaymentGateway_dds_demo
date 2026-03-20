# Payment Gateway - Arquitetura do Sistema v2

> **Versão:** 2.0
> **Data:** 2026-03-18
> **Status:** Implementado

---

## 1. Visão Geral da Arquitetura

```mermaid
graph TB
    subgraph PRESENTATION["Presentation Layer"]
        WEB["Next.js Frontend<br/>:3000"]
        WEBHOOK_RX["Webhook Receiver<br/>NestJS :4000"]
    end

    subgraph API_LAYER["API Layer"]
        API["PaymentGateway.Api<br/>.NET 8 Minimal API :5000<br/>MediatR CQRS Pipeline"]
    end

    subgraph DDS_BUS["CycloneDDS Message Bus"]
        direction LR
        T1(["payment.create"])
        T2(["payment.created"])
        T3(["fraud.check"])
        T4(["fraud.checked"])
        T5(["payment.approved"])
        T6(["payment.rejected"])
        T7(["payment.refunded"])
        T8(["payment.captured"])
        T9(["settlement.process"])
        T10(["notification.send"])
    end

    subgraph SERVICES["Microservices - Console Apps"]
        PROC["PaymentProcessor<br/>Create / Approve<br/>Reject / Refund / Capture"]
        FRAUD["FraudDetector<br/>AI Analysis<br/>Risk Score / Decision"]
        NOTIF["NotificationService<br/>Email / SMS<br/>Webhook Dispatch"]
        SETTLE["SettlementService<br/>Daily Batch<br/>Reconciliation"]
        SYNC["MongoSyncService<br/>Bulk Copy<br/>MongoDB to PostgreSQL"]
        HIST["TransactionHistory<br/>Event Store<br/>Analytics"]
    end

    subgraph INFRA["Infrastructure"]
        MONGO[("MongoDB<br/>Write Buffer")]
        PG[("PostgreSQL<br/>Read DB")]
        REDIS[("Redis<br/>Cache")]
        MINIO[("MinIO<br/>Files/Receipts")]
        ELASTIC[("Elasticsearch<br/>Logs and APM")]
        OPENROUTER["OpenRouter API<br/>MiniMax M2.5"]
    end

    WEB -->|"REST /api/v1/*<br/>proxy via rewrites"| API
    WEB -.->|"WebSocket<br/>/ws/payments"| API

    API -->|"Publish Commands"| T1
    API -->|"Publish Commands"| T9

    T1 -->|subscribe| PROC
    PROC -->|publish| T2
    PROC -->|publish| T3
    PROC -->|publish| T5
    PROC -->|publish| T6
    PROC -->|publish| T7
    PROC -->|publish| T8

    T3 -->|subscribe| FRAUD
    FRAUD -->|publish| T4
    T4 -->|subscribe| PROC

    T2 -->|subscribe| HIST
    T5 -->|subscribe| NOTIF
    T6 -->|subscribe| NOTIF
    T7 -->|subscribe| NOTIF
    T8 -->|subscribe| NOTIF

    T9 -->|subscribe| SETTLE

    NOTIF -->|"POST /webhooks/*<br/>HMAC-SHA256"| WEBHOOK_RX

    API --> MONGO
    API --> REDIS
    PROC --> MONGO
    FRAUD --> OPENROUTER
    FRAUD --> MONGO
    SYNC --> MONGO
    SYNC --> PG
    SETTLE --> PG
    HIST --> MONGO
    API --> ELASTIC
    NOTIF --> MINIO

    API -.->|"Query reads"| PG

    classDef presentation fill:#dbeafe,stroke:#3b82f6,color:#1e3a5f
    classDef api fill:#fef3c7,stroke:#f59e0b,color:#78350f
    classDef dds fill:#dcfce7,stroke:#22c55e,color:#14532d
    classDef service fill:#f3e8ff,stroke:#a855f7,color:#3b0764
    classDef infra fill:#fee2e2,stroke:#ef4444,color:#7f1d1d
    classDef external fill:#e0e7ff,stroke:#6366f1,color:#312e81

    class WEB,WEBHOOK_RX presentation
    class API api
    class T1,T2,T3,T4,T5,T6,T7,T8,T9,T10 dds
    class PROC,FRAUD,NOTIF,SETTLE,SYNC,HIST service
    class MONGO,PG,REDIS,MINIO,ELASTIC infra
    class OPENROUTER external
```

---

## 2. Fluxo Principal de um Pagamento

```mermaid
sequenceDiagram
    participant Web as Next.js :3000
    participant API as API :5000
    participant DDS as CycloneDDS
    participant PP as PaymentProcessor
    participant FD as FraudDetector
    participant NS as NotificationService
    participant WH as Webhook Receiver :4000
    participant Mongo as MongoDB
    participant PG as PostgreSQL

    Web->>API: POST /api/v1/payments
    API->>Mongo: Save (Write Buffer)
    API->>DDS: publish payment.create

    DDS->>PP: subscribe payment.create
    PP->>DDS: publish payment.created
    PP->>DDS: publish fraud.check

    DDS->>FD: subscribe fraud.check
    FD->>FD: AI Analysis (OpenRouter)
    FD->>DDS: publish fraud.checked

    DDS->>PP: subscribe fraud.checked
    alt Approved
        PP->>DDS: publish payment.approved
        DDS->>NS: subscribe payment.approved
        NS->>WH: POST /webhooks/payment.approved (HMAC-SHA256)
    else Rejected
        PP->>DDS: publish payment.rejected
        DDS->>NS: subscribe payment.rejected
        NS->>WH: POST /webhooks/payment.rejected
    end

    Note over Mongo,PG: MongoSync (Quartz every 2min) - MongoDB to PostgreSQL bulk copy
    Web->>API: GET /api/v1/statistics
    API->>PG: Query (Read DB)
```

---

## 3. Padrao CQRS - Write vs Read Path

```mermaid
graph LR
    subgraph WRITE["Write Path"]
        CMD["Command"] --> DDS_PUB["DDS Publish"]
        DDS_PUB --> HANDLER["Service Handler"]
        HANDLER --> MONGO_W[("MongoDB")]
    end

    subgraph SYNC_PATH["Sync"]
        MONGO_W -.->|"Quartz 2min"| PG_W[("PostgreSQL")]
    end

    subgraph READ["Read Path"]
        QUERY["Query"] --> PG_R[("PostgreSQL")]
        PG_R --> CACHE["Redis Cache"]
    end

    classDef write fill:#dcfce7,stroke:#22c55e
    classDef read fill:#dbeafe,stroke:#3b82f6
    classDef sync fill:#fef3c7,stroke:#f59e0b

    class CMD,DDS_PUB,HANDLER,MONGO_W write
    class QUERY,PG_R,CACHE read
    class PG_W sync
```

---

## 4. DDS Topics - Commands e Events

```mermaid
flowchart LR
    subgraph COMMANDS["Command Topics"]
        C1["payment.create"]
        C2["payment.approve"]
        C3["payment.reject"]
        C4["payment.refund"]
        C5["payment.capture"]
        C6["fraud.check"]
        C7["settlement.process"]
        C8["notification.send"]
    end

    subgraph EVENTS["Event Topics"]
        E1["payment.created"]
        E2["payment.approved"]
        E3["payment.rejected"]
        E4["payment.refunded"]
        E5["payment.captured"]
        E6["fraud.checked"]
        E7["notification.sent"]
        E8["settlement.completed"]
    end

    subgraph HANDLERS["Service Handlers"]
        H1["PaymentProcessor"]
        H2["FraudDetector"]
        H3["NotificationService"]
        H4["SettlementService"]
        H5["TransactionHistory"]
        H6["MongoSyncService"]
    end

    C1 --> H1
    C2 --> H1
    C3 --> H1
    C4 --> H1
    C5 --> H1
    C6 --> H2
    C7 --> H4
    C8 --> H3

    H1 --> E1
    H1 --> E2
    H1 --> E3
    H1 --> E4
    H1 --> E5
    H2 --> E6
    H3 --> E7
    H4 --> E8

    E1 --> H5
    E2 --> H3
    E3 --> H3
    E4 --> H3
    E5 --> H3
    E6 --> H1
```

---

## 5. Console Apps - SOLID Responsibilities

```mermaid
graph TB
    subgraph SERVICES["PaymentGateway.Services"]
        subgraph PP["PaymentProcessor"]
            PP1[CreatePayment]
            PP2[ApprovePayment]
            PP3[RejectPayment]
            PP4[RefundPayment]
            PP5[CapturePayment]
        end

        subgraph FD["FraudDetector"]
            FD1[CheckFraud]
            FD2[CalculateScore]
            FD3[DecisionEngine]
        end

        subgraph NS["NotificationService"]
            NS1[EmailSender]
            NS2[SMSGateway]
            NS3[WebhookDispatcher]
        end

        subgraph SS["SettlementService"]
            SS1[BatchProcessor]
            SS2[Reconciler]
            SS3[DailyCloser]
        end

        subgraph MS["MongoSyncService"]
            MS1[QuartzJob]
            MS2[BulkCopy]
            MS3[Cleanup]
        end

        subgraph TH["TransactionHistory"]
            TH1[EventStore]
            TH2[Analytics]
            TH3[ReportGen]
        end
    end

    DDS["CycloneDDS Bus"] -.->|subscribe| PP1
    DDS -.->|subscribe| FD1
    DDS -.->|subscribe| NS1
    DDS -.->|subscribe| SS1
    DDS -.->|subscribe| TH1
```

---

## 6. Clean Architecture Layers

```mermaid
graph TB
    subgraph PRES["Presentation"]
        API[Minimal API]
        WEBUI[Next.js Web]
    end

    subgraph APP["Application"]
        CMD[Commands]
        QRY[Queries]
        HAND[Handlers]
    end

    subgraph DOM["Domain"]
        AGG[Aggregates]
        EVT[Events]
        SRV[Domain Services]
        VAL[Value Objects]
    end

    subgraph INF["Infrastructure"]
        DDS[CycloneDDS]
        EF[EF Core]
        RDS[Redis]
        MIO[MinIO]
    end

    API --> CMD
    API --> QRY
    CMD --> HAND
    QRY --> HAND
    HAND --> AGG
    HAND --> EVT
    AGG --> SRV
    AGG --> VAL
    EVT --> DDS
    SRV --> EF
    SRV --> RDS
```

---

## 7. Autenticacao e Autorizacao

```mermaid
flowchart LR
    subgraph AUTH_FLOW["Auth Flow"]
        USER[User] -->|Login| AUTH[Authentik]
        AUTH -->|OIDC| TOKEN[JWT Token]
        TOKEN -->|Bearer| APIG[API]
        TOKEN -->|Bearer| FEG[Frontend]
    end

    subgraph CLAIMS["Token Claims"]
        TOKEN -->|contains| SUB[sub: user_id]
        TOKEN -->|contains| ROLE[role: merchant/admin]
        TOKEN -->|contains| MERCH[merchant_id]
        TOKEN -->|contains| PERM[permissions]
    end

    subgraph AUTHZ["Authorization"]
        APIG -->|check| PERM
        PERM -->|allow| RD[GET /payments]
        PERM -->|allow| WR[POST /payments]
        PERM -->|admin| ADM[DELETE /payments/:id]
    end
```

---

## 8. Observabilidade

```mermaid
graph LR
    subgraph COLLECT["Metrics Collection"]
        APP[Application] -->|OTel| OTEL[OpenTelemetry]
        OTEL -->|export| ELK[Elasticsearch]
    end

    subgraph VIS["Visualization"]
        ELK -->|query| KIB[Kibana Dashboard]
        KIB -->|show| MET[Metrics View]
        KIB -->|show| LOG[Logs View]
        KIB -->|show| APM[APM View]
    end

    subgraph TRACKED["Metrics Captured"]
        MET -->|track| LAT[Latency p50/p95/p99]
        MET -->|track| RPS[Requests per sec]
        MET -->|track| ERR[Error Rate]
        MET -->|track| DDSL[DDS Latency]
        MET -->|track| MEM[Memory Usage]
        MET -->|track| GC[GC Collections]
    end
```

---

## 9. Stack Tecnologico

| Camada | Tecnologia | Finalidade |
|--------|------------|------------|
| Frontend | Next.js 16, TypeScript, Tailwind | Web UI |
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
| Webhooks | NestJS receiver | Event callbacks |

---

## 10. Portas dos Servicos

| Servico | Porta | Protocolo |
|---------|-------|-----------|
| PaymentGateway.Api | 5000 | HTTP |
| Next.js Frontend | 3000 | HTTP |
| Webhook Receiver | 4000 | HTTP |
| PostgreSQL | 5432 | TCP |
| MongoDB | 27017 | TCP |
| Redis | 30379 | TCP |
| Elasticsearch | 443 | HTTPS |
| MinIO | 443 | HTTPS |

---

## 11. Performance Targets

| Metrica | Target |
|---------|--------|
| RPS (Requests/sec) | 1,000,000+ |
| API Latency p99 | < 50ms |
| DDS Latency | < 5ms |
| Database Write | < 10ms |
| Cache Hit Rate | > 95% |
