import type { NextConfig } from "next";

/**
 * Baseline security headers applied to every HTML response. Next.js terminates
 * TLS on Vercel for app.sendnex.xyz, so headers live here rather than in the
 * nginx reverse proxy (which only serves the apex marketing site).
 *
 * TODO: tighten CSP by moving off 'unsafe-inline' / 'unsafe-eval' to nonces once
 * all inline scripts/styles are audited.
 */
const securityHeaders = [
  {
    key: "Strict-Transport-Security",
    value: "max-age=63072000; includeSubDomains; preload",
  },
  { key: "X-Frame-Options", value: "DENY" },
  { key: "X-Content-Type-Options", value: "nosniff" },
  { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
  {
    key: "Permissions-Policy",
    value: "camera=(), microphone=(), geolocation=()",
  },
  {
    key: "Content-Security-Policy",
    value: [
      "default-src 'self'",
      "script-src 'self' 'unsafe-inline' 'unsafe-eval'",
      "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
      "font-src 'self' https://fonts.gstatic.com data:",
      "img-src 'self' data: https:",
      "connect-src 'self' https://app.sendnex.xyz",
      "frame-ancestors 'none'",
      "base-uri 'self'",
      "form-action 'self'",
    ].join("; "),
  },
];

const nextConfig: NextConfig = {
  output: "standalone",
  async headers() {
    return [
      {
        source: "/:path*",
        headers: securityHeaders,
      },
    ];
  },
  async redirects() {
    return [
      // Landing advertises docs.sendnex.xyz — make the in-app /docs path route
      // to the public docs site instead of a login-gated dead-end.
      {
        source: "/docs",
        destination: "https://docs.sendnex.xyz",
        permanent: true,
      },
      {
        source: "/docs/:path*",
        destination: "https://docs.sendnex.xyz/:path*",
        permanent: true,
      },
    ];
  },
};

export default nextConfig;
