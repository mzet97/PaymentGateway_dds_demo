import { test, expect } from '@playwright/test';
import { ApiClient } from '../helpers/api-client';
import { WEBHOOK_RECEIVER_URL } from '../helpers/fixtures';

test.describe('Phase 1 - Health Checks', () => {
  let api: ApiClient;

  test.beforeEach(async ({ request }) => {
    api = new ApiClient(request);
  });

  test('API health check returns 200 with status healthy', async () => {
    const response = await api.healthCheck();

    expect(response.status()).toBe(200);
    const body = await response.json();
    expect(body.status).toBe('healthy');
  });

  test('Webhook receiver health check returns 200', async () => {
    const response = await fetch(`${WEBHOOK_RECEIVER_URL}/health`);

    expect(response.status).toBe(200);
    const body = await response.json();
    expect(body).toBeDefined();
  });
});
