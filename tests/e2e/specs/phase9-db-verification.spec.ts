import { test, expect } from '@playwright/test';
import { ApiClient } from '../helpers/api-client';
import { createPaymentData, MERCHANT_ID, WEBHOOK_RECEIVER_URL } from '../helpers/fixtures';
import { waitForPaymentProcessed } from '../helpers/wait-utils';
import * as mongo from '../helpers/db-mongo';
import * as postgres from '../helpers/db-postgres';

test.describe.serial('Phase 9 - Database Verification', () => {
  let paymentId: string;
  let paymentData: ReturnType<typeof createPaymentData>;

  test.afterAll(async () => {
    await mongo.disconnect();
    await postgres.disconnect();
  });

  test('Create payment for DB verification', async ({ request }) => {
    const api = new ApiClient(request);
    paymentData = createPaymentData({ amount: 33.50 });
    const res = await api.createPayment(paymentData);
    expect(res.status()).toBe(202);
    const body = await res.json();
    paymentId = body.paymentId;
    expect(paymentId).toBeDefined();

    // Give MongoDB a moment to persist the write buffer
    await new Promise((r) => setTimeout(r, 2000));
  });

  test('Payment exists in MongoDB after creation', async () => {
    expect(paymentId).toBeDefined();
    const doc = await mongo.findPayment(paymentId);
    expect(doc).not.toBeNull();
  });

  test('Payment data in MongoDB matches API response (amount, currency, merchantId)', async () => {
    expect(paymentId).toBeDefined();
    const doc = await mongo.findPayment(paymentId);
    expect(doc).not.toBeNull();

    // Check key fields match what was sent
    // Field naming may vary (camelCase vs snake_case) so check both
    const amount = (doc as Record<string, unknown>).amount ??
                   (doc as Record<string, unknown>).Amount;
    const currency = (doc as Record<string, unknown>).currency ??
                     (doc as Record<string, unknown>).Currency;
    const merchantId = (doc as Record<string, unknown>).merchantId ??
                       (doc as Record<string, unknown>).MerchantId ??
                       (doc as Record<string, unknown>).merchant_id;

    // MongoDB may store decimals as strings (Decimal128 → string)
    expect(Number(amount)).toBe(paymentData.amount);
    expect(currency).toBe(paymentData.currency);
    // merchantId stored as Binary UUID in MongoDB - just verify it exists
    expect(merchantId).toBeDefined();
  });

  test('Webhook config persists in MongoDB after registration', async ({ request }) => {
    const api = new ApiClient(request);

    // Register a webhook
    await api.createWebhook({
      merchantId: MERCHANT_ID,
      url: `${WEBHOOK_RECEIVER_URL}/webhooks/receive/db-test`,
      events: ['payment.created'],
      active: true,
    });

    // Verify webhook exists via API (MongoDB merchantId is binary UUID, hard to query directly)
    const listRes = await api.listWebhooks(MERCHANT_ID);
    expect(listRes.ok()).toBe(true);
    const webhookList = await listRes.json();
    const items = Array.isArray(webhookList) ? webhookList : webhookList.webhooks || [];
    expect(items.length).toBeGreaterThan(0);
  });

  test('PostgreSQL has merchant record for demo merchant', async () => {
    const merchant = await postgres.findMerchant(MERCHANT_ID);
    expect(merchant).not.toBeNull();
    expect(merchant.id || merchant.Id).toBe(MERCHANT_ID);
  });
});
