# Sprint 2 Combined Gate Review (Staff Engineer + Principal Engineer)

**Reviewer:** Staff Engineer / Principal Engineer (Combined Gate 3+4)
**Date:** 2026-03-27
**Sprint:** Sprint 2 - Batch Send, Tracking, Webhooks, API Key Rotation

---

## Verdict: APPROVE WITH CONDITIONS

Sprint 2 delivers a solid set of features: batch email sending, open/click tracking with HMAC-signed tokens, SES webhook processing (bounce/complaint/delivery), API key rotation with 24h grace period, suppressions, domain soft-delete, email listing with rich filters, and template preview. The code quality is high -- structured logging via `LoggerMessage`, proper use of `AsNoTracking`, correct HMAC token validation with `CryptographicOperations.FixedTimeEquals`, and clean separation between the API layer and the webhook processor. However, there are three issues that must be addressed before production deployment.

---

## Critical Issues (must fix)

1. **SendBatchHandler rate limit loop is an N+1 Redis call (lines 37-42).** The handler calls `CheckRateLimitAsync` in a loop once per email in the batch. For a 100-email batch, this makes 100 sequential Redis calls to check the rate limit, and each call likely increments a counter. This means a batch of 100 consumes the entire rate limit budget in the check alone. Fix: call `CheckRateLimitAsync` once, passing the batch size as the requested increment, or check remaining budget in a single call and compare against `emailCount`.

2. **SendBatchHandler calls `SaveChangesAsync` inside the loop (line 118).** Each email in the batch triggers a separate database round-trip. For 100 emails, that is 100 INSERT round-trips plus 100 MassTransit publishes sequentially. Fix: batch the `_dbContext.Emails.Add()` and `_dbContext.EmailEvents.Add()` calls, then call `SaveChangesAsync` once after the loop (or in chunks of ~25). Publish messages after the save succeeds. This also prevents partial state if a mid-batch failure occurs -- currently, some emails are committed and published while later ones are not, leaving the batch in an inconsistent state.

3. **SNS signature validation is incomplete (SnsMessageHandler lines 133-143).** The handler only validates that `SigningCertUrl` is HTTPS and ends with `.amazonaws.com`. It does NOT verify the actual SNS message signature using the certificate. An attacker could forge SNS messages by pointing `SigningCertUrl` to their own `*.amazonaws.com` subdomain (or an attacker-controlled subdomain if DNS is misconfigured). Fix: implement full SNS signature verification per the AWS SNS documentation -- download the certificate from the URL, verify it chains to a trusted CA, and validate the message signature. Alternatively, use the `Amazon.SimpleNotificationService` SDK's `Message.IsMessageSignatureValid()` method.

---

## Recommendations (nice-to-have)

1. **SendBatchHandler suppression check is N+1 per recipient.** The `CheckSuppression` method queries Redis and potentially the database for each recipient in each email. For a batch with many recipients, consider pre-loading the suppression list for the tenant (or at least batch the DB query with an `IN` clause) to reduce round-trips.

2. **ClickTrackingLinkRewriter uses `AppDbContext` directly** rather than going through an interface. This is inconsistent with the rest of the architecture where services depend on `ICacheService`, `IEmailDeliveryService`, etc. Consider extracting an `ITrackingLinkRepository` if you want testability, though this is not blocking.

3. **ApiKeyAuthHandler caches rotating keys without TTL (line 77).** When a rotating key is validated from the DB, it gets cached via `SetApiKeyCacheAsync` with no explicit TTL. This means the cached entry could outlive the 24h grace period. The rotation handler correctly sets the 24h TTL for the old key's cache (line 71 in RotateApiKeyHandler), but the auth handler's re-cache on DB lookup does not. Consider setting a short TTL (e.g., 1h) on the auth handler's cache write, or checking `RotatingExpiresAt` before caching.

4. **SendRawEmailAsync copies the stream unnecessarily (SesEmailService lines 147-148).** The incoming `mimeMessage` stream is already a `MemoryStream` (from `BuildMimeMessage`). The method copies it into another `MemoryStream` before sending. You can check if the input is already a `MemoryStream` and skip the copy.

5. **Tracking pixel and click endpoints swallow all exceptions silently (WebhookProcessor Program.cs lines 104-107, 169-171).** While it is correct that tracking failures should not break the user experience, consider at minimum logging these exceptions so you can debug tracking data gaps in production.

6. **ListEmailsHandler tag filtering (lines 43-51) uses AND logic for multiple tags** (`foreach` applying successive `.Where` clauses). The variable name `tagList` and comma-separated input suggest users might expect OR logic (any tag matches). Clarify the API documentation or switch to OR with `query.Where(e => tagList.Any(t => e.Tags.Contains(t)))`.

7. **Migration script uses `varchar_pattern_ops` index (line 57)** for suppression email search, which only helps with left-anchored LIKE queries (`LIKE 'foo%'`). The `ListSuppressionsHandler` uses `ILike(s.EmailAddress, "%search%")` which is a middle-of-string search -- this index will not help. Consider a trigram GIN index (`gin_trgm_ops`) instead if you need partial match performance.

8. **Migration has a redundant index on `tracking_links.token` (line 73).** The `UNIQUE` constraint on line 69 already creates a unique index on the `token` column. The explicit index on line 73 is redundant.

---

## Ship Decision

**Ship after fixing the 3 critical issues.** Issues #1 and #2 are performance/correctness problems that will manifest under real batch load -- a 100-email batch will be slow and partially committed on failure. Issue #3 is a security gap that must be closed before the webhook endpoint is exposed to the internet. The recommendations are quality improvements that can be addressed in Sprint 3 without blocking the release.
