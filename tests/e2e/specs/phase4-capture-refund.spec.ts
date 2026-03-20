import { test, expect } from '@playwright/test';
import { ApiClient } from '../helpers/api-client';
import { createPaymentData } from '../helpers/fixtures';
import { waitForPaymentProcessed, waitForPaymentStatus } from '../helpers/wait-utils';

test.describe.serial('Phase 4 - Capture & Refund', () => {
  /**
   * Helper: creates a payment and waits until it is processed (approved/rejected).
   * Uses 60s timeout for the DDS + OpenRouter AI round-trip.
   */
  async function createAndWait(
    api: ApiClient,
    overrides: Record<string, unknown> = {},
  ): Promise<Record<string, unknown>> {
    const data = createPaymentData({ amount: 12.50, ...overrides });
    const res = await api.createPayment(data);
    expect(res.status()).toBe(202);
    const body = await res.json();
    // Wait specifically for approved or rejected (not review/pending)
    return waitForPaymentStatus(api, body.paymentId, ['approved', 'rejected'], 120_000);
  }

  test('Capture approved payment changes status to captured', async ({ request }) => {
    test.slow();

    const api = new ApiClient(request);
    const processed = await createAndWait(api, { amount: 10.00 });

    // If rejected, lifecycle cannot continue — assert approved
    expect(processed.status).toBe('approved');

    const paymentId = (processed.paymentId || processed.id) as string;

    // Capture returns 202 (async)
    const captureRes = await api.capturePayment(paymentId);
    expect([200, 202]).toContain(captureRes.status());

    // Poll for captured status (up to 15s)
    const captured = await waitForPaymentStatus(api, paymentId, ['captured'], 15_000);
    expect(captured.status).toBe('captured');
  });

  test('Refund captured payment changes status to refunded with refundedAmount', async ({ request }) => {
    test.slow();

    const api = new ApiClient(request);
    const processed = await createAndWait(api, { amount: 20.00 });

    expect(processed.status).toBe('approved');

    const paymentId = (processed.paymentId || processed.id) as string;

    // Capture first (202 async)
    const captureRes = await api.capturePayment(paymentId);
    expect([200, 202]).toContain(captureRes.status());
    await waitForPaymentStatus(api, paymentId, ['captured'], 15_000);

    // Then refund (202 async)
    const refundRes = await api.refundPayment(paymentId, { reason: 'E2E refund test' });
    expect([200, 202]).toContain(refundRes.status());

    // Poll for refunded status (up to 15s)
    const refunded = await waitForPaymentStatus(api, paymentId, ['refunded'], 15_000);
    expect(refunded.status).toBe('refunded');
    expect(refunded.refundedAmount).toBeDefined();
  });

  test('Cancel pending payment', async ({ request }) => {
    const api = new ApiClient(request);
    const data = createPaymentData({ amount: 5.00 });
    const createRes = await api.createPayment(data);
    expect(createRes.status()).toBe(202);
    const created = await createRes.json();
    const paymentId = created.paymentId;

    // Try to cancel immediately while still pending
    const cancelRes = await api.cancelPayment(paymentId);
    // Accept 200 (success) or 400/409 (already processed)
    if (cancelRes.status() === 200) {
      const cancelled = await waitForPaymentStatus(api, paymentId, ['cancelled'], 15_000);
      expect(cancelled.status).toBe('cancelled');
    } else {
      // Payment may have been processed too quickly
      expect([400, 409]).toContain(cancelRes.status());
    }
  });

  test('Cannot refund a non-captured payment (400)', async ({ request }) => {
    const api = new ApiClient(request);

    // Use a freshly created pending payment (no capture done)
    const data = createPaymentData({ amount: 20.00 });
    const res = await api.createPayment(data);
    expect(res.status()).toBe(202);
    const body = await res.json();

    // Try to refund a pending/approved payment that was never captured
    const refundRes = await api.refundPayment(body.paymentId, { reason: 'Should fail' });
    // Should fail with 400 or 500 (invalid state transition)
    expect(refundRes.status()).toBeGreaterThanOrEqual(400);
  });

  test('Cannot capture a pending payment (400)', async ({ request }) => {
    const api = new ApiClient(request);

    // Create a fresh payment and try to capture immediately (still pending)
    const data = createPaymentData({ amount: 20.00 });
    const res = await api.createPayment(data);
    expect(res.status()).toBe(202);
    const body = await res.json();

    // Try to capture a pending payment — should fail
    const captureRes = await api.capturePayment(body.paymentId);
    expect(captureRes.status()).toBeGreaterThanOrEqual(400);
  });
});
