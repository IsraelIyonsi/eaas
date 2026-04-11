import sendEmail from "../scripts/send-email.js";

/**
 * Smoke test: 1 VU, 30 seconds.
 * Validates the API is responsive and the test scripts work.
 */
export const options = {
  vus: 1,
  duration: "30s",
  thresholds: {
    http_req_duration: ["p(95)<1000"],
    http_req_failed: ["rate<0.05"],
  },
};

export default sendEmail;
