import http from "k6/http";
import { check, sleep } from "k6";
import { getHeaders, getBaseUrl } from "../lib/auth.js";
import { sendEmailPayload } from "../lib/data.js";

const BASE_URL = getBaseUrl();
const headers = getHeaders();

export const options = {
  thresholds: {
    http_req_duration: ["p(95)<500", "p(99)<2000"],
    http_req_failed: ["rate<0.01"],
  },
};

export default function () {
  const res = http.post(`${BASE_URL}/api/v1/emails`, sendEmailPayload(), {
    headers,
  });

  check(res, {
    "status is 200 or 202": (r) => r.status === 200 || r.status === 202,
    "has success field": (r) => {
      try {
        return JSON.parse(r.body).success === true;
      } catch {
        return false;
      }
    },
  });

  sleep(0.1);
}
