# Payment Gateway - CQRS e Fluxo de Eventos

> **Versão:** 2.0
> **Data:** 2026-03-19
> **Status:** Implementado

---

## 1. Fluxo de Criação de Pagamento

```mermaid
sequenceDiagram
    participant Client as Client
    participant API as Minimal API
    participant Validator as Input Validator
    participant MongoDB as MongoDB
    participant DDS as CycloneDDS
    participant Fraud as FraudDetector
    participant Payment as PaymentProcessor
    participant Redis as Redis Cache
    participant ELK as Elasticsearch

    Note over Client,ELK: CREATE PAYMENT FLOW

    Client->>API: POST /payments<br/>{amount, currency, method, customer}

    API->>Validator: Validate Request<br/>Schema + Business Rules
    alt Validation Failed
        Validator-->>Client: 400 Bad Request
    end

    Validator->>API: Valid Request

    API->>MongoDB: Insert PendingPayment<br/>(write buffer)
    MongoDB-->>API: Inserted

    API->>Redis: Cache PaymentId<br/>(TTL: 1h)
    Redis-->>API: Cached

    API->>DDS: Publish payment.create<br/>CreatePaymentCommand
    DDS-->>API: Published

    API->>ELK: Log Request<br/>(OTel)
    ELK-->>API: Logged

    API-->>Client: 202 Accepted<br/>{paymentId, status: pending}

    Note over DDS,Payment: ASYNC PROCESSING

    DDS->>Payment: Subscribe payment.create
    Payment->>Payment: Validate Merchant
    Payment->>Payment: Create Payment Aggregate
    Payment->>DDS: Publish payment.created

    DDS->>Fraud: Subscribe payment.created
    Fraud->>Fraud: Analyze Transaction<br/>(AI)
    Fraud->>DDS: Publish fraud.checked

    DDS->>Payment: Subscribe fraud.checked
    alt Fraud Approved
        Payment->>DDS: Publish payment.approved
    else Fraud Rejected
        Payment->>DDS: Publish payment.rejected
    end

    Note over Client,ELK: RESULT NOTIFICATION

    DDS->>API: Subscribe events
    API->>Redis: Update Cache<br/>status = approved/rejected
    API->>Client: WebSocket<br/>payment.{id} updated

    API->>ELK: Trace Complete<br/>payment_id, duration, status
```

---

## 2. Fluxo de Aprovação de Pagamento

```mermaid
sequenceDiagram
    participant API as Minimal API
    participant DDS as CycloneDDS
    participant Payment as PaymentProcessor
    participant MongoDB as MongoDB
    participant PG as PostgreSQL
    participant Notif as NotificationService

    Note over API,Notif: APPROVE PAYMENT FLOW

    API->>API: Validate Payment exists

    API->>DDS: Publish payment.approve<br/>ApprovePaymentCommand

    DDS->>Payment: Subscribe payment.approve
    Payment->>Payment: Validate Payment Status<br/>(must be AwaitingApproval)
    Payment->>Payment: Update Aggregate<br/>Status = Approved
    Payment->>Payment: Generate TransactionId

    Payment->>DDS: Publish payment.approved<br/>PaymentApprovedEvent

    Note over Payment,PG: PERSIST STATE

    Payment->>MongoDB: Update PendingPayment<br/>status = approved
    Payment->>PG: Update Payment<br/>status = approved, transactionId

    Note over Payment,Notif: NOTIFICATION

    Payment->>Notif: Publish notification.send<br/>PaymentApproved

    Notif->>Notif: Send Email
    Notif->>Notif: Send Webhook
    Notif->>DDS: Publish notification.sent
```

---

## 3. Fluxo de Verificação de Fraude

```mermaid
sequenceDiagram
    participant Payment as PaymentProcessor
    participant DDS as CycloneDDS
    participant Fraud as FraudDetector
    participant AI as OpenRouter<br/>MiniMax M2.5
    participant Redis as Redis Cache

    Note over Payment,Redis: FRAUD CHECK FLOW

    Payment->>DDS: Publish fraud.check<br/>FraudCheckCommand

    DDS->>Fraud: Subscribe fraud.check
    Fraud->>Redis: Check Merchant<br/>fraud config
    Redis-->>Fraud: Config

    Fraud->>AI: POST /v1/chat/completions<br/>Analyze transaction
    rect rgb(240, 248, 255)
        Note over AI,Fraud: FRAUD ANALYSIS PROMPT
        AI<<--System: You are a fraud detection analyst...
        AI<<--User: Analyze this transaction:
        AI<<--User: - Amount: $500
        AI<<--User: - Customer: john@email.com
        AI<<--User: - IP: 192.168.1.1
        AI<<--User: - Merchant: online_store
    end

    AI-->>Fraud: Response<br/>{score: 25, decision: approved, reasons: []}

    alt Risk Score < 30
        Fraud->>DDS: Publish fraud.checked<br/>Decision: Approved
    else Risk Score 30-70
        Fraud->>DDS: Publish fraud.checked<br/>Decision: Review
    else Risk Score > 70
        Fraud->>DDS: Publish fraud.checked<br/>Decision: Rejected
    end

    Fraud->>Redis: Cache fraud result<br/>(merchant + customer)
```

---

## 4. Fluxo de Reembolso

```mermaid
sequenceDiagram
    participant Client as Client
    participant API as Minimal API
    participant DDS as CycloneDDS
    participant Payment as PaymentProcessor
    participant PG as PostgreSQL
    participant Notif as NotificationService

    Note over Client,Notif: REFUND FLOW

    Client->>API: POST /payments/{id}/refund<br/>{amount?, reason}

    API->>API: Validate Payment<br/>exists, can refund

    API->>DDS: Publish payment.refund<br/>RefundPaymentCommand

    DDS->>Payment: Subscribe payment.refund

    alt Full Refund
        Payment->>Payment: Validate Payment<br/>is captured
    else Partial Refund
        Payment->>Payment: Validate Amount<br/><= remaining
    end

    Payment->>Payment: Update Aggregate<br/>Status = Refunded
    Payment->>Payment: Generate RefundId

    Payment->>DDS: Publish payment.refunded<br/>PaymentRefundedEvent

    Payment->>PG: Update Payment<br/>status = refunded
    Payment->>PG: Insert Refund<br/>refund record

    Payment->>Notif: Publish notification.send<br/>RefundConfirmation

    Notif->>Client: Send Email<br/>Refund confirmation
```

---

## 5. Fluxo de Sync MongoDB → PostgreSQL

```mermaid
sequenceDiagram
    participant Quartz as Quartz.NET<br/>Scheduler
    participant Job as MongoSyncService
    participant Mongo as MongoDB
    participant PG as PostgreSQL
    participant ELK as Elasticsearch

    Note over Quartz,ELK: BULK SYNC FLOW (Every 2 min)

    Quartz->>Job: Trigger SyncJob

    Job->>Mongo: Find PendingPayments<br/>{synced: false, limit: 10000}
    Mongo-->>Job: Pending Payments (batch)

    loop Process Batch
        Job->>Job: Transform to PaymentEntity

        Job->>PG: Bulk Insert<br/>Payments Table
        PG-->>Job: Inserted

        Job->>PG: Bulk Insert<br/>Transactions Table
        PG-->>Job: Inserted
    end

    Job->>Mongo: Update Synced<br/>{paymentId: synced: true}

    Job->>ELK: Log Sync Metrics<br/>{count, duration, errors}

    alt Errors > 0
        Job->>Job: Send Alert<br/>{sync failures}
    end
```

---

## 6. Fluxo de Liquidação (Settlement)

```mermaid
sequenceDiagram
    participant Quartz as Quartz.NET<br/>Daily
    participant Settle as SettlementService
    participant PG as PostgreSQL
    participant Merchant as Merchant
    participant Notif as NotificationService

    Note over Quartz,Notif: SETTLEMENT DAILY FLOW

    Quartz->>Settle: Trigger DailySettlement

    Settle->>PG: Query Approved Payments<br/>{date: yesterday, settled: false}
    PG-->>Settle: Transactions

    loop Per Merchant
        Settle->>Settle: Calculate Fees<br/>amount * merchant.fee

        Settle->>Settle: Calculate Net<br/>amount - fees

        Settle->>PG: Insert Settlement<br/>{merchantId, date, amount, fees}

        Settle->>PG: Update Payments<br/>settled: true
    end

    Settle->>PG: Insert SettlementBatch

    Settle->>Notif: Publish settlement.processed<br/>MerchantSettlement

    Notif->>Merchant: Send Settlement Report<br/>{amount, fees, transactions}

    Merchant->>Merchant: Process Payment<br/>(external gateway)
```

---

## 7. Consulta de Pagamento (Read)

```mermaid
sequenceDiagram
    participant Client as Client
    participant API as Minimal API
    participant Redis as Redis Cache
    participant PG as PostgreSQL

    Note over Client,PG: QUERY PAYMENT FLOW

    Client->>API: GET /payments/{id}

    API->>Redis: Get from Cache<br/>payment:{id}
    alt Cache Hit
        Redis-->>API: Cached Data
        API-->>Client: 200 OK (cached)
    else Cache Miss
        API->>PG: Query Payment<br/>SELECT * FROM Payments WHERE Id = {id}
        PG-->>API: Payment Data

        alt Payment Found
            API->>Redis: Cache Payment<br/>TTL: 5 min
            API-->>Client: 200 OK
        else Not Found
            API-->>Client: 404 Not Found
        end
    end
```

---

## 8. Fluxo de Autenticação

```mermaid
sequenceDiagram
    participant User as User
    participant Frontend as Next.js
    participant Authentik as Authentik OIDC
    participant API as Minimal API
    participant Redis as Redis

    Note over User,Redis: AUTHENTICATION FLOW

    User->>Frontend: Access /dashboard

    Frontend->>Authentik: Redirect /authorize<br/>client_id, redirect_uri, scope

    Authentik-->>User: Login Page

    User->>Authentik: Submit Credentials<br/>email, password

    alt Authentication Success
        Authentik-->>Frontend: Redirect /callback<br/>code: {auth_code}

        Frontend->>Authentik: POST /token<br/>code: {auth_code}

        Authentik-->>Frontend: Response<br/>{access_token, id_token, refresh_token}

        Frontend->>Frontend: Store Tokens<br/>HttpOnly Cookies

        Frontend-->>User: Redirect /dashboard
    else Authentication Failed
        Authentik-->>User: Error Page
    end

    Note over API,Redis: API AUTHENTICATION

    Frontend->>API: GET /api/payments<br/>Authorization: Bearer {token}

    API->>API: Validate JWT<br/>signature, expiration, claims

    alt Token Valid
        API->>Redis: Check Session<br/>session:{userId}
        Redis-->>API: Session Valid

        API-->>Frontend: 200 OK
    else Token Invalid/Expired
        API-->>Frontend: 401 Unauthorized
    end
```

---

## 9. Event Sourcing - Stored Events

```mermaid
sequenceDiagram
    participant Aggregate as Payment Aggregate
    participant EventStore as Event Store (DDS)
    participant History as Transaction History Service
    participant PG as PostgreSQL

    Note over Aggregate,PG: EVENT SOURCING FLOW

    Aggregate->>Aggregate: Apply Command<br/>approve()

    Aggregate->>Aggregate: Validate Business Rules

    Aggregate->>Aggregate: Create Event<br/>PaymentApprovedEvent

    Aggregate->>EventStore: Publish Event<br/>payment.approved

    EventStore-->>Aggregate: Persisted

    Note over History,PG: EVENT STORAGE

    EventStore->>History: Subscribe payment.* (all events)

    History->>PG: Insert Event<br/>EventStore Table
    PG-->>History: Stored

    History->>PG: Update Current State<br/>Payments Table

    Note over Aggregate,PG: REPLAY (Optional)

    PG->>Aggregate: Replay Events<br/>reconstruct aggregate from events
```

---

## 10. Fluxo Completo - happy path

```mermaid
sequenceDiagram
    participant C as Client
    participant API as API
    participant M as MongoDB
    participant DDS as DDS
    participant F as Fraud
    participant P as Payment
    participant PG as PostgreSQL
    participant N as Notif

    C->>API: POST /payments
    API->>M: Insert
    M->>API: OK
    API->>DDS: publish create
    API->>C: 202 {id}

    DDS->>P: create
    P->>DDS: created

    DDS->>F: check
    F->>AI: analyze
    AI-->>F: score: 15
    F->>DDS: checked (approved)

    DDS->>P: checked
    P->>DDS: approved

    DDS->>N: notify
    N->>C: email

    P->>PG: sync (later)
    M->>PG: sync (later)
```

---

## 11. Tabela de Eventos

| Evento | Producer | Consumer | Payload |
|--------|----------|----------|---------|
| `payment.create` | API | PaymentProcessor | CreatePaymentCommand |
| `payment.created` | PaymentProcessor | FraudDetector, History | PaymentCreatedEvent |
| `fraud.check` | PaymentProcessor | FraudDetector | FraudCheckCommand |
| `fraud.checked` | FraudDetector | PaymentProcessor | FraudCheckedEvent |
| `payment.approved` | PaymentProcessor | NotificationService, History | PaymentApprovedEvent |
| `payment.rejected` | PaymentProcessor | NotificationService, History | PaymentRejectedEvent |
| `payment.capture` | API | PaymentProcessor | CaptureCommand |
| `payment.captured` | PaymentProcessor | SettlementService | PaymentCapturedEvent |
| `payment.refund` | API | PaymentProcessor | RefundCommand |
| `payment.refunded` | PaymentProcessor | NotificationService, History | PaymentRefundedEvent |
| `settlement.process` | Quartz | SettlementService | SettlementCommand |
| `notification.send` | Multiple | NotificationService | NotificationCommand |
