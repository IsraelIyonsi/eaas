import http from "k6/http";
import { check } from "k6";
import { getHeaders, getBaseUrl } from "../lib/auth.js";
import { sendEmailPayload } from "../lib/data.js";

/**
 * Throughput target test: constant arrival rate of 140 requests/sec.
 *
 * This validates the API ingestion rate (HTTP → queue), NOT the SES delivery
 * rate which is capped at MaxSendRate in appsettings.json.
 *
 * The 140 emails/sec target measures how fast the API can accept and enqueue
 * email send requests.
 */

const BASE_URL = getBaseUrl();
const headers = getHeaders();

export const options = {
  scenarios: {
    throughput: {
      executor: "constant-arrival-rate",
      rate: 140,
      timeUnit: "1s",
      duration: "5m",
      preAllocatedVUs: 50,
      maxVUs: 200,
    },
  },
  thresholds: {
    http_req_duration: ["p(95)<500", "p(99)<2000"],
    http_req_failed: ["rate<0.01"],
    http_reqs: ["rate>=130"], // At least 130/sec sustained (93% of target)
  },
};

export default function () {
  const res = http.post(`${BASE_URL}/api/v1/emails`, sendEmailPayload(), {
    headers,
  });

  check(res, {
    "status is 2xx": (r) => r.status >= 200 && r.status < 300,
    "response time < 500ms": (r) => r.timings.duration < 500,
  });
}
