/**
 * Returns the secure cookie flag based on SECURE_COOKIES env var.
 * Must be consistent across all auth routes — a cookie set with Secure=true
 * cannot be cleared by a response with Secure=false.
 */
export function getSecureCookieFlag(): boolean {
  return process.env.SECURE_COOKIES === "true";
}
