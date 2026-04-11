import http from "k6/http";
import { check, sleep } from "k6";
import { getHeaders, getBaseUrl } from "../lib/auth.js";
import { sendEmailPayload, templatePayload } from "../lib/data.js";

/**
 * Mixed workload simulating realistic API usage:
 * - 60% send email
 * - 20% list emails
 * - 10% template operations
 * - 10% list domains
 */

const BASE_URL = getBaseUrl();
const headers = getHeaders();

export const options = {
  thresholds: {
    http_req_duration: ["p(95)<500", "p(99)<2000"],
    http_req_failed: ["rate<0.01"],
  },
};

export default function () {
  const roll = Math.random();

  if (roll < 0.6) {
    // Send email (60%)
    const res = http.post(`${BASE_URL}/api/v1/emails`, sendEmailPayload(), {
      headers,
    });
    check(res, {
      "send: status 2xx": (r) => r.status >= 200 && r.status < 300,
    });
  } else if (roll < 0.8) {
    // List emails (20%)
    const res = http.get(`${BASE_URL}/api/v1/emails?page=1&page_size=20`, {
      headers,
    });
    check(res, {
      "list: status 200": (r) => r.status === 200,
    });
  } else if (roll < 0.9) {
    // List templates (10%)
    const res = http.get(`${BASE_URL}/api/v1/templates`, { headers });
    check(res, {
      "templates: status 200": (r) => r.status === 200,
    });
  } else {
    // List domains (10%)
    const res = http.get(`${BASE_URL}/api/v1/domains`, { headers });
    check(res, {
      "domains: status 200": (r) => r.status === 200,
    });
  }

  sleep(0.05);
}
