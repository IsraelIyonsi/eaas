/**
 * Legal entity + postal address shown on every public-facing legal page.
 * Required by CAN-SPAM Act §7704(a)(5) and GDPR Art 13(1)(a).
 *
 * Values are read from public env vars so deployments can override without a
 * code change. Defaults are placeholders — the DEPLOY README warns that these
 * MUST be populated with the real registered entity name and physical address
 * before production use.
 */
export const SENDNEX_LEGAL_ENTITY =
  process.env.NEXT_PUBLIC_SENDNEX_LEGAL_ENTITY ?? "SendNex Ltd., [ADDRESS REQUIRED]";

export const SENDNEX_POSTAL_ADDRESS =
  process.env.NEXT_PUBLIC_SENDNEX_POSTAL_ADDRESS ?? "[Postal address pending registration]";
