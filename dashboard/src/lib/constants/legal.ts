/**
 * Legal entity + contact shown on every public-facing legal page.
 * Required by CAN-SPAM Act §7704(a)(5) and GDPR Art 13(1)(a).
 *
 * Values are read from public env vars so deployments can override without a
 * code change. The postal address is intentionally optional — a missing line
 * is less harmful than a live placeholder such as "[ADDRESS REQUIRED]".
 */
export const SENDNEX_LEGAL_ENTITY =
  process.env.NEXT_PUBLIC_SENDNEX_LEGAL_ENTITY ?? "SendNex Ltd.";

export const SENDNEX_CONTACT_EMAIL =
  process.env.NEXT_PUBLIC_SENDNEX_CONTACT_EMAIL ?? "hello@sendnex.xyz";

/**
 * Optional registered postal address. Only rendered when populated via env var.
 * TODO: add registered postal address once company registration completes.
 */
export const SENDNEX_POSTAL_ADDRESS =
  process.env.NEXT_PUBLIC_SENDNEX_POSTAL_ADDRESS ?? "";
