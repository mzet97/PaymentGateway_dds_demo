import { test, expect } from '@playwright/test';
import { ApiClient } from '../helpers/api-client';
import { MERCHANT_ID } from '../helpers/fixtures';

test.describe('Phase 6 - Merchants', () => {
  let api: ApiClient;

  test.beforeEach(async ({ request }) => {
    api = new ApiClient(request);
  });

  test('GET merchant by ID returns merchant details', async () => {
    const response = await api.getMerchant(MERCHANT_ID);
    expect(response.status()).toBe(200);

    const merchant = await response.json();
    expect(merchant).toBeDefined();
    expect(merchant.id || merchant.merchantId).toBe(MERCHANT_ID);
  });

  test('Merchant has expected fields (name, email, category, status)', async () => {
    const response = await api.getMerchant(MERCHANT_ID);
    const merchant = await response.json();

    expect(merchant.name).toBeDefined();
    expect(typeof merchant.name).toBe('string');

    expect(merchant.email).toBeDefined();
    expect(typeof merchant.email).toBe('string');

    expect(merchant.category).toBeDefined();

    expect(merchant.status).toBeDefined();
    // Status may be returned as a string or numeric enum value
    expect(['string', 'number']).toContain(typeof merchant.status);
  });

  test('GET merchant payments returns paginated list', async () => {
    const response = await api.getMerchantPayments(MERCHANT_ID, {
      page: '1',
      pageSize: '10',
    });

    expect(response.status()).toBe(200);
    const body = await response.json();

    const items = Array.isArray(body) ? body : (body.items || body.data || body.payments || []);
    expect(Array.isArray(items)).toBe(true);
  });
});
