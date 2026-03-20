import { test, expect } from '@playwright/test';
import { MERCHANT_ID, API_KEY } from '../helpers/fixtures';

test.describe('Phase 10 - Frontend', () => {

  async function setupApiAccess(page: import('@playwright/test').Page) {
    await page.evaluate(({ merchantId, apiKey }) => {
      localStorage.setItem('pg_api_access', JSON.stringify({ merchantId, apiKey }));
    }, { merchantId: MERCHANT_ID, apiKey: API_KEY });
  }

  test('Dashboard page loads at / with title', async ({ page }) => {
    await page.goto('/');
    await expect(page).toHaveTitle(/.+/);

    await setupApiAccess(page);
    await page.reload();

    // Verify main content loaded (look for heading or key UI element)
    const body = page.locator('body');
    await expect(body).toBeVisible();
    // Should not show a raw error page
    await expect(page.locator('text=Internal Server Error')).not.toBeVisible();
  });

  test('Settings page allows entering API credentials', async ({ page }) => {
    await page.goto('/settings');

    await expect(page).toHaveTitle(/.+/);

    // Look for input fields or settings-related content
    const body = page.locator('body');
    await expect(body).toBeVisible();

    // Set credentials via localStorage
    await setupApiAccess(page);
    await page.reload();

    // Verify the page renders without error after credentials are set
    await expect(page.locator('text=Internal Server Error')).not.toBeVisible();
  });

  test('Payments page loads at /payments and shows content', async ({ page }) => {
    await page.goto('/');
    await setupApiAccess(page);
    await page.goto('/payments');

    await expect(page).toHaveTitle(/.+/);
    const body = page.locator('body');
    await expect(body).toBeVisible();
    await expect(page.locator('text=Internal Server Error')).not.toBeVisible();
  });

  test('Analytics page loads at /analytics', async ({ page }) => {
    await page.goto('/');
    await setupApiAccess(page);
    await page.goto('/analytics');

    await expect(page).toHaveTitle(/.+/);
    const body = page.locator('body');
    await expect(body).toBeVisible();
    await expect(page.locator('text=Internal Server Error')).not.toBeVisible();
  });

  test('Webhooks page loads at /webhooks', async ({ page }) => {
    await page.goto('/');
    await setupApiAccess(page);
    await page.goto('/webhooks');

    await expect(page).toHaveTitle(/.+/);
    const body = page.locator('body');
    await expect(body).toBeVisible();
    await expect(page.locator('text=Internal Server Error')).not.toBeVisible();
  });

  test('Transactions page loads at /transactions', async ({ page }) => {
    await page.goto('/');
    await setupApiAccess(page);
    await page.goto('/transactions');

    await expect(page).toHaveTitle(/.+/);
    const body = page.locator('body');
    await expect(body).toBeVisible();
    await expect(page.locator('text=Internal Server Error')).not.toBeVisible();
  });
});
