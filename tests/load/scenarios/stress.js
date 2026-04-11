import mixedWorkload from "../scripts/mixed-workload.js";

/**
 * Stress test: ramp to 200 VUs to find the breaking point.
 * Identifies the maximum load before error rates spike or latency degrades.
 */
export const options = {
  stages: [
    { duration: "2m", target: 50 },    // Baseline
    { duration: "3m", target: 100 },   // Push
    { duration: "3m", target: 150 },   // Stress
    { duration: "3m", target: 200 },   // Peak
    { duration: "2m", target: 0 },     // Recovery
  ],
  thresholds: {
    // Relaxed thresholds — we expect some failures at peak
    http_req_duration: ["p(95)<2000"],
    http_req_failed: ["rate<0.10"],
  },
};

export default mixedWorkload;
