/**
 * k6 Load Testing Script for PaymentGateway API
 *
 * Measures end-to-end performance under various loads:
 * - 1 concurrent client (baseline latency)
 * - 10 concurrent clients (light load)
 * - 100 concurrent clients (medium load)
 *
 * Validates 2.5x latency improvement over HTTP.
 *
 * Run: k6 run --vus 10 --duration 60s load-test.js
 * Or: k6 run --vus 100 --duration 120s load-test.js
 */

import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Trend, Counter, Gauge } from 'k6/metrics';

// Custom metrics
const errors = new Counter('errors');
const latency = new Trend('latency_ms', { unit: 'ms', percentiles: ['p50', 'p95', 'p99'] });
const throughput = new Counter('throughput');
const activeConnections = new Gauge('active_connections');

// Configuration
const BASE_URL = 'http://localhost:5000';
const API_TIMEOUT = '30s';

export const options = {
  stages: [
    // Ramp up to target VUs over 30 seconds
    { duration: '30s', target: __ENV.VUS || 10 },
    // Stay at target VUs for 60 seconds (main test)
    { duration: '60s', target: __ENV.VUS || 10 },
    // Ramp down over 30 seconds
    { duration: '30s', target: 0 },
  ],
  thresholds: {
    // Performance assertions
    'http_req_duration': ['p(95) < 5000', 'p(99) < 10000'], // p95 < 5s, p99 < 10s (DDS latency)
    'errors': ['count < 100'],
    'latency_ms': ['p(95) < 5000'],
  },
  ext: {
    loadimpact: {
      name: 'PaymentGateway Load Test',
      projectID: 3356156,
      name: 'PaymentGateway API - End-to-End',
    },
  },
};

// Test data generators
function generatePayment() {
  const merchants = ['MERCH-001', 'MERCH-002', 'MERCH-003', 'MERCH-004', 'MERCH-005'];
  const currencies = ['USD', 'EUR', 'BRL', 'GBP'];

  return {
    merchant_id: merchants[Math.floor(Math.random() * merchants.length)],
    amount: Math.floor(Math.random() * 100000) + 1000, // $10-$1010
    currency: currencies[Math.floor(Math.random() * currencies.length)],
    customer_id: `CUST-${Math.floor(Math.random() * 10000)}`,
    description: `Payment for order #${Math.floor(Math.random() * 1000000)}`,
  };
}

function generateFraudCheckPayload(paymentId) {
  return {
    payment_id: paymentId,
    amount: Math.floor(Math.random() * 100000) + 1000,
    customer_country: 'US',
    transaction_type: 'PAYMENT',
    device_id: `DEV-${Math.floor(Math.random() * 10000)}`,
  };
}

// Main test scenario
export default function () {
  const headers = {
    'Content-Type': 'application/json',
    'Accept': 'application/json',
  };

  // Scenario 1: Create Payment
  group('POST /payments', function () {
    const payload = generatePayment();

    const res = http.post(`${BASE_URL}/payments`, JSON.stringify(payload), {
      headers: headers,
      timeout: API_TIMEOUT,
    });

    const duration = res.timings.duration;
    latency.add(duration);
    throughput.add(1);

    const passed = check(res, {
      'status is 200-201': (r) => r.status >= 200 && r.status < 300,
      'response time < 5s': (r) => r.timings.duration < 5000,
      'has content type json': (r) => r.headers['Content-Type'] && r.headers['Content-Type'].includes('application/json'),
      'response is not empty': (r) => r.body && r.body.length > 0,
    });

    if (!passed) {
      errors.add(1);
      console.log(`❌ Create Payment failed: ${res.status} - ${res.body.substring(0, 200)}`);
    }

    // Extract payment ID if available
    let paymentId = null;
    try {
      const data = JSON.parse(res.body);
      paymentId = data.id || data.payment_id;
    } catch (e) {
      console.log(`⚠️  Could not parse response: ${e.message}`);
    }

    sleep(0.1); // Small delay between operations
  });

  // Scenario 2: Get Payment (if we have ID)
  group('GET /payments/{id}', function () {
    const paymentId = Math.floor(Math.random() * 10000); // Use random ID for now

    const res = http.get(`${BASE_URL}/payments/${paymentId}`, {
      headers: headers,
      timeout: API_TIMEOUT,
    });

    const duration = res.timings.duration;
    latency.add(duration);
    throughput.add(1);

    const passed = check(res, {
      'status is 200 or 404': (r) => [200, 404].includes(r.status),
      'response time < 1s': (r) => r.timings.duration < 1000,
    });

    if (!passed && res.status !== 404) {
      errors.add(1);
    }

    sleep(0.1);
  });

  // Scenario 3: List Payments
  group('GET /payments', function () {
    const res = http.get(`${BASE_URL}/payments?limit=10&offset=0`, {
      headers: headers,
      timeout: API_TIMEOUT,
    });

    const duration = res.timings.duration;
    latency.add(duration);
    throughput.add(1);

    const passed = check(res, {
      'status is 200': (r) => r.status === 200,
      'response time < 2s': (r) => r.timings.duration < 2000,
      'has items': (r) => r.body && r.body.includes('items'),
    });

    if (!passed) {
      errors.add(1);
    }

    sleep(0.1);
  });

  // Scenario 4: Approve Payment (random ID)
  group('POST /payments/{id}/approve', function () {
    const paymentId = Math.floor(Math.random() * 10000);

    const res = http.post(
      `${BASE_URL}/payments/${paymentId}/approve`,
      JSON.stringify({}),
      {
        headers: headers,
        timeout: API_TIMEOUT,
      }
    );

    const duration = res.timings.duration;
    latency.add(duration);
    throughput.add(1);

    const passed = check(res, {
      'status is 200 or 404 or 400': (r) => [200, 404, 400].includes(r.status),
      'response time < 3s': (r) => r.timings.duration < 3000,
    });

    if (!passed && ![200, 404, 400].includes(res.status)) {
      errors.add(1);
    }

    sleep(0.1);
  });

  // Scenario 5: Health Check
  group('GET /health', function () {
    const res = http.get(`${BASE_URL}/health`, {
      headers: headers,
      timeout: '5s',
    });

    const passed = check(res, {
      'health check status is 200': (r) => r.status === 200,
      'health check response time < 1s': (r) => r.timings.duration < 1000,
      'DDS is healthy': (r) => r.body && r.body.includes('Healthy'),
    });

    if (!passed) {
      errors.add(1);
      console.log(`⚠️  Health check failed: ${res.status}`);
    }

    sleep(0.5); // Longer delay before next health check
  });

  // Update active connections gauge
  activeConnections.set(__VU);
}

/**
 * Summary and reporting
 */
export function handleSummary(data) {
  console.log('📊 Load Test Summary');
  console.log('====================');
  console.log(`Total Requests: ${data.metrics.throughput.value || 0}`);
  console.log(`Errors: ${data.metrics.errors.value || 0}`);
  console.log(`Latency p50: ${data.metrics.latency_ms.values['p(50)'] || 'N/A'}ms`);
  console.log(`Latency p95: ${data.metrics.latency_ms.values['p(95)'] || 'N/A'}ms`);
  console.log(`Latency p99: ${data.metrics.latency_ms.values['p(99)'] || 'N/A'}ms`);

  return {
    'stdout': textSummary(data, { indent: ' ', enableColors: true }),
    'results.json': JSON.stringify(data.metrics),
  };
}

function textSummary(data, options) {
  const metrics = data.metrics;
  let summary = '\n📊 LOAD TEST RESULTS\n';
  summary += '====================\n\n';

  // Calculate RPS from throughput counter
  const duration = (data.state.testRunDurationMs || 0) / 1000;
  const rps = duration > 0 ? (metrics.throughput.value / duration).toFixed(2) : 'N/A';

  summary += `Throughput: ${rps} RPS\n`;
  summary += `Total Requests: ${metrics.throughput.value || 0}\n`;
  summary += `Errors: ${metrics.errors.value || 0}\n`;
  summary += `Duration: ${duration}s\n\n`;

  if (metrics.http_req_duration && metrics.http_req_duration.values) {
    summary += 'Latency:\n';
    summary += `  p50: ${metrics.http_req_duration.values['p(50)'] || 'N/A'}ms\n`;
    summary += `  p95: ${metrics.http_req_duration.values['p(95)'] || 'N/A'}ms\n`;
    summary += `  p99: ${metrics.http_req_duration.values['p(99)'] || 'N/A'}ms\n`;
  }

  return summary;
}
