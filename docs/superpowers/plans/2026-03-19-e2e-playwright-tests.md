# E2E Playwright Tests - Implementation Plan

> **For Claude:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a comprehensive Playwright E2E test suite that covers 100% of the PaymentGateway flow — from API calls through DDS processing to database verification and webhook delivery.

**Architecture:** Standalone TypeScript Playwright project in `tests/e2e/`. Tests call the API directly via `request` context, verify state in MongoDB and PostgreSQL via native drivers, check webhook delivery via the webhook-receiver REST API, and validate the Next.js frontend via browser automation.

**Tech Stack:** Playwright, TypeScript, node-postgres (pg), mongodb driver, dotenv

---

## File Structure

```
tests/e2e/
├── package.json                    # Dependencies: playwright, pg, mongodb, dotenv
├── tsconfig.json                   # TypeScript config
├── playwright.config.ts            # Playwright config (baseURL, timeouts, projects)
├── .env                            # Connection strings (gitignored)
├── .env.example                    # Template
├── helpers/
│   ├── api-client.ts               # Typed API client wrapping Playwright request
│   ├── db-mongo.ts                 # MongoDB helper (connect, query, cleanup)
│   ├── db-postgres.ts              # PostgreSQL helper (connect, query, cleanup)
│   ├── webhook-client.ts           # Webhook receiver query helper
│   ├── wait-utils.ts               # Polling/retry utilities for async DDS flow
│   └── fixtures.ts                 # Test data factories (merchant, payment, customer)
├── specs/
│   ├── phase1-health.spec.ts       # Health check and basic connectivity
│   ├── phase2-payments-crud.spec.ts        # Payment create, get, list
│   ├── phase3-payment-lifecycle.spec.ts    # Full lifecycle: create → fraud → approve/reject
│   ├── phase4-capture-refund.spec.ts       # Capture and refund flows
│   ├── phase5-webhooks.spec.ts             # Webhook registration + delivery verification
│   ├── phase6-merchants.spec.ts            # Merchant endpoints
│   ├── phase7-transactions-stats.spec.ts   # Transactions list + statistics
│   ├── phase8-error-cases.spec.ts          # Auth, validation, idempotency errors
│   ├── phase9-db-verification.spec.ts      # Cross-database consistency checks
│   └── phase10-frontend.spec.ts            # Browser tests for Next.js UI
└── global-setup.ts                 # Verify services are running before tests
```

---

## Chunk 1: Project Setup (Phase 1)

### Task 1: Initialize Playwright project

**Files:**
- Create: `tests/e2e/package.json`
- Create: `tests/e2e/tsconfig.json`
- Create: `tests/e2e/playwright.config.ts`
- Create: `tests/e2e/.env.example`
- Create: `tests/e2e/.env`

- [ ] **Step 1: Create package.json**

```json
{
  "name": "paymentgateway-e2e",
  "private": true,
  "scripts": {
    "test": "npx playwright test",
    "test:api": "npx playwright test --project=api",
    "test:ui": "npx playwright test --project=ui",
    "test:phase1": "npx playwright test specs/phase1",
    "test:phase2": "npx playwright test specs/phase2",
    "test:phase3": "npx playwright test specs/phase3",
    "test:phase4": "npx playwright test specs/phase4",
    "test:phase5": "npx playwright test specs/phase5",
    "test:phase6": "npx playwright test specs/phase6",
    "test:phase7": "npx playwright test specs/phase7",
    "test:phase8": "npx playwright test specs/phase8",
    "test:phase9": "npx playwright test specs/phase9",
    "test:phase10": "npx playwright test specs/phase10",
    "report": "npx playwright show-report"
  },
  "devDependencies": {
    "@playwright/test": "^1.50.0",
    "pg": "^8.13.0",
    "@types/pg": "^8.11.0",
    "mongodb": "^6.12.0",
    "dotenv": "^16.4.0"
  }
}
```

- [ ] **Step 2: Create tsconfig.json**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "commonjs",
    "strict": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "outDir": "./dist",
    "rootDir": ".",
    "resolveJsonModule": true
  },
  "include": ["**/*.ts"]
}
```

- [ ] **Step 3: Create playwright.config.ts**

```typescript
import { defineConfig } from '@playwright/test';
import dotenv from 'dotenv';
import path from 'path';

dotenv.config({ path: path.resolve(__dirname, '.env') });

export default defineConfig({
  testDir: './specs',
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: [['html', { open: 'never' }], ['list']],
  globalSetup: './global-setup.ts',
  use: {
    baseURL: process.env.API_BASE_URL || 'http://localhost:5000',
    extraHTTPHeaders: {
      'Content-Type': 'application/json',
      'X-API-Key': process.env.API_KEY || 'sk_test_smoke_merchant',
    },
  },
  projects: [
    {
      name: 'api',
      testMatch: /phase[1-9].*\.spec\.ts/,
    },
    {
      name: 'ui',
      testMatch: /phase10.*\.spec\.ts/,
      use: {
        baseURL: process.env.WEB_BASE_URL || 'http://localhost:3000',
        headless: true,
      },
    },
  ],
});
```

- [ ] **Step 4: Create .env.example and .env**

`.env.example`:
```
API_BASE_URL=http://localhost:5000
WEB_BASE_URL=http://localhost:3000
WEBHOOK_RECEIVER_URL=http://localhost:4000
API_KEY=sk_test_smoke_merchant
MERCHANT_ID=11111111-1111-1111-1111-111111111111

POSTGRES_HOST=spsql.home.arpa
POSTGRES_PORT=5432
POSTGRES_DB=demo-gateway
POSTGRES_USER=app
POSTGRES_PASSWORD=Admin@123

MONGO_URL=mongodb://admin:Admin%40123@mongodb.home.arpa:27017/?authSource=admin
MONGO_DB=demo-gateway
```

`.env` same content with real values.

- [ ] **Step 5: Install dependencies**

Run: `cd tests/e2e && npm install`

- [ ] **Step 6: Commit**

```bash
git add tests/e2e/package.json tests/e2e/tsconfig.json tests/e2e/playwright.config.ts tests/e2e/.env.example
git commit -m "feat(e2e): initialize Playwright project with config"
```

---

### Task 2: Create helpers

**Files:**
- Create: `tests/e2e/helpers/api-client.ts`
- Create: `tests/e2e/helpers/db-mongo.ts`
- Create: `tests/e2e/helpers/db-postgres.ts`
- Create: `tests/e2e/helpers/webhook-client.ts`
- Create: `tests/e2e/helpers/wait-utils.ts`
- Create: `tests/e2e/helpers/fixtures.ts`
- Create: `tests/e2e/global-setup.ts`

- [ ] **Step 1: Create api-client.ts**

Typed wrapper around Playwright APIRequestContext for all endpoints:
- `createPayment(data)` → POST /api/v1/payments
- `getPayment(id)` → GET /api/v1/payments/{id}
- `listPayments(params?)` → GET /api/v1/payments
- `refundPayment(id, data?)` → POST /api/v1/payments/{id}/refund
- `capturePayment(id)` → POST /api/v1/payments/{id}/capture
- `cancelPayment(id)` → POST /api/v1/payments/{id}/cancel
- `reprocessPayment(id)` → POST /api/v1/payments/{id}/reprocess
- `getMerchant(id)` → GET /api/v1/merchants/{id}
- `updateMerchant(id, data)` → PUT /api/v1/merchants/{id}
- `getMerchantPayments(id, params?)` → GET /api/v1/merchants/{id}/payments
- `createWebhook(data)` → PUT /api/v1/webhooks
- `listWebhooks(merchantId)` → GET /api/v1/webhooks?merchantId=
- `deleteWebhook(id)` → DELETE /api/v1/webhooks/{id}
- `getTransactions(params?)` → GET /api/v1/transactions
- `getStatistics(params?)` → GET /api/v1/statistics
- `healthCheck()` → GET /health

- [ ] **Step 2: Create db-mongo.ts**

```typescript
// connect(), disconnect()
// findPayment(id) → pending_payments collection
// findWebhooks(merchantId) → webhooks collection
// findEvents(paymentId) → transaction_events collection
// countPayments(filter?) → count documents
// cleanup(paymentIds) → delete test data
```

- [ ] **Step 3: Create db-postgres.ts**

```typescript
// connect(), disconnect()
// findPayment(id) → SELECT * FROM "Payments" WHERE "Id" = $1
// findTransactions(paymentId) → SELECT * FROM "Transactions" WHERE "PaymentId" = $1
// findMerchant(id) → SELECT * FROM "Merchants" WHERE "Id" = $1
// countPayments(merchantId) → SELECT COUNT(*) ...
// cleanup(paymentIds) → DELETE test records
```

- [ ] **Step 4: Create webhook-client.ts**

```typescript
// getAllEvents() → GET /webhooks/events
// getEventsByType(type) → GET /webhooks/events/{type}
// healthCheck() → GET /health
// waitForEvent(paymentId, eventType, timeoutMs) → poll until found
```

- [ ] **Step 5: Create wait-utils.ts**

```typescript
// waitForCondition(fn, options) → poll fn() until truthy, with timeout and interval
// waitForPaymentStatus(apiClient, paymentId, status, timeoutMs)
// waitForWebhookEvent(webhookClient, paymentId, eventType, timeoutMs)
// sleep(ms)
```

- [ ] **Step 6: Create fixtures.ts**

```typescript
// createPaymentData(overrides?) → valid CreatePaymentCommand with random data
// randomEmail() → unique test email
// randomDocument() → valid CPF-like document
// MERCHANT_ID → from env
// API_KEY → from env
```

- [ ] **Step 7: Create global-setup.ts**

```typescript
// Verify API is reachable (GET /health)
// Verify webhook-receiver is reachable (GET /health)
// Verify PostgreSQL connection
// Verify MongoDB connection
// Throw if any service is down
```

- [ ] **Step 8: Commit**

```bash
git add tests/e2e/helpers/ tests/e2e/global-setup.ts
git commit -m "feat(e2e): add helpers for API, DB, webhooks, and test fixtures"
```

---

## Chunk 2: API Tests (Phases 1-4)

### Task 3: Phase 1 - Health Check

**Files:**
- Create: `tests/e2e/specs/phase1-health.spec.ts`

- [ ] **Step 1: Write tests**

```typescript
// test('API health check returns 200')
// test('Webhook receiver health check returns 200')
// test('MongoDB is reachable')
// test('PostgreSQL is reachable')
```

- [ ] **Step 2: Run and verify**

Run: `cd tests/e2e && npx playwright test specs/phase1 --project=api`
Expected: 4 tests pass

- [ ] **Step 3: Commit**

---

### Task 4: Phase 2 - Payments CRUD

**Files:**
- Create: `tests/e2e/specs/phase2-payments-crud.spec.ts`

- [ ] **Step 1: Write tests**

```typescript
// test('POST /payments creates payment with 202 and returns paymentId')
// test('GET /payments/{id} returns payment details')
// test('GET /payments lists payments with pagination')
// test('GET /payments filters by status')
// test('GET /payments filters by merchantId')
// test('POST /payments with idempotencyKey returns same paymentId on retry')
```

- [ ] **Step 2: Run and verify**

Run: `npx playwright test specs/phase2 --project=api`
Expected: 6 tests pass

- [ ] **Step 3: Commit**

---

### Task 5: Phase 3 - Payment Lifecycle (DDS async flow)

**Files:**
- Create: `tests/e2e/specs/phase3-payment-lifecycle.spec.ts`

This is the critical test — verifies the full async DDS flow.

- [ ] **Step 1: Write tests**

```typescript
// test('Payment progresses from pending to approved/rejected')
//   1. POST /payments → get paymentId
//   2. Poll GET /payments/{id} until status != 'pending' (timeout 30s)
//   3. Assert status is 'approved' or 'rejected'
//   4. Assert fraudScore is a number 0-100
//   5. Assert fraudDecision is 'approved' or 'rejected'
//   6. Assert processedAt is set

// test('Payment data appears in MongoDB pending_payments')
//   1. Create payment
//   2. Query MongoDB pending_payments by ID
//   3. Assert document exists with correct amount, currency, merchantId

// test('Payment fraud analysis produces valid score')
//   1. Create payment with known good data (low amount, valid customer)
//   2. Wait for processing
//   3. Assert fraudScore <= 60 (should be low risk)

// test('Multiple payments process independently')
//   1. Create 3 payments simultaneously
//   2. Wait for all to be processed
//   3. Assert each has independent status and fraudScore
```

- [ ] **Step 2: Run and verify**

Run: `npx playwright test specs/phase3 --project=api`
Expected: 4 tests pass (may take 30-60s due to DDS processing)

- [ ] **Step 3: Commit**

---

### Task 6: Phase 4 - Capture and Refund

**Files:**
- Create: `tests/e2e/specs/phase4-capture-refund.spec.ts`

- [ ] **Step 1: Write tests**

```typescript
// test('Capture approved payment changes status to captured')
//   1. Create payment, wait for approved
//   2. POST /payments/{id}/capture
//   3. Assert status is 'captured'
//   4. Assert capturedAt is set

// test('Refund captured payment changes status to refunded')
//   1. Create payment, wait for approved, capture
//   2. POST /payments/{id}/refund with reason
//   3. Assert status is 'refunded'
//   4. Assert refundedAt is set
//   5. Assert refundedAmount matches

// test('Cancel pending payment changes status to cancelled')
//   1. Create payment (don't wait for processing)
//   2. POST /payments/{id}/cancel immediately
//   3. Assert status is 'cancelled'

// test('Cannot refund a rejected payment')
//   1. Find or create a rejected payment
//   2. POST /payments/{id}/refund
//   3. Assert 400 Bad Request

// test('Cannot capture a rejected payment')
//   1. Find or create a rejected payment
//   2. POST /payments/{id}/capture
//   3. Assert 400 Bad Request
```

- [ ] **Step 2: Run and verify**

Run: `npx playwright test specs/phase4 --project=api`

- [ ] **Step 3: Commit**

---

## Chunk 3: Webhooks, Merchants, Transactions (Phases 5-7)

### Task 7: Phase 5 - Webhooks

**Files:**
- Create: `tests/e2e/specs/phase5-webhooks.spec.ts`

- [ ] **Step 1: Write tests**

```typescript
// test('Register webhook for merchant')
//   1. PUT /api/v1/webhooks with url, events, secret
//   2. Assert 200 with webhookId
//   3. Verify in MongoDB webhooks collection

// test('List webhooks for merchant')
//   1. GET /api/v1/webhooks?merchantId=...
//   2. Assert returns registered webhooks

// test('Webhook fires on payment.approved')
//   1. Register webhook for payment.approved pointing to webhook-receiver:4000
//   2. Create and wait for payment to be approved
//   3. Poll webhook-receiver GET /webhooks/events/payment.approved
//   4. Assert event received with matching paymentId
//   5. Assert signatureValid is true (HMAC-SHA256)

// test('Webhook fires on payment.rejected')
//   Similar to above but with payment.rejected event

// test('Delete webhook')
//   1. DELETE /api/v1/webhooks/{id}
//   2. Assert 204
//   3. Verify removed from MongoDB

// test('Webhook HMAC signature is valid')
//   1. Check received webhook event
//   2. Assert signatureValid === true
```

- [ ] **Step 2: Run and verify**

Run: `npx playwright test specs/phase5 --project=api`

- [ ] **Step 3: Commit**

---

### Task 8: Phase 6 - Merchants

**Files:**
- Create: `tests/e2e/specs/phase6-merchants.spec.ts`

- [ ] **Step 1: Write tests**

```typescript
// test('GET merchant by ID returns merchant details')
// test('GET merchant payments returns paginated list')
// test('Merchant scope enforcement - cannot access other merchant data')
//   (if using different API keys / merchant IDs)
```

- [ ] **Step 2: Run and verify, commit**

---

### Task 9: Phase 7 - Transactions and Statistics

**Files:**
- Create: `tests/e2e/specs/phase7-transactions-stats.spec.ts`

- [ ] **Step 1: Write tests**

```typescript
// test('GET transactions for a payment returns authorization record')
//   1. Create and process a payment
//   2. GET /api/v1/transactions?paymentId={id}
//   3. Assert at least one transaction with type="authorization"

// test('GET transactions after capture includes capture record')

// test('GET statistics returns aggregated data')
//   1. GET /api/v1/statistics?merchantId=...
//   2. Assert totalTransactions > 0
//   3. Assert byStatus has entries
//   4. Assert byMethod has entries

// test('Statistics filter by date range')
```

- [ ] **Step 2: Run and verify, commit**

---

## Chunk 4: Error Cases and DB Verification (Phases 8-9)

### Task 10: Phase 8 - Error Cases

**Files:**
- Create: `tests/e2e/specs/phase8-error-cases.spec.ts`

- [ ] **Step 1: Write tests**

```typescript
// test('Request without API key returns 401')
// test('Request with invalid API key returns 401')
// test('GET payment with non-existent ID returns 404')
// test('POST payment with missing required fields returns 400')
// test('POST payment with negative amount returns 400')
// test('POST payment with invalid currency returns 400')
// test('Idempotency: same key returns same paymentId, not duplicate')
// test('POST payment with empty merchantId returns 400')
```

- [ ] **Step 2: Run and verify, commit**

---

### Task 11: Phase 9 - Cross-Database Verification

**Files:**
- Create: `tests/e2e/specs/phase9-db-verification.spec.ts`

- [ ] **Step 1: Write tests**

```typescript
// test('Payment exists in MongoDB after creation')
//   1. Create payment via API
//   2. Query MongoDB pending_payments
//   3. Assert document exists with matching fields

// test('Payment syncs to PostgreSQL after processing')
//   1. Create payment, wait for approved
//   2. Wait up to 150s for MongoSync (runs every 2 min)
//   3. Query PostgreSQL Payments table
//   4. Assert row exists with correct status, amount, fraudScore

// test('Transaction records exist in PostgreSQL')
//   1. After payment approved + captured
//   2. Query PostgreSQL Transactions table
//   3. Assert authorization and capture records exist

// test('Webhook config persists in MongoDB')
//   1. Create webhook via API
//   2. Query MongoDB webhooks collection
//   3. Assert document matches (url, events, secret, isActive)

// test('MongoDB and PostgreSQL data are consistent')
//   1. Create payment, wait for full processing + sync
//   2. Compare MongoDB document vs PostgreSQL row
//   3. Assert amount, currency, status, merchantId match
```

- [ ] **Step 2: Run and verify, commit**

---

## Chunk 5: Frontend Browser Tests (Phase 10)

### Task 12: Phase 10 - Frontend Tests

**Files:**
- Create: `tests/e2e/specs/phase10-frontend.spec.ts`

- [ ] **Step 1: Write tests**

```typescript
// test('Dashboard loads and shows statistics')
//   1. Navigate to /
//   2. Assert page title contains "Payment Gateway" or "Dashboard"
//   3. Assert stat cards are visible (total, approved, rejected, amount)

// test('Payments list page shows payments')
//   1. Configure API access in settings first (merchantId + apiKey)
//   2. Navigate to /payments
//   3. Assert table or payment cards are visible
//   4. Assert at least one payment row

// test('Create payment form submits successfully')
//   1. Navigate to /payments/new
//   2. Fill form: amount, currency, method, customer info
//   3. Submit
//   4. Assert success message or redirect to payment details

// test('Payment detail page shows fraud score')
//   1. Navigate to /payments/{known-payment-id}
//   2. Assert fraud score is displayed
//   3. Assert status badge is visible

// test('Analytics page loads with charts')
//   1. Navigate to /analytics
//   2. Assert chart containers are visible
//   3. Assert period filter buttons work

// test('Webhooks page allows adding webhook')
//   1. Navigate to /webhooks
//   2. Click "Add Webhook"
//   3. Fill URL and select events
//   4. Submit
//   5. Assert webhook appears in list

// test('Settings page allows configuring API access')
//   1. Navigate to /settings
//   2. Enter merchant ID and API key
//   3. Save
//   4. Assert values are persisted (reload page, check fields)

// test('Transactions page shows transaction history')
//   1. Navigate to /transactions
//   2. Assert table is visible with type, amount, status columns
```

- [ ] **Step 2: Run and verify**

Run: `npx playwright test specs/phase10 --project=ui`

- [ ] **Step 3: Commit**

---

## Chunk 6: Full E2E Lifecycle Test

### Task 13: Master lifecycle test (ties everything together)

**Files:**
- Create: `tests/e2e/specs/phase3-payment-lifecycle.spec.ts` (add to existing)

- [ ] **Step 1: Add comprehensive lifecycle test**

```typescript
// test('FULL LIFECYCLE: create → fraud → approve → capture → refund → verify all')
//   1. Register webhook pointing to receiver:4000
//   2. POST /payments → get paymentId
//   3. Verify MongoDB: pending_payments has document
//   4. Wait for status != pending (DDS processing, up to 30s)
//   5. Assert status is approved, fraudScore is set
//   6. Verify webhook-receiver: payment.created event received
//   7. Verify webhook-receiver: payment.approved event received, signatureValid=true
//   8. POST /payments/{id}/capture
//   9. Assert status is captured, capturedAt set
//  10. Verify webhook-receiver: payment.captured event received
//  11. POST /payments/{id}/refund with amount and reason
//  12. Assert status is refunded, refundedAt set
//  13. Verify webhook-receiver: payment.refunded event received
//  14. GET /api/v1/transactions?paymentId={id}
//  15. Assert 3 transactions: authorization, capture, refund
//  16. Wait for MongoSync (up to 150s)
//  17. Verify PostgreSQL: payment row matches final state
//  18. Verify PostgreSQL: 3 transaction rows exist
//  19. Compare MongoDB vs PostgreSQL: all fields consistent
//  20. Cleanup: delete test webhook
```

- [ ] **Step 2: Run full suite**

Run: `cd tests/e2e && npx playwright test`
Expected: All phases pass

- [ ] **Step 3: Final commit**

```bash
git add tests/e2e/
git commit -m "feat(e2e): complete Playwright E2E test suite covering full payment lifecycle"
```

---

## Execution Order

| Phase | Tests | Dependencies | Estimated Time |
|-------|-------|-------------|----------------|
| 1 | Health checks (4) | Services running | 5s |
| 2 | Payments CRUD (6) | API + MongoDB | 10s |
| 3 | Payment lifecycle (4+1) | API + DDS services | 60s |
| 4 | Capture/refund (5) | Phase 3 payments | 60s |
| 5 | Webhooks (6) | API + webhook-receiver | 30s |
| 6 | Merchants (3) | API | 5s |
| 7 | Transactions/stats (4) | Phase 3-4 data | 10s |
| 8 | Error cases (8) | API | 5s |
| 9 | DB verification (5) | All services + MongoSync | 180s |
| 10 | Frontend (8) | Web + API | 30s |
| **Total** | **~54 tests** | | **~6 min** |

## Prerequisites for Running

```bash
# Start all services (from project root)
bash scripts/start-all-bg.sh

# Run all E2E tests
cd tests/e2e
npm install
npx playwright test

# Run specific phase
npx playwright test specs/phase3

# Run only API tests
npx playwright test --project=api

# Run only UI tests
npx playwright test --project=ui
```
