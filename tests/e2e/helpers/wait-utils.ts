import { APIRequestContext } from '@playwright/test';
import { ApiClient } from './api-client';

export async function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export async function waitForCondition<T>(
  fn: () => Promise<T | null | undefined>,
  options: { timeoutMs?: number; intervalMs?: number; label?: string } = {},
): Promise<T> {
  const { timeoutMs = 30_000, intervalMs = 1_000, label = 'condition' } = options;
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    const result = await fn();
    if (result) return result;
    await sleep(intervalMs);
  }

  throw new Error(`Timeout waiting for ${label} after ${timeoutMs}ms`);
}

export async function waitForPaymentStatus(
  api: ApiClient,
  paymentId: string,
  targetStatuses: string[],
  timeoutMs = 30_000,
): Promise<Record<string, unknown>> {
  return waitForCondition(
    async () => {
      const res = await api.getPayment(paymentId);
      if (!res.ok()) return null;
      const body = await res.json();
      const status = body.status as string;
      if (targetStatuses.includes(status)) return body;
      if (status === 'pending' || status === 'processing') return null;
      // Unexpected terminal status — return it anyway so test can assert
      return body;
    },
    { timeoutMs, intervalMs: 1_000, label: `payment ${paymentId} status in [${targetStatuses}]` },
  );
}

/**
 * Wait until payment is fully processed by DDS pipeline.
 * Returns as soon as fraudDecision is set (approved, rejected, or review).
 * For "review" decisions, status stays "pending" but processing is complete.
 */
export async function waitForPaymentProcessed(
  api: ApiClient,
  paymentId: string,
  timeoutMs = 30_000,
): Promise<Record<string, unknown>> {
  return waitForCondition(
    async () => {
      const res = await api.getPayment(paymentId);
      if (!res.ok()) return null;
      const body = await res.json();
      // Done when fraud analysis is complete (fraudDecision is set)
      if (body.fraudDecision) return body;
      const status = body.status as string;
      // Also done for any terminal status
      if (['approved', 'rejected', 'captured', 'refunded', 'cancelled', 'failed'].includes(status)) return body;
      return null;
    },
    { timeoutMs, intervalMs: 1_000, label: `payment ${paymentId} processed` },
  );
}
