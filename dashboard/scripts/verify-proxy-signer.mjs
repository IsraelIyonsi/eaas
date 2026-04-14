#!/usr/bin/env node
// -----------------------------------------------------------------------------
// verify-proxy-signer.mjs
//
// Contract-parity test for the admin proxy-token signer.
//
// This script LOCKS the signing input format:
//
//     "eaas.proxy.v1\n" + METHOD + "\n" + PATH + "\n" + USER_ID + "." + TIMESTAMP
//
// which MUST match the server-side verifier in
// `src/EaaS.Api/Authentication/AdminSessionAuthHandler.cs`
// (see `ProxyTokenHmacDomain = "eaas.proxy.v1\n"`).
//
// The dashboard has no vitest/jest harness installed (only Playwright for E2E),
// so this runs as a plain Node script. If a real unit-test runner is added
// later, port these vectors to `src/lib/auth/__tests__/proxy-signer.test.ts`.
//
// Run:   node dashboard/scripts/verify-proxy-signer.mjs
// CI:    exits non-zero on any assertion failure.
//
// Fixture (deterministic, computed locally with the same algorithm):
//   secret    = "test-secret-at-least-32-bytes-long-padding"
//   userId    = "00000000-0000-0000-0000-000000000001"
//   method    = "GET"
//   path      = "/api/admin/users"
//   timestamp = 1700000000
//   expected sig (hex, lowercase) =
//     3ea90c6e82407d39871432cd356eaf27db374323982c604e70b77d0baec8a884
//   expected token =
//     MTcwMDAwMDAwMA.3ea90c6e82407d39871432cd356eaf27db374323982c604e70b77d0baec8a884
// -----------------------------------------------------------------------------

import crypto from "node:crypto";

// Inlined signer. Mirrors `signAdminProxyToken` in
// dashboard/src/app/api/proxy/[...path]/route.ts. Any divergence here means
// the production signer and this locked contract have drifted — fix BOTH.
const PROXY_TOKEN_HMAC_DOMAIN = "eaas.proxy.v1\n";

function buildSigningInput(userId, method, path, timestamp) {
  return `${PROXY_TOKEN_HMAC_DOMAIN}${method.toUpperCase()}\n${path}\n${userId}.${timestamp}`;
}

function signAdminProxyToken(secret, userId, method, path, timestamp) {
  const ts = String(timestamp);
  const encodedTs = Buffer.from(ts, "utf-8").toString("base64url");
  const hmac = crypto.createHmac("sha256", secret);
  hmac.update(buildSigningInput(userId, method, path, ts));
  return {
    token: `${encodedTs}.${hmac.digest("hex")}`,
    encodedTs,
    signingInput: buildSigningInput(userId, method, path, ts),
  };
}

// --- Fixture ----------------------------------------------------------------
const SECRET = "test-secret-at-least-32-bytes-long-padding";
const USER_ID = "00000000-0000-0000-0000-000000000001";
const METHOD = "GET";
const PATH = "/api/admin/users";
const TIMESTAMP = 1700000000;

const EXPECTED_DOMAIN = "eaas.proxy.v1\n";
const EXPECTED_SIGNING_INPUT =
  "eaas.proxy.v1\nGET\n/api/admin/users\n00000000-0000-0000-0000-000000000001.1700000000";
const EXPECTED_HEX_SIG =
  "3ea90c6e82407d39871432cd356eaf27db374323982c604e70b77d0baec8a884";

// --- Assertions -------------------------------------------------------------
const failures = [];

function assert(cond, msg) {
  if (!cond) failures.push(msg);
}

const { token, signingInput } = signAdminProxyToken(
  SECRET,
  USER_ID,
  METHOD,
  PATH,
  TIMESTAMP,
);
const [, hexSig] = token.split(".");

// (a) domain prefix
assert(
  signingInput.startsWith(EXPECTED_DOMAIN),
  `signing input must start with domain prefix "eaas.proxy.v1\\n"; got: ${JSON.stringify(signingInput)}`,
);

// (b) signing input format
assert(
  signingInput === EXPECTED_SIGNING_INPUT,
  `signing input mismatch.\n  expected: ${JSON.stringify(EXPECTED_SIGNING_INPUT)}\n  actual:   ${JSON.stringify(signingInput)}`,
);

// (c) hex-lowercase signature, 64 chars (SHA-256 = 32 bytes)
assert(
  /^[0-9a-f]{64}$/.test(hexSig),
  `signature must be lowercase hex sha256 (64 chars); got: ${hexSig}`,
);

// (d) fixture signature matches expected hex
assert(
  hexSig === EXPECTED_HEX_SIG,
  `fixture signature mismatch.\n  expected: ${EXPECTED_HEX_SIG}\n  actual:   ${hexSig}`,
);

if (failures.length > 0) {
  console.error("verify-proxy-signer: FAIL");
  for (const f of failures) console.error("  -", f);
  process.exit(1);
}

console.log("verify-proxy-signer: OK");
console.log(`  domain prefix : ${JSON.stringify(EXPECTED_DOMAIN)}`);
console.log(`  signing input : ${JSON.stringify(EXPECTED_SIGNING_INPUT)}`);
console.log(`  signature     : ${hexSig}`);
console.log(`  token         : ${token}`);
