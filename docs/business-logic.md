# Payment Gateway - Lógica de Negócio

> **Versão:** 2.0
> **Data:** 2026-03-19
> **Status:** Implementado

---

## 1. Diagrama de Classes - Domínio

```mermaid
classDiagram
    %% Aggregate Root
    class PaymentAggregate {
        <<aggregate>>
        +Guid Id
        +PaymentStatus Status
        +Money Amount
        +Currency Currency
        +string Description
        +DateTime CreatedAt
        +DateTime? ProcessedAt
        + FraudRiskLevel FraudScore
        +Create(command) PaymentCreatedEvent
        +Approve() PaymentApprovedEvent
        +Reject(reason) PaymentRejectedEvent
        +Refund() PaymentRefundedEvent
        +Capture() PaymentCapturedEvent
    }

    %% Entities
    class Payment {
        <<entity>>
        +Guid Id
        +Guid MerchantId
        +PaymentStatus Status
        +Money Amount
        +string Currency
        +PaymentMethod Method
        +string Description
        +CustomerInfo Customer
        +FraudCheckResult FraudResult
        +DateTime CreatedAt
        +DateTime? ApprovedAt
        +DateTime? SettledAt
    }

    class Merchant {
        <<entity>>
        +Guid Id
        +string Name
        +string ApiKey
        +MerchantStatus Status
        +MerchantCategory Category
        +decimal TransactionFee
        +DateTime CreatedAt
    }

    class Transaction {
        <<entity>>
        +Guid Id
        +Guid PaymentId
        +TransactionType Type
        +decimal Amount
        +TransactionStatus Status
        +string Reference
        +DateTime CreatedAt
    }

    class FraudCheck {
        <<value_object>>
        +decimal RiskScore
        +FraudDecision Decision
        +string Reason
        +DateTime CheckedAt
    }

    %% Value Objects
    class Money {
        <<value_object>>
        +decimal Amount
        +string Currency
        +Money Add(Money)
        +Money Subtract(Money)
        +bool IsGreaterThan(Money)
        +string Format()
    }

    class PaymentStatus {
        <<enum>>
        +Pending
        +AwaitingFraudCheck
        +Approved
        +Rejected
        +Captured
        +Refunded
        +Failed
    }

    class PaymentMethod {
        <<enum>>
        +CreditCard
        +DebitCard
        +PIX
        +Boleto
        +BankTransfer
    }

    class FraudDecision {
        <<enum>>
        +Approved
        +Review
        +Rejected
    }

    %% Relationships
    PaymentAggregate --> Payment
    PaymentAggregate --> FraudCheck
    PaymentAggregate --> Transaction
    Payment --> Merchant
    Money --* Payment
    PaymentStatus --* Payment
    PaymentMethod --* Payment
    FraudDecision --* FraudCheck
```

---

## 2. Eventos de Domínio (Event Sourcing)

```mermaid
classDiagram
    %% Domain Events
    class DomainEvent {
        <<abstract>>
        +Guid EventId
        +Guid AggregateId
        +DateTime OccurredAt
        +int Version
    }

    class PaymentCreatedEvent {
        +Guid PaymentId
        +Guid MerchantId
        +Money Amount
        +PaymentMethod Method
        +CustomerInfo Customer
    }

    class FraudCheckedEvent {
        +Guid PaymentId
        +decimal RiskScore
        +FraudDecision Decision
        +string Reason
    }

    class PaymentApprovedEvent {
        +Guid PaymentId
        +DateTime ApprovedAt
        +string TransactionId
    }

    class PaymentRejectedEvent {
        +Guid PaymentId
        +string Reason
        +DateTime RejectedAt
    }

    class PaymentCapturedEvent {
        +Guid PaymentId
        +string CaptureReference
        +DateTime CapturedAt
    }

    class PaymentRefundedEvent {
        +Guid PaymentId
        +decimal RefundAmount
        +string RefundReference
        +DateTime RefundedAt
    }

    class SettlementCompletedEvent {
        +Guid SettlementId
        +Guid MerchantId
        +decimal TotalAmount
        +int TransactionCount
        +DateTime CompletedAt
    }

    DomainEvent <|-- PaymentCreatedEvent
    DomainEvent <|-- FraudCheckedEvent
    DomainEvent <|-- PaymentApprovedEvent
    DomainEvent <|-- PaymentRejectedEvent
    DomainEvent <|-- PaymentCapturedEvent
    DomainEvent <|-- PaymentRefundedEvent
    DomainEvent <|-- SettlementCompletedEvent
```

---

## 3. Commands e Queries (CQRS)

```mermaid
classDiagram
    %% Commands
    class ICommand~TResult~ {
        <<interface>>
    }

    class CreatePaymentCommand {
        +Guid MerchantId
        +decimal Amount
        +string Currency
        +PaymentMethod Method
        +CustomerInfo Customer
        +string Description
    }

    class ApprovePaymentCommand {
        +Guid PaymentId
        +string ApproverId
    }

    class RejectPaymentCommand {
        +Guid PaymentId
        +string Reason
    }

    class RefundPaymentCommand {
        +Guid PaymentId
        +decimal? Amount
        +string Reason
    }

    class CapturePaymentCommand {
        +Guid PaymentId
    }

    class CheckFraudCommand {
        +Guid PaymentId
        +CustomerInfo Customer
        +Money Amount
    }

    %% Queries
    class IQuery~TResult~ {
        <<interface>>
    }

    class GetPaymentByIdQuery {
        +Guid PaymentId
    }

    class GetPaymentsByMerchantQuery {
        +Guid MerchantId
        +DateTime? From
        +DateTime? To
        +PaymentStatus? Status
    }

    class GetPaymentStatisticsQuery {
        +Guid MerchantId
        +DateTime From
        +DateTime To
    }

    ICommand <|-- CreatePaymentCommand
    ICommand <|-- ApprovePaymentCommand
    ICommand <|-- RejectPaymentCommand
    ICommand <|-- RefundPaymentCommand
    ICommand <|-- CapturePaymentCommand
    ICommand <|-- CheckFraudCommand

    IQuery <|-- GetPaymentByIdQuery
    IQuery <|-- GetPaymentsByMerchantQuery
    IQuery <|-- GetPaymentStatisticsQuery
```

---

## 4. Handlers

```mermaid
classDiagram
    %% Command Handlers
    class ICommandHandler~TCommand, TResult~ {
        <<interface>>
        +Handle(TCommand command)
    }

    class CreatePaymentHandler {
        +Handle(CreatePaymentCommand) CommandResult
    }

    class ApprovePaymentHandler {
        +Handle(ApprovePaymentCommand) CommandResult
    }

    class FraudCheckHandler {
        +Handle(CheckFraudCommand) FraudResult
    }

    class RefundPaymentHandler {
        +Handle(RefundPaymentCommand) CommandResult
    }

    %% Query Handlers
    class IQueryHandler~TQuery, TResult~ {
        <<interface>>
        +Handle(TQuery query)
    }

    class GetPaymentByIdHandler {
        +Handle(GetPaymentByIdQuery) PaymentDto
    }

    class GetPaymentsByMerchantHandler {
        +Handle(GetPaymentsByMerchantQuery) PaymentListDto
    }

    class GetStatisticsHandler {
        +Handle(GetPaymentStatisticsQuery) StatisticsDto
    }

    ICommandHandler <|-- CreatePaymentHandler
    ICommandHandler <|-- ApprovePaymentHandler
    ICommandHandler <|-- FraudCheckHandler
    ICommandHandler <|-- RefundPaymentHandler

    IQueryHandler <|-- GetPaymentByIdHandler
    IQueryHandler <|-- GetPaymentsByMerchantHandler
    IQueryHandler <|-- GetStatisticsHandler
```

---

## 5. Domain Services

```mermaid
classDiagram
    class PaymentDomainService {
        <<service>>
        +ValidatePayment(command) ValidationResult
        +CalculateFees(amount, merchant) Money
        +ApplyPaymentRules(payment, merchant) PaymentResult
    }

    class FraudAnalysisService {
        <<service>>
        +AnalyzeTransaction(payment, customer) FraudCheckResult
        +CalculateRiskScore(transaction) decimal
        +DetermineDecision(score, rules) FraudDecision
    }

    class SettlementService {
        <<service>>
        +CreateSettlementBatch(merchant, date) SettlementBatch
        +CalculateSettlementAmount(transactions) Money
        +ProcessReconciliation(batch) ReconciliationResult
    }

    class NotificationService {
        <<service>>
        +SendPaymentConfirmation(payment) NotificationResult
        +SendRefundNotification(payment) NotificationResult
        +SendSettlementNotification(settlement) NotificationResult
    }

    PaymentDomainService --> PaymentAggregate
    FraudAnalysisService --> FraudCheck
    SettlementService --> Transaction
    NotificationService --> Payment
```

---

## 6. Infrastructure - Persistência

```mermaid
classDiagram
    %% EF Core Contexts
    class PaymentDbContext {
        <<context>>
        +DbSet~Payment~ Payments
        +DbSet~Merchant~ Merchants
        +DbSet~Transaction~ Transactions
        +DbSet~Settlement~ Settlements
        +OnModelCreating(modelBuilder)
    }

    class MongoDbContext {
        <<context>>
        +IMongoCollection~PendingPayment~ PendingPayments
        +InsertPendingPayment(payment)
        +GetPendingPayments(count)
        +DeletePendingPayments(ids)
    }

    %% Entities - PostgreSQL
    class PaymentEntity {
        +Guid Id PK
        +Guid MerchantId FK
        +string Status
        +decimal Amount
        +string Currency
        +string Method
        +string CustomerEmail
        +string CustomerDocument
        +decimal? FraudScore
        +string? FraudDecision
        +DateTime CreatedAt
        +DateTime? ProcessedAt
    }

    class MerchantEntity {
        +Guid Id PK
        +string Name
        +string ApiKey
        +string Status
        +string Category
        +decimal TransactionFee
        +DateTime CreatedAt
    }

    %% Entities - MongoDB
    class PendingPaymentEntity {
        +Guid Id
        +Guid MerchantId
        +decimal Amount
        +string Currency
        +string Method
        +string Status
        +DateTime CreatedAt
        +bool Synced
    }

    PaymentDbContext --> PaymentEntity
    PaymentDbContext --> MerchantEntity
    MongoDbContext --> PendingPaymentEntity
    PaymentEntity --> MerchantEntity
```

---

## 7. Redis Cache

```mermaid
classDiagram
    class RedisCacheService {
        <<service>>
        +SetAsync(key, value, expiry)
        +GetAsync~T~(key)
        +DeleteAsync(key)
        +ExistsAsync(key)
    }

    class SessionManager {
        <<service>>
        +CreateSession(userId, claims)
        +GetSession(sessionId)
        +RefreshSession(sessionId)
        +InvalidateSession(sessionId)
    }

    class DistributedLock {
        <<service>>
        +AcquireLock(key, timeout)
        +ReleaseLock(key)
        +IsLocked(key)
    }

    class RateLimiter {
        <<service>>
        +CheckLimit(merchantId, operation)
        +GetRemainingRequests(merchantId)
        +ResetLimit(merchantId)
    }

    RedisCacheService --> SessionManager
    RedisCacheService --> DistributedLock
    RedisCacheService --> RateLimiter
```

---

## 8. DDS Topics - Contratos

```mermaid
classDiagram
    %% Command Contracts
    class CreatePaymentContract {
        <<dds_contract>>
        +Guid PaymentId
        +Guid MerchantId
        +decimal Amount
        +string Currency
        +string Method
        +string CustomerEmail
        +string CustomerDocument
        +string CustomerIp
    }

    class FraudCheckContract {
        <<dds_contract>>
        +Guid PaymentId
        +string CustomerEmail
        +string CustomerDocument
        +decimal Amount
        +string Currency
        +string CustomerIp
        +string MerchantCategory
    }

    class PaymentDecisionContract {
        <<dds_contract>>
        +Guid PaymentId
        +string Decision
        +decimal? RiskScore
        +string? Reason
    }

    %% Event Contracts
    class PaymentCreatedContract {
        <<dds_contract>>
        +Guid PaymentId
        +Guid MerchantId
        +decimal Amount
        +string Status
        +DateTime CreatedAt
    }

    class FraudCheckedContract {
        <<dds_contract>>
        +Guid PaymentId
        +decimal RiskScore
        +string Decision
        +string Reason
        +DateTime CheckedAt
    }

    class PaymentProcessedContract {
        <<dds_contract>>
        +Guid PaymentId
        +string Status
        +string? TransactionId
        +DateTime ProcessedAt
    }
```

---

## 9. Detecção de Fraude (AI)

```mermaid
classDiagram
    class IFraudDetectionService {
        <<interface>>
        +AnalyzeTransaction(request) FraudAnalysisResult
    }

    class OpenRouterFraudService {
        <<implementation>>
        -HttpClient _httpClient
        -string _apiKey
        -string _model
        +AnalyzeTransaction(request) FraudAnalysisResult
    }

    class FraudAnalysisRequest {
        +Guid PaymentId
        +decimal Amount
        +string Currency
        +CustomerInfo Customer
        +string MerchantCategory
        +DateTime Timestamp
    }

    class FraudAnalysisResult {
        +Guid PaymentId
        +decimal RiskScore
        +FraudDecision Decision
        +List~string~ Reasons
        +Dictionary~string, object~ Metadata
    }

    IFraudDetectionService <|-- OpenRouterFraudService
    OpenRouterFraudService --> FraudAnalysisRequest
    OpenRouterFraudService --> FraudAnalysisResult
```

---

## 10. Fluxo de Estados (State Machine)

```mermaid
stateDiagram-v2
    [*] --> Pending: Create Payment

    Pending --> AwaitingFraudCheck: Submit for Fraud Check
    AwaitingFraudCheck --> Approved: Fraud Approved
    AwaitingFraudCheck --> Rejected: Fraud Rejected
    AwaitingFraudCheck --> Review: Manual Review Required

    Approved --> Captured: Capture Payment
    Approved --> Refunded: Refund Request

    Captured --> Refunded: Refund Request
    Captured --> Settled: Settlement Complete

    Rejected --> [*]

    Review --> Approved: Manual Approve
    Review --> Rejected: Manual Reject

    Refunded --> [*]

    Settled --> [*]

    Failed --> [*]
    Pending --> Failed: Payment Failed
```

---

## 11. Regras de Negócio

### 11.1 Criação de Pagamento

```csharp
// Regras de negócio para criação
1. Merchant deve estar ativo
2. Amount deve ser > 0
3. Amount não deve exceder limite do merchant
4. Currency deve ser suportada
5. Customer deve ter email válido
6. Método de pagamento deve estar disponível para o merchant
```

### 11.2 Análise de Fraude

```csharp
// Regras de scoring de fraude
- Score < 30: Aprovado automaticamente
- Score 30-70: Revisão manual
- Score > 70: Rejeitado automaticamente
- Score > 50 com amount > 10000: Revisão manual obrigatória
```

### 11.3 Taxas e Comissão

```csharp
// Cálculo de taxas
Fee = TransactionAmount * Merchant.FeePercentage
NetAmount = TransactionAmount - Fee
SettlementAmount = Sum(NetAmount) - SettlementFee
```

---

## 12. DTOs

```mermaid
classDiagram
    class PaymentDto {
        +Guid Id
        +Guid MerchantId
        +string MerchantName
        +decimal Amount
        +string Currency
        +string Status
        +string Method
        +string CustomerEmail
        +decimal? FraudScore
        +DateTime CreatedAt
        +DateTime? ProcessedAt
    }

    class CreatePaymentRequest {
        +decimal Amount
        +string Currency
        +string Method
        +string CustomerEmail
        +string CustomerName
        +string? CustomerDocument
        +string? Description
    }

    class PaymentResponse {
        +Guid PaymentId
        +string Status
        +DateTime CreatedAt
        +string? RedirectUrl
    }

    class StatisticsDto {
        +int TotalTransactions
        +decimal TotalAmount
        +decimal AverageAmount
        +int ApprovedCount
        +int RejectedCount
        +int RefundedCount
    }
```
