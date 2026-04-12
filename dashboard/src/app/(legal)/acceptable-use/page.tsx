import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Acceptable Use Policy - SendNex",
  description:
    "Rules governing the acceptable use of the SendNex email sending platform.",
};

export default function AcceptableUsePolicyPage() {
  return (
    <>
      <h1>Acceptable Use Policy</h1>
      <p className="text-xs !text-muted-foreground/60">
        Last updated: April 2026
      </p>

      <p>
        This Acceptable Use Policy (&quot;AUP&quot;) governs your use of the
        SendNex email sending platform. By using our service, you agree to comply
        with this policy. Violations may result in account suspension or
        termination.
      </p>

      <h2 id="purpose">1. Purpose</h2>
      <p>
        This policy exists to ensure that SendNex is used responsibly and legally,
        to protect the reputation and deliverability of our shared email
        infrastructure, and to comply with international anti-spam and data
        protection regulations.
      </p>

      <h2 id="prohibited-content">2. Prohibited Content</h2>
      <p>You may not use SendNex to send emails containing:</p>
      <ul>
        <li>
          <strong>Spam</strong> — unsolicited bulk commercial email
        </li>
        <li>
          <strong>Phishing</strong> — messages designed to deceive recipients
          into revealing personal information, credentials, or financial details
        </li>
        <li>
          <strong>Malware</strong> — attachments or links containing viruses,
          trojans, ransomware, or other malicious software
        </li>
        <li>
          <strong>Illegal content</strong> — content that violates any
          applicable law in the sender&apos;s or recipient&apos;s jurisdiction
        </li>
        <li>
          <strong>Hate speech</strong> — content that promotes violence or
          discrimination based on race, ethnicity, religion, gender, sexual
          orientation, disability, or any other protected characteristic
        </li>
        <li>
          <strong>Adult content</strong> — sexually explicit material, unless
          behind appropriate age verification mechanisms
        </li>
        <li>
          <strong>Pyramid or Ponzi schemes</strong> — fraudulent investment or
          multi-level marketing schemes
        </li>
        <li>
          <strong>Counterfeit goods</strong> — promotion of fake or unauthorized
          replicas of branded products
        </li>
      </ul>

      <h2 id="prohibited-activities">3. Prohibited Activities</h2>
      <p>You may not:</p>
      <ul>
        <li>
          <strong>Send to purchased or harvested lists</strong> — email
          addresses must be collected with the recipient&apos;s knowledge and
          consent
        </li>
        <li>
          <strong>Send without consent</strong> — all recipients must have
          opted in to receive your emails, either explicitly or through an
          existing business relationship
        </li>
        <li>
          <strong>Spoof sender identity</strong> — misrepresent the origin of
          emails by forging headers, using deceptive &quot;From&quot; names, or
          impersonating another person or organization
        </li>
        <li>
          <strong>Bypass unsubscribe mechanisms</strong> — re-add recipients who
          have opted out, or send to addresses that have previously unsubscribed
        </li>
        <li>
          <strong>Exceed rate limits</strong> — circumvent or attempt to
          circumvent API rate limits or sending quotas
        </li>
        <li>
          <strong>Reverse engineer the API</strong> — decompile, disassemble,
          or attempt to derive the source code of the SendNex platform
        </li>
        <li>
          <strong>Resell without authorization</strong> — redistribute or resell
          SendNex services to third parties without prior written consent
        </li>
      </ul>

      <h2 id="sending-requirements">4. Email Sending Requirements</h2>
      <p>All emails sent through SendNex must:</p>
      <ul>
        <li>
          <strong>Have recipient consent</strong> — recipients must have opted
          in to receive your messages (confirmed opt-in recommended)
        </li>
        <li>
          <strong>Include an unsubscribe mechanism</strong> — every commercial
          email must contain a clear, functioning unsubscribe link (CAN-SPAM
          &sect;7704(a)(3))
        </li>
        <li>
          <strong>Include sender physical address</strong> — a valid postal
          address of the sender must be included (CAN-SPAM &sect;7704(a)(5))
        </li>
        <li>
          <strong>Honor opt-outs</strong> — unsubscribe requests must be
          processed within <strong>10 business days</strong> (CAN-SPAM
          &sect;7704(a)(4))
        </li>
        <li>
          <strong>Maintain list hygiene</strong> — regularly remove hard bounces,
          invalid addresses, and unsubscribed recipients from your mailing lists
        </li>
        <li>
          <strong>Use accurate subject lines</strong> — subject lines must not
          be deceptive or misleading (CAN-SPAM &sect;7704(a)(2))
        </li>
      </ul>

      <h2 id="compliance">5. Compliance Requirements</h2>
      <p>
        You are responsible for ensuring your use of SendNex complies with all
        applicable laws in your jurisdiction and the jurisdictions of your
        recipients, including but not limited to:
      </p>
      <ul>
        <li>
          <strong>CAN-SPAM Act</strong> (United States) — 15 U.S.C. &sect;7701
          et seq.
        </li>
        <li>
          <strong>CASL</strong> (Canada) — Canada&apos;s Anti-Spam Legislation,
          S.C. 2010, c. 23
        </li>
        <li>
          <strong>GDPR</strong> (European Union) — Regulation (EU) 2016/679,
          particularly Art 6 (lawful basis) and Art 7 (consent)
        </li>
        <li>
          <strong>PECR</strong> (United Kingdom) — Privacy and Electronic
          Communications Regulations 2003
        </li>
        <li>
          <strong>Spam Act 2003</strong> (Australia) — including Schedule 1
          conditions for commercial electronic messages
        </li>
        <li>
          <strong>NDPA</strong> (Nigeria) — Nigeria Data Protection Act 2023
        </li>
        <li>
          All other applicable local, national, and international anti-spam and
          data protection laws
        </li>
      </ul>

      <h2 id="monitoring">6. Monitoring and Enforcement</h2>
      <p>
        SendNex monitors sending activity to protect our platform, our customers,
        and email recipients. We track the following metrics:
      </p>
      <ul>
        <li>
          <strong>Bounce rate</strong> — exceeding <strong>5%</strong> triggers
          an automated warning
        </li>
        <li>
          <strong>Complaint rate</strong> — exceeding <strong>0.1%</strong>{" "}
          triggers a manual review of your account
        </li>
        <li>
          <strong>Spam trap hits</strong> — any spam trap hit triggers an
          immediate review
        </li>
      </ul>
      <p>SendNex reserves the right to:</p>
      <ul>
        <li>
          <strong>Warn</strong> — notify you of a policy violation and request
          corrective action
        </li>
        <li>
          <strong>Throttle</strong> — reduce your sending rate to limit the
          impact of problematic sending
        </li>
        <li>
          <strong>Suspend</strong> — temporarily disable your sending
          capabilities pending investigation
        </li>
        <li>
          <strong>Terminate</strong> — permanently close your account for
          repeated or severe violations
        </li>
      </ul>

      <h2 id="consequences">7. Consequences of Violations</h2>
      <p>
        Violations of this AUP are handled through an escalating enforcement
        process:
      </p>
      <ol>
        <li>
          <strong>Warning</strong> — first violation or minor infraction. You
          will be notified and given an opportunity to correct the behavior.
        </li>
        <li>
          <strong>Throttling</strong> — continued or repeated minor violations.
          Your sending rate will be reduced.
        </li>
        <li>
          <strong>Suspension</strong> — serious violations or failure to
          address warnings. Your account will be temporarily disabled.
        </li>
        <li>
          <strong>Termination</strong> — repeated serious violations or failure
          to resolve suspended account issues.
        </li>
      </ol>
      <p>
        <strong>Severe violations</strong> — including phishing, malware
        distribution, or any activity that poses an immediate threat — will
        result in <strong>immediate termination without prior notice</strong>.
        No refund will be issued for accounts terminated due to AUP violations.
      </p>

      <h2 id="reporting">8. Reporting Abuse</h2>
      <p>
        If you believe someone is using SendNex in violation of this policy, please
        report it to:
      </p>
      <ul>
        <li>
          <strong>Email:</strong>{" "}
          <a href="mailto:abuse@sendnex.xyz">abuse@sendnex.xyz</a>
        </li>
      </ul>
      <p>
        We investigate all abuse reports and will respond within{" "}
        <strong>24 hours</strong>.
      </p>

      <h2 id="changes">9. Changes to This Policy</h2>
      <p>
        We may update this Acceptable Use Policy from time to time. We will
        provide at least <strong>30 days&apos; notice</strong> before material
        changes take effect, via email to active customers and by updating this
        page.
      </p>

      <h2 id="contact">10. Contact</h2>
      <p>
        For questions about this policy, contact us at:
      </p>
      <ul>
        <li>
          <strong>Email:</strong>{" "}
          <a href="mailto:legal@sendnex.xyz">legal@sendnex.xyz</a>
        </li>
        <li>
          <strong>Location:</strong> Lagos, Nigeria
        </li>
      </ul>
    </>
  );
}
