import http from 'k6/http';
import { check } from 'k6';
import { Rate, Trend } from 'k6/metrics';

const successRate = new Rate('success_rate');
const latency = new Trend('query_latency', true);

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
    http_req_duration: ['p(95)<300', 'p(99)<500'],
    success_rate: ['rate>0.95'],
  },
};

const API_URL = __ENV.API_URL || 'http://localhost:5000';
const API_KEY = __ENV.API_KEY || 'sk_test_smoke_merchant';
const MERCHANT_ID = __ENV.MERCHANT_ID || '11111111-1111-1111-1111-111111111111';

const headers = {
  'X-API-Key': API_KEY,
};

export default function () {
  // GET a single payment by ID (indexed lookup) or health check
  const endpoints = [
    `/health`,
    `/api/v1/merchants/${MERCHANT_ID}`,
  ];
  const endpoint = endpoints[Math.floor(Math.random() * endpoints.length)];
  const res = http.get(`${API_URL}${endpoint}`, { headers });

  const ok = check(res, {
    'status is 200': (r) => r.status === 200,
    'has body': (r) => r.body && r.body.length > 2,
  });

  successRate.add(ok);
  latency.add(res.timings.duration);
}
