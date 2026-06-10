// k6 performance / load test for the SADC OMS API.
//
// Ramps virtual users against the read and write paths and asserts latency/error
// thresholds. Run via the official k6 Docker image (no local install needed):
//
//   docker run --rm -e BASE_URL=http://host.docker.internal:5080 \
//     -v "$PWD/scripts:/scripts" grafana/k6 run /scripts/load-test.js
//
// (host.docker.internal resolves to the host on Docker Desktop. If you have k6
//  installed locally, just: k6 run -e BASE_URL=http://localhost:5080 scripts/load-test.js)

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter } from 'k6/metrics';

const BASE = __ENV.BASE_URL || 'http://host.docker.internal:5080';
const ordersCreated = new Counter('orders_created');

export const options = {
  stages: [
    { duration: '15s', target: 10 }, // ramp up
    { duration: '30s', target: 25 }, // sustain
    { duration: '10s', target: 0 },  // ramp down
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'],       // <1% errors
    http_req_duration: ['p(95)<500'],     // 95% of requests under 500ms
    'http_req_duration{op:list_orders}': ['p(95)<400'],
  },
};

// Authenticate once and create a customer to place orders against.
export function setup() {
  const tokenRes = http.post(`${BASE}/api/dev/token`);
  const token = tokenRes.json('accessToken');
  const headers = { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' };

  const custRes = http.post(
    `${BASE}/api/customers`,
    JSON.stringify({ name: 'Load Test', email: `load-${Date.now()}@example.com`, countryCode: 'ZA' }),
    { headers },
  );
  return { token, customerId: custRes.json('id') };
}

export default function (data) {
  const headers = { Authorization: `Bearer ${data.token}`, 'Content-Type': 'application/json' };

  // Read path — list customers and orders (the most common operations).
  const customers = http.get(`${BASE}/api/customers?page=1&pageSize=20`, { headers });
  check(customers, { 'list customers 200': (r) => r.status === 200 });

  const orders = http.get(`${BASE}/api/orders?page=1&pageSize=20&sort=createdAt_desc`, {
    headers,
    tags: { op: 'list_orders' },
  });
  check(orders, { 'list orders 200': (r) => r.status === 200 });

  // Write path — create an order roughly every fourth iteration.
  if (Math.random() < 0.25) {
    const res = http.post(
      `${BASE}/api/orders`,
      JSON.stringify({
        customerId: data.customerId,
        currencyCode: 'ZAR',
        lines: [{ productSku: 'LOAD-1', quantity: 2, unitPrice: 99.99 }],
      }),
      { headers },
    );
    if (check(res, { 'create order 201': (r) => r.status === 201 })) {
      ordersCreated.add(1);
    }
  }

  sleep(1);
}
