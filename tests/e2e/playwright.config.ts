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
      testMatch: /phase[1-9]-.*\.spec\.ts/,
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
