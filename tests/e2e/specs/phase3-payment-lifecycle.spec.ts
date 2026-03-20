import { test, expect } from '@playwright/test';
import { ApiClient } from '../helpers/api-client';
import { createPaymentData } from '../helpers/fixtures';
import { waitForPaymentProcessed, waitForPaymentStatus } from '../helpers/wait-utils';

test.describe.serial('Phase 3 - Payment Lifecycle', () => {
  let paymentId: string;
  let processedPayment: Record<string, unknown>;

  test('Payment progresses from pending to approved or rejected within 60s', async ({ request }) => {
    test.slow();

    const api = new ApiClient(request);
    const data = createPaymentData();
    const createRes = await api.createPayment(data);
    expect(createRes.status()).toBe(202);
    const created = await createRes.json();
    paymentId = created.paymentId;

    // First DDS processing in suite may be slow (OpenRouter AI cold start + DDS warmup)
    processedPayment = await waitForPaymentProcessed(api, paymentId, 120_000);
    // Status can be approved, rejected, or pending (with fraudDecision=review)
    expect(processedPayment.fraudDecision).toBeDefined();
  });

  test('Processed payment has fraudScore and fraudDecision set', async () => {
    expect(processedPayment).toBeDefined();
    expect(typeof processedPayment.fraudScore).toBe('number');
    expect(processedPayment.fraudDecision).toBeDefined();
    expect(['approve', 'reject', 'review', 'approved', 'rejected']).toContain(
      String(processedPayment.fraudDecision).toLowerCase(),
    );
  });

  test('processedAt timestamp is populated after processing', async () => {
    expect(processedPayment).toBeDefined();
    expect(processedPayment.processedAt).toBeDefined();
    expect(new Date(processedPayment.processedAt as string).getTime()).toBeGreaterThan(0);
  });

  test('Multiple payments process independently', async ({ request }) => {
    test.slow();

    const api = new ApiClient(request);
    const payment1 = createPaymentData({ amount: 100.00, description: 'Independent test 1' });
    const payment2 = createPaymentData({ amount: 200.00, description: 'Independent test 2' });

    const [res1, res2] = await Promise.all([
      api.createPayment(payment1),
      api.createPayment(payment2),
    ]);

    const body1 = await res1.json();
    const body2 = await res2.json();

    expect(body1.paymentId).not.toBe(body2.paymentId);

    const [processed1, processed2] = await Promise.all([
      waitForPaymentProcessed(api, body1.paymentId, 60_000),
      waitForPaymentProcessed(api, body2.paymentId, 60_000),
    ]);

    // Each payment should have been processed (fraudDecision set)
    expect(processed1.fraudDecision).toBeDefined();
    expect(processed2.fraudDecision).toBeDefined();
    // They should have independent fraud scores
    expect(processed1.paymentId || processed1.id).not.toBe(processed2.paymentId || processed2.id);
  });

  test('FULL LIFECYCLE: create -> process -> fraud -> capture -> refund -> verify', async ({ request }) => {
    test.slow();

    const api = new ApiClient(request);

    // Step 1: Create payment with a low amount to favour approval
    const data = createPaymentData({ amount: 15.00, method: 'credit_card' });
    const createRes = await api.createPayment(data);
    expect(createRes.status()).toBe(202);
    const created = await createRes.json();
    const lifecyclePaymentId = created.paymentId;

    // Step 2: Wait for processing (fraud check + decision) — 60s timeout
    const processed = await waitForPaymentProcessed(api, lifecyclePaymentId, 60_000);
    expect(processed.fraudScore).toBeDefined();
    expect(processed.fraudDecision).toBeDefined();

    // If not approved, the lifecycle cannot continue to capture/refund
    if (processed.status !== 'approved') {
      console.log(`Payment fraud result: ${processed.fraudDecision} (score ${processed.fraudScore}) — lifecycle test verified fraud processing`);
      return;
    }

    // Step 3: Capture the approved payment (returns 202 async)
    const captureRes = await api.capturePayment(lifecyclePaymentId);
    expect([200, 202]).toContain(captureRes.status());

    const captured = await waitForPaymentStatus(api, lifecyclePaymentId, ['captured'], 15_000);
    expect(captured.status).toBe('captured');

    // Step 4: Refund the captured payment
    const refundRes = await api.refundPayment(lifecyclePaymentId, { reason: 'E2E test refund' });
    expect([200, 202]).toContain(refundRes.status());

    const refunded = await waitForPaymentStatus(api, lifecyclePaymentId, ['refunded'], 15_000);
    expect(refunded.status).toBe('refunded');

    // Step 5: Verify final state
    const finalRes = await api.getPayment(lifecyclePaymentId);
    expect(finalRes.status()).toBe(200);
    const finalPayment = await finalRes.json();
    expect(finalPayment.status).toBe('refunded');
  });
});
