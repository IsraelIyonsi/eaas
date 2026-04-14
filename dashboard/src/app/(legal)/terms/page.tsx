import type { Metadata } from "next";
import { SENDNEX_LEGAL_ENTITY, SENDNEX_POSTAL_ADDRESS } from "@/lib/constants/legal";

export const metadata: Metadata = {
  title: "Terms of Service - SendNex",
  description: "Terms and conditions for using the SendNex platform.",
};

export default function TermsOfServicePage() {
  return (
    <>
      <h1>Terms of Service</h1>
      <p className="text-xs !text-muted-foreground/60">
        Last updated: April 2026
      </p>

      <h2 id="service-description">1. Service Description</h2>
      <p>
        SendNex (&quot;Email as a Service&quot;) is an API-based email sending and
        receiving platform. We provide infrastructure for developers and
        businesses to send transactional and marketing emails programmatically
        through our REST API and dashboard.
      </p>

      <h2 id="account-registration">2. Account Registration</h2>
      <p>To use SendNex, you must:</p>
      <ul>
        <li>
          Be at least <strong>18 years old</strong>
        </li>
        <li>
          Provide <strong>accurate and complete</strong> registration information
        </li>
        <li>
          Maintain <strong>one account per person or entity</strong>
        </li>
        <li>
          Keep your <strong>API keys and credentials secure</strong> — you are
          responsible for all activity under your account
        </li>
      </ul>
      <p>
        We reserve the right to suspend or terminate accounts that provide false
        information or violate these terms.
      </p>

      <h2 id="acceptable-use">3. Acceptable Use</h2>
      <p>
        You agree <strong>not</strong> to use SendNex to:
      </p>
      <ul>
        <li>
          <strong>Send spam</strong> — unsolicited bulk email without recipient
          consent
        </li>
        <li>
          <strong>Send phishing emails</strong> — messages designed to steal
          credentials or personal information
        </li>
        <li>
          <strong>Distribute malware</strong> — emails containing viruses,
          trojans, or other malicious software
        </li>
        <li>
          <strong>Send illegal content</strong> — anything that violates
          Nigerian law or the law of the recipient&apos;s jurisdiction
        </li>
        <li>
          <strong>Impersonate others</strong> — forge headers or misrepresent
          the sender identity
        </li>
        <li>
          <strong>Harvest addresses</strong> — scrape or collect email addresses
          without consent
        </li>
      </ul>
      <p>
        Violation of this policy may result in immediate account suspension
        without prior notice.
      </p>

      <h2 id="api-usage">4. API Usage</h2>
      <ul>
        <li>
          <strong>Rate limits</strong> apply to all API endpoints. Current limits
          are documented in the API docs and may change with notice.
        </li>
        <li>
          <strong>Fair use</strong> — do not use the API in a way that
          degrades the experience for other customers
        </li>
        <li>
          <strong>Authentication</strong> — all API requests must include a
          valid API key
        </li>
        <li>
          We may throttle or block requests that exceed rate limits or exhibit
          abusive patterns
        </li>
      </ul>

      <h2 id="payment-terms">5. Payment Terms</h2>
      <ul>
        <li>
          A <strong>free tier</strong> is available with limited monthly email
          volume
        </li>
        <li>
          Paid plans are <strong>billed monthly</strong> in advance
        </li>
        <li>
          Payments are processed through <strong>Paystack</strong> or{" "}
          <strong>Stripe</strong>
        </li>
        <li>
          Fees are <strong>non-refundable</strong> except where required by law
        </li>
        <li>
          We may change pricing with <strong>30 days notice</strong>
        </li>
        <li>
          Unpaid accounts may be suspended after 14 days past due
        </li>
      </ul>

      <h2 id="sla">6. Service Level</h2>
      <p>
        We target <strong>99.9% uptime</strong> for the SendNex API. During the
        beta period, this is a best-effort target, not a contractual guarantee.
        We do not offer SLA credits during beta.
      </p>
      <p>
        Planned maintenance will be announced at least 24 hours in advance
        through the dashboard and via email.
      </p>

      <h2 id="intellectual-property">7. Intellectual Property</h2>
      <ul>
        <li>
          <strong>Your content:</strong> You retain full ownership of all email
          content, templates, and data you upload or send through SendNex
        </li>
        <li>
          <strong>Our platform:</strong> SendNex, its logo, API, dashboard, and
          documentation are our intellectual property
        </li>
        <li>
          You grant us a limited license to process your content solely to
          provide the service
        </li>
      </ul>

      <h2 id="liability">8. Limitation of Liability</h2>
      <p>
        The SendNex service is provided <strong>&quot;as is&quot;</strong> during
        the beta period. To the maximum extent permitted by law:
      </p>
      <ul>
        <li>
          We are <strong>not liable</strong> for indirect, incidental, or
          consequential damages
        </li>
        <li>
          Our total liability is limited to the <strong>fees you paid</strong> in
          the 12 months preceding the claim
        </li>
        <li>
          We are <strong>not responsible</strong> for email delivery failures
          caused by recipient mail servers, DNS misconfiguration, or content
          filtering
        </li>
      </ul>

      <h2 id="termination">9. Termination</h2>
      <ul>
        <li>
          <strong>By you:</strong> You may close your account at any time
          through the dashboard settings
        </li>
        <li>
          <strong>By us:</strong> We may terminate your account for violation of
          these terms, with notice where practicable
        </li>
        <li>
          <strong>Effect of termination:</strong> Your data will be deleted{" "}
          <strong>30 days</strong> after account closure. You may request an
          export before then.
        </li>
      </ul>

      <h2 id="governing-law">10. Governing Law</h2>
      <p>
        These terms are governed by the laws of the{" "}
        <strong>Federal Republic of Nigeria</strong>.
      </p>

      <h2 id="dispute-resolution">11. Dispute Resolution</h2>
      <p>
        Any dispute arising from these terms shall first be submitted to{" "}
        <strong>binding arbitration in Lagos, Nigeria</strong> before pursuing
        litigation. Both parties agree to attempt good-faith resolution before
        initiating formal proceedings.
      </p>

      <h2 id="changes">12. Changes to These Terms</h2>
      <p>
        We may update these terms from time to time. We will provide at least{" "}
        <strong>30 days notice</strong> for material changes by email or through
        the dashboard. Continued use of the service after the notice period
        constitutes acceptance of the updated terms.
      </p>

      <h2 id="contact">13. Contact</h2>
      <p>For questions about these terms, contact us at:</p>
      <ul>
        <li>
          <strong>Email:</strong>{" "}
          <a href="mailto:legal@sendnex.xyz">legal@sendnex.xyz</a>
        </li>
        <li>
          <strong>Legal entity:</strong> {SENDNEX_LEGAL_ENTITY}
        </li>
        <li>
          <strong>Postal address:</strong>{" "}
          <span style={{ whiteSpace: "pre-line" }}>{SENDNEX_POSTAL_ADDRESS}</span>
        </li>
      </ul>
    </>
  );
}
