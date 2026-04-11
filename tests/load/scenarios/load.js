import mixedWorkload from "../scripts/mixed-workload.js";

/**
 * Load test: ramp to 50 VUs over 5 minutes, hold for 10 minutes.
 * Validates steady-state performance under expected production load.
 */
export const options = {
  stages: [
    { duration: "2m", target: 10 },   // Warm up
    { duration: "3m", target: 50 },   // Ramp to target
    { duration: "10m", target: 50 },  // Hold at target
    { duration: "2m", target: 0 },    // Cool down
  ],
  thresholds: {
    http_req_duration: ["p(95)<500", "p(99)<2000"],
    http_req_failed: ["rate<0.01"],
  },
};

export default mixedWorkload;
