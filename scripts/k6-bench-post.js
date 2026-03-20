import http from 'k6/http';
import { check, sleep } from 'k6';
import { randomString } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';
import { Counter, Rate, Trend } from 'k6/metrics';

const successRate = new Rate('success_rate');
const paymentCreated = new Counter('payments_created');
const latency = new Trend('payment_latency', true);

export const options = {
  scenarios: {
    ramp_up: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '10s', target: 10 },
        { duration: '30s', target: 50 },
        { duration: '20s', target: 100 },
        { duration: '30s', target: 100 },
        { duration: '10s', target: 0 },
      ],
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<500', 'p(99)<1000'],
    success_rate: ['rate>0.95'],
  },
};

const API_URL = __ENV.API_URL || 'http://localhost:5000';
const API_KEY = __ENV.API_KEY || 'sk_test_smoke_merchant';
const MERCHANT_ID = __ENV.MERCHANT_ID || '11111111-1111-1111-1111-111111111111';

const headers = {
  'Content-Type': 'application/json',
  'X-API-Key': API_KEY,
};

const methods = ['credit_card', 'debit_card', 'pix', 'boleto'];
const currencies = ['BRL', 'USD'];

function randomPayment() {
  return JSON.stringify({
    merchantId: MERCHANT_ID,
    amount: Math.round((Math.random() * 9999 + 1) * 100) / 100,
    currency: currencies[Math.floor(Math.random() * currencies.length)],
    method: methods[Math.floor(Math.random() * methods.length)],
    customer: {
      email: `bench-${randomString(6)}@test.local`,
      name: `K6 User ${__VU}-${__ITER}`,
      document: String(Math.floor(Math.random() * 99999999999)).padStart(11, '0'),
    },
    description: `k6 benchmark payment VU=${__VU} ITER=${__ITER}`,
  });
}

export default function () {
  const payload = randomPayment();
  const res = http.post(`${API_URL}/api/v1/payments`, payload, { headers });

  const ok = check(res, {
    'status is 202': (r) => r.status === 202,
    'has paymentId': (r) => {
      try { return JSON.parse(r.body).paymentId !== undefined; } catch { return false; }
    },
  });

  successRate.add(ok);
  if (ok) paymentCreated.add(1);
  latency.add(res.timings.duration);
}
