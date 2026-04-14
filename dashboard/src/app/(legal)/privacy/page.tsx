import type { Metadata } from "next";
import Link from "next/link";
import {
  SENDNEX_CONTACT_EMAIL,
  SENDNEX_LEGAL_ENTITY,
  SENDNEX_POSTAL_ADDRESS,
} from "@/lib/constants/legal";

export const metadata: Metadata = {
  title: "Privacy Policy — SendNex",
  description:
    "How SendNex collects, uses, retains, and protects personal data under the NDPR and GDPR.",
};

export default function PrivacyPolicyPage() {
  return (
    <>
      <h1>Privacy Policy</h1>
      <p className="text-xs !text-muted-foreground/60">
        Last updated: April 2026
      </p>

      <h2 id="introduction">1. Introduction</h2>
      <p>
        SendNex (&quot;Email as a Service&quot;) is an API-based email sending
        platform operated from Lagos, Nigeria. This privacy policy explains how
        we collect, use, store, and protect your personal data when you use our
        service.
      </p>
      <p>
        We are committed to complying with the{" "}
        <strong>Nigeria Data Protection Regulation (NDPR)</strong> and the{" "}
        <strong>EU General Data Protection Regulation (GDPR)</strong> where
        applicable.
      </p>

      <h2 id="data-we-collect">2. Data We Collect</h2>

      <h3 id="account-information">2.1 Account Information</h3>
      <p>When you create an account, we collect:</p>
      <ul>
        <li>
          <strong>Name</strong> and <strong>email address</strong>
        </li>
        <li>
          <strong>Company name</strong> (optional)
        </li>
        <li>
          <strong>Password</strong> (stored as a salted hash, never in plain
          text)
        </li>
      </ul>

      <h3 id="email-data">2.2 Email Data</h3>
      <p>When you send emails through our API, we process:</p>
      <ul>
        <li>
          <strong>Email content</strong> (subject, body, attachments)
        </li>
        <li>
          <strong>Recipient addresses</strong> (to, cc, bcc)
        </li>
        <li>
          <strong>Sender addresses</strong> and display names
        </li>
        <li>
          <strong>Custom headers</strong> and metadata you provide
        </li>
      </ul>

      <h3 id="tracking-data">2.3 Tracking Data</h3>
      <p>If you enable tracking, we collect:</p>
      <ul>
        <li>
          <strong>Open events</strong> (timestamp, approximate location)
        </li>
        <li>
          <strong>Click events</strong> (timestamp, URL clicked)
        </li>
        <li>
          <strong>Delivery status</strong> (delivered, bounced, complained)
        </li>
      </ul>

      <h3 id="usage-logs">2.4 API Usage Logs</h3>
      <p>We log API requests for security and debugging purposes:</p>
      <ul>
        <li>API key used (masked)</li>
        <li>Request timestamp and endpoint</li>
        <li>Response status code</li>
        <li>IP address</li>
      </ul>

      <h3 id="payment-information">2.5 Payment Information</h3>
      <p>
        Payment processing is handled by <strong>Paystack</strong> and{" "}
        <strong>Stripe</strong>. We do not store your full credit card number.
        We receive only a payment token, last four digits, and transaction
        status from the payment processor.
      </p>

      <h2 id="why-we-collect">3. Why We Collect Your Data</h2>
      <p>We use your data to:</p>
      <ul>
        <li>
          <strong>Provide the service</strong> — send, receive, and track emails
          on your behalf
        </li>
        <li>
          <strong>Manage your account</strong> — authentication, billing, and
          support
        </li>
        <li>
          <strong>Improve reliability</strong> — monitor delivery rates, detect
          abuse, and debug issues
        </li>
        <li>
          <strong>Comply with law</strong> — respond to legal requests and
          prevent fraud
        </li>
      </ul>

      <h2 id="data-processor-role">4. Our Role as Data Processor</h2>
      <p>
        When you send emails through SendNex, <strong>you are the data controller</strong>{" "}
        and <strong>we are the data processor</strong>. We process email content
        and recipient data solely on your behalf and according to your
        instructions. We do not use your email content for our own purposes.
      </p>

      <h2 id="data-retention">5. Data Retention</h2>
      <ul>
        <li>
          <strong>Email logs and tracking data:</strong> retained for{" "}
          <strong>90 days</strong>, then automatically deleted
        </li>
        <li>
          <strong>Account data:</strong> retained until you delete your account
        </li>
        <li>
          <strong>API usage logs:</strong> retained for <strong>90 days</strong>
        </li>
        <li>
          <strong>Payment records:</strong> retained as required by Nigerian tax
          law (minimum 6 years)
        </li>
      </ul>

      <h2 id="third-parties">6. Third-Party Services</h2>
      <p>We share data with these service providers as necessary to operate:</p>
      <ul>
        <li>
          <strong>AWS SES</strong> — email delivery infrastructure (data
          processed in AWS regions)
        </li>
        <li>
          <strong>Paystack / Stripe</strong> — payment processing
        </li>
        <li>
          <strong>Grafana / Prometheus</strong> — internal monitoring and
          observability (no personal data shared)
        </li>
      </ul>
      <p>
        We do not sell your data to third parties. We do not share your data
        with advertisers.
      </p>

      <h2 id="your-rights">7. Your Rights</h2>

      <h3 id="gdpr-rights">7.1 GDPR Rights (EU/EEA Users)</h3>
      <p>If you are in the EU or EEA, you have the right to:</p>
      <ul>
        <li>
          <strong>Access</strong> — request a copy of the data we hold about you
        </li>
        <li>
          <strong>Rectification</strong> — correct inaccurate data
        </li>
        <li>
          <strong>Erasure</strong> — request deletion of your data
        </li>
        <li>
          <strong>Portability</strong> — receive your data in a structured,
          machine-readable format
        </li>
        <li>
          <strong>Objection</strong> — object to processing of your data
        </li>
        <li>
          <strong>Restriction</strong> — request we limit processing
        </li>
      </ul>

      <h3 id="ndpr-rights">7.2 NDPR Rights (Nigerian Users)</h3>
      <p>
        Under the Nigeria Data Protection Regulation, you have equivalent rights
        to access, rectify, and delete your personal data. You may also withdraw
        consent at any time.
      </p>

      <p>
        To exercise any of these rights, email us at{" "}
        <a href="mailto:privacy@sendnex.xyz">privacy@sendnex.xyz</a>. We will respond
        within 30 days.
      </p>

      <h2 id="data-security">8. Data Security</h2>
      <p>We protect your data with:</p>
      <ul>
        <li>Encryption in transit (TLS 1.2+)</li>
        <li>Encryption at rest for stored data</li>
        <li>API key authentication with HMAC-signed session tokens</li>
        <li>Role-based access control</li>
        <li>Regular security audits</li>
      </ul>

      <h2 id="cookies">9. Cookies</h2>
      <p>
        We use only essential cookies required for the service to function. See
        our{" "}
        <Link href="/cookies" className="text-primary hover:underline">
          Cookie Policy
        </Link>{" "}
        for details.
      </p>

      <h2 id="children">10. Children</h2>
      <p>
        SendNex is not intended for use by anyone under the age of 18. We do not
        knowingly collect data from children.
      </p>

      <h2 id="changes">11. Changes to This Policy</h2>
      <p>
        We may update this policy from time to time. We will notify you of
        material changes by email or through the dashboard at least 30 days
        before they take effect.
      </p>

      <h2 id="contact">12. Contact</h2>
      <p>
        For privacy-related questions or requests, contact us at:
      </p>
      <ul>
        <li>
          <strong>Email:</strong>{" "}
          <a href={`mailto:${SENDNEX_CONTACT_EMAIL}`}>{SENDNEX_CONTACT_EMAIL}</a>
        </li>
        <li>
          <strong>Legal entity:</strong> {SENDNEX_LEGAL_ENTITY}
        </li>
        {SENDNEX_POSTAL_ADDRESS ? (
          <li>
            <strong>Postal address:</strong>{" "}
            <span style={{ whiteSpace: "pre-line" }}>{SENDNEX_POSTAL_ADDRESS}</span>
          </li>
        ) : null}
        {/* TODO: add registered postal address once company registration completes */}
      </ul>
    </>
  );
}
