import dotenv from 'dotenv';
import path from 'path';

dotenv.config({ path: path.resolve(__dirname, '..', '.env') });

export const MERCHANT_ID = process.env.MERCHANT_ID || '11111111-1111-1111-1111-111111111111';
export const API_KEY = process.env.API_KEY || 'sk_test_smoke_merchant';
export const API_BASE_URL = process.env.API_BASE_URL || 'http://localhost:5000';
export const WEB_BASE_URL = process.env.WEB_BASE_URL || 'http://localhost:3000';
export const WEBHOOK_RECEIVER_URL = process.env.WEBHOOK_RECEIVER_URL || 'http://localhost:4000';

let counter = 0;

const names = ['maria', 'joao', 'ana', 'carlos', 'julia', 'pedro', 'luisa', 'rafael'];

export function randomEmail(): string {
  const name = names[counter % names.length];
  return `${name}.silva${++counter}@gmail.com`;
}

export function randomDocument(): string {
  return String(Math.floor(Math.random() * 99999999999)).padStart(11, '0');
}

export function randomIdempotencyKey(): string {
  return `idem_e2e_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`;
}

export function createPaymentData(overrides: Record<string, unknown> = {}) {
  return {
    merchantId: MERCHANT_ID,
    // Keep amount low (15-45 BRL) to ensure AI fraud score stays low → approved
    amount: Math.round((Math.random() * 30 + 15) * 100) / 100,
    currency: 'BRL',
    method: 'pix',
    customer: {
      email: randomEmail(),
      name: `${names[counter % names.length].charAt(0).toUpperCase() + names[counter % names.length].slice(1)} Silva`,
      document: randomDocument(),
    },
    description: `E2E test payment ${Date.now()}`,
    idempotencyKey: randomIdempotencyKey(),
    ...overrides,
  };
}
