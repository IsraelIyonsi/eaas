import type { Metadata } from "next";
import Link from "next/link";

export const metadata: Metadata = {
  title: "Data Processing Agreement - EaaS",
  description:
    "Data Processing Agreement between EaaS (Processor) and its customers (Controllers) under GDPR Article 28.",
};

export default function DpaPage() {
  return (
    <>
      <h1>Data Processing Agreement</h1>
      <p className="text-xs !text-muted-foreground/60">
        Last updated: April 2026
      </p>

      <p>
        This Data Processing Agreement (&quot;DPA&quot;) forms part of the
        agreement between you (&quot;Controller&quot;) and EaaS
        (&quot;Processor&quot;) for the provision of email sending services. This
        DPA is entered into pursuant to{" "}
        <strong>Article 28 of the General Data Protection Regulation</strong>{" "}
        (EU) 2016/679 (&quot;GDPR&quot;) and the{" "}
        <strong>Nigeria Data Protection Act 2023</strong> (&quot;NDPA&quot;).
      </p>

      <h2 id="definitions">1. Definitions</h2>
      <p>In this DPA, the following terms have these meanings:</p>
      <ul>
        <li>
          <strong>Controller</strong> — the entity that determines the purposes
          and means of processing personal data (you, the EaaS customer).
        </li>
        <li>
          <strong>Processor</strong> — the entity that processes personal data on
          behalf of the Controller (EaaS).
        </li>
        <li>
          <strong>Data Subject</strong> — an identified or identifiable natural
          person whose personal data is processed.
        </li>
        <li>
          <strong>Personal Data</strong> — any information relating to a Data
          Subject, as defined in GDPR Art 4(1).
        </li>
        <li>
          <strong>Processing</strong> — any operation performed on personal data,
          including collection, storage, transmission, and deletion.
        </li>
        <li>
          <strong>Sub-processor</strong> — a third party engaged by the Processor
          to process personal data on behalf of the Controller.
        </li>
      </ul>

      <h2 id="subject-matter">2. Subject Matter and Duration</h2>
      <p>
        This DPA governs the processing of personal data by EaaS when providing
        its API-based email sending, receiving, and tracking services to the
        Controller. The duration of processing corresponds to the term of the
        underlying service agreement between the parties, plus any retention
        period specified in Section 12.
      </p>

      <h2 id="nature-purpose">3. Nature and Purpose of Processing</h2>
      <p>
        EaaS processes personal data solely for the purpose of providing email
        services to the Controller. This includes:
      </p>
      <ul>
        <li>
          <strong>Sending emails</strong> on behalf of the Controller via the
          EaaS API
        </li>
        <li>
          <strong>Receiving inbound emails</strong> routed through Controller
          domains
        </li>
        <li>
          <strong>Tracking delivery events</strong> (delivered, bounced,
          complained, opened, clicked) when enabled by the Controller
        </li>
        <li>
          <strong>Storing email metadata and content</strong> for the retention
          period
        </li>
        <li>
          <strong>Processing attachments</strong> transmitted through the service
        </li>
      </ul>

      <h2 id="types-of-data">4. Types of Personal Data Processed</h2>
      <ul>
        <li>
          <strong>Email addresses</strong> — sender and recipient addresses (to,
          cc, bcc)
        </li>
        <li>
          <strong>Names</strong> — sender and recipient display names
        </li>
        <li>
          <strong>Email content</strong> — subject lines, message bodies (HTML
          and plain text)
        </li>
        <li>
          <strong>Attachment content</strong> — files transmitted with emails
        </li>
        <li>
          <strong>IP addresses</strong> — of API callers and email recipients
          (when tracking is enabled)
        </li>
        <li>
          <strong>Tracking data</strong> — open timestamps, click timestamps, and
          URLs clicked
        </li>
        <li>
          <strong>Custom metadata</strong> — tags and headers provided by the
          Controller
        </li>
      </ul>

      <h2 id="data-subjects">5. Categories of Data Subjects</h2>
      <ul>
        <li>
          <strong>Controller&apos;s customers</strong> — recipients of emails
          sent through EaaS
        </li>
        <li>
          <strong>Email recipients</strong> — any individual whose email address
          is processed
        </li>
        <li>
          <strong>Controller&apos;s employees</strong> — individuals who use the
          EaaS dashboard or API
        </li>
      </ul>

      <h2 id="controller-obligations">6. Controller&apos;s Obligations</h2>
      <p>The Controller shall:</p>
      <ul>
        <li>
          Ensure a <strong>lawful basis</strong> exists for processing personal
          data through EaaS (GDPR Art 6)
        </li>
        <li>
          Ensure the <strong>accuracy</strong> of personal data provided to EaaS
        </li>
        <li>
          Provide <strong>documented instructions</strong> to EaaS regarding the
          processing of personal data
        </li>
        <li>
          Comply with the{" "}
          <Link
            href="/acceptable-use"
            className="text-primary hover:underline"
          >
            Acceptable Use Policy
          </Link>{" "}
          and all applicable data protection laws
        </li>
        <li>
          Respond to Data Subject requests and inform EaaS where assistance is
          required
        </li>
      </ul>

      <h2 id="processor-obligations">7. Processor&apos;s Obligations</h2>

      <h3 id="instructions">7.1 Processing on Instructions</h3>
      <p>
        EaaS shall process personal data only on documented instructions from
        the Controller (GDPR Art 28(3)(a)), unless required to do so by
        applicable law. If such a legal requirement arises, EaaS will inform
        the Controller before processing, unless the law prohibits such
        notification.
      </p>

      <h3 id="confidentiality">7.2 Confidentiality</h3>
      <p>
        EaaS ensures that all personnel authorized to process personal data have
        committed to confidentiality obligations or are under an appropriate
        statutory obligation of confidentiality (GDPR Art 28(3)(b)).
      </p>

      <h3 id="security-measures">7.3 Security Measures</h3>
      <p>
        EaaS implements appropriate technical and organizational measures to
        ensure a level of security appropriate to the risk (GDPR Art 32),
        including:
      </p>
      <ul>
        <li>
          <strong>Encryption in transit</strong> — TLS 1.3 for all API and email
          communications
        </li>
        <li>
          <strong>Encryption at rest</strong> — AES-256 via AWS managed
          encryption
        </li>
        <li>
          <strong>Access controls</strong> — role-based access with
          least-privilege principles
        </li>
        <li>
          <strong>Password hashing</strong> — BCrypt with salted hashes
        </li>
        <li>
          <strong>API key security</strong> — SHA-256 hashed, never stored in
          plain text
        </li>
        <li>
          <strong>Session security</strong> — HMAC-SHA256 signed session tokens
        </li>
        <li>
          <strong>Pseudonymization</strong> — where technically feasible
        </li>
      </ul>

      <h3 id="sub-processors">7.4 Sub-Processors</h3>
      <p>
        EaaS shall not engage another processor without prior written
        authorization from the Controller (GDPR Art 28(2)). EaaS maintains a
        list of approved sub-processors at our{" "}
        <Link
          href="/sub-processors"
          className="text-primary hover:underline"
        >
          Sub-Processor List
        </Link>
        . The same data protection obligations set out in this DPA are imposed
        on each sub-processor by way of contract (GDPR Art 28(4)).
      </p>

      <h3 id="data-subject-requests">7.5 Data Subject Requests</h3>
      <p>
        EaaS shall assist the Controller in fulfilling its obligations to
        respond to Data Subject requests (GDPR Art 28(3)(e)), including requests
        for access, rectification, erasure, and data portability. EaaS will
        respond to Controller requests for assistance within{" "}
        <strong>72 hours</strong>.
      </p>

      <h3 id="dpia">7.6 Data Protection Impact Assessments</h3>
      <p>
        EaaS shall assist the Controller with data protection impact assessments
        and prior consultation with supervisory authorities where required (GDPR
        Art 28(3)(f), Art 35, Art 36).
      </p>

      <h3 id="deletion">7.7 Deletion or Return of Data</h3>
      <p>
        Upon termination of the service agreement, EaaS shall, at the
        Controller&apos;s choice, delete or return all personal data within{" "}
        <strong>30 days</strong> and delete existing copies, unless applicable
        law requires further storage (GDPR Art 28(3)(g)).
      </p>

      <h3 id="audit">7.8 Audit and Compliance</h3>
      <p>
        EaaS shall make available to the Controller all information necessary to
        demonstrate compliance with GDPR Art 28 obligations and allow for and
        contribute to audits, including inspections, conducted by the Controller
        or another auditor mandated by the Controller (GDPR Art 28(3)(h)).
      </p>

      <h2 id="sub-processing">8. Sub-Processing</h2>
      <p>
        A current list of approved sub-processors is maintained at{" "}
        <Link
          href="/sub-processors"
          className="text-primary hover:underline"
        >
          eaas.dev/sub-processors
        </Link>
        .
      </p>
      <ul>
        <li>
          EaaS will provide <strong>30 days&apos; written notice</strong> before
          adding or replacing a sub-processor
        </li>
        <li>
          The Controller has the <strong>right to object</strong> to a new
          sub-processor within 14 days of notification
        </li>
        <li>
          If the Controller objects and no reasonable alternative is available,
          either party may terminate the affected service
        </li>
      </ul>

      <h2 id="data-transfers">9. International Data Transfers</h2>
      <p>
        EaaS is based in Lagos, Nigeria. Personal data may be transferred to and
        processed in the following locations:
      </p>
      <ul>
        <li>
          <strong>AWS eu-west-1 (Ireland)</strong> — primary infrastructure
          region within the EU
        </li>
        <li>
          <strong>Nigeria</strong> — EaaS operational base
        </li>
      </ul>
      <p>
        For transfers from the EU/EEA to Nigeria, EaaS relies on{" "}
        <strong>Standard Contractual Clauses</strong> (SCCs) as approved by the
        European Commission (GDPR Art 46(2)(c)). For transfers to the US (where
        sub-processors are located), the applicable sub-processor&apos;s own
        transfer mechanisms apply (e.g., EU-US Data Privacy Framework).
      </p>

      <h2 id="security">10. Security Measures</h2>

      <h3 id="technical-measures">10.1 Technical Measures</h3>
      <ul>
        <li>TLS 1.3 encryption for all data in transit</li>
        <li>AES-256 encryption for all data at rest (AWS managed keys)</li>
        <li>BCrypt password hashing with unique salts</li>
        <li>SHA-256 hashing for API keys</li>
        <li>HMAC-SHA256 signed session tokens</li>
        <li>Rate limiting and abuse detection</li>
        <li>Automated vulnerability scanning</li>
      </ul>

      <h3 id="organizational-measures">10.2 Organizational Measures</h3>
      <ul>
        <li>Role-based access control with least-privilege principles</li>
        <li>Comprehensive audit logging of all administrative actions</li>
        <li>Documented incident response plan</li>
        <li>Regular security reviews and testing</li>
        <li>Confidentiality agreements for all personnel</li>
      </ul>

      <h2 id="breach-notification">11. Data Breach Notification</h2>
      <p>
        In the event of a personal data breach (GDPR Art 33), EaaS shall:
      </p>
      <ul>
        <li>
          Notify the Controller <strong>within 48 hours</strong> of becoming
          aware of the breach (stricter than GDPR&apos;s 72-hour requirement)
        </li>
        <li>
          Provide the following information:
          <ul>
            <li>Nature of the breach</li>
            <li>Categories and approximate number of Data Subjects affected</li>
            <li>Categories of personal data records affected</li>
            <li>Likely consequences of the breach</li>
            <li>Measures taken or proposed to address the breach</li>
            <li>Measures taken to mitigate possible adverse effects</li>
          </ul>
        </li>
        <li>
          Cooperate with the Controller in notifying the relevant supervisory
          authority and affected Data Subjects where required
        </li>
      </ul>

      <h2 id="retention">12. Data Retention and Deletion</h2>
      <ul>
        <li>
          <strong>Email logs and tracking data:</strong> retained for{" "}
          <strong>90 days</strong>, then automatically purged
        </li>
        <li>
          <strong>Account data:</strong> retained until termination of the
          service agreement, plus <strong>30 days</strong> for data export
        </li>
        <li>
          <strong>Backups:</strong> purged within <strong>60 days</strong> of
          data deletion from production systems
        </li>
        <li>
          <strong>Payment records:</strong> retained as required by Nigerian tax
          law (minimum 6 years under FIRS regulations)
        </li>
      </ul>

      <h2 id="audit-rights">13. Audit Rights</h2>
      <ul>
        <li>
          The Controller may audit EaaS&apos;s compliance with this DPA{" "}
          <strong>once per year</strong> with at least <strong>30 days</strong>{" "}
          prior written notice
        </li>
        <li>
          Audits shall be conducted during normal business hours and shall not
          unreasonably interfere with EaaS&apos;s operations
        </li>
        <li>
          The Controller bears the cost of the audit unless a material breach is
          discovered
        </li>
        <li>
          As an alternative to on-site audits, EaaS may provide a{" "}
          <strong>SOC 2 Type II report</strong> or equivalent third-party
          certification
        </li>
      </ul>

      <h2 id="liability">14. Liability</h2>
      <ul>
        <li>
          Each party shall be liable for damage caused by processing that
          infringes the GDPR (Art 82)
        </li>
        <li>
          The Processor&apos;s liability under this DPA shall not exceed{" "}
          <strong>12 months of fees</strong> paid by the Controller under the
          service agreement
        </li>
        <li>
          This limitation does not apply to liability arising from willful
          misconduct or gross negligence
        </li>
      </ul>

      <h2 id="term">15. Term and Termination</h2>
      <ul>
        <li>
          This DPA is effective for the duration of the underlying service
          agreement
        </li>
        <li>
          Obligations relating to data deletion (Section 12) and
          confidentiality (Section 7.2) survive termination
        </li>
        <li>
          Either party may terminate this DPA if the other party materially
          breaches its obligations and fails to cure within 30 days of written
          notice
        </li>
      </ul>

      <h2 id="governing-law">16. Governing Law</h2>
      <p>
        This DPA is governed by the laws of the{" "}
        <strong>Federal Republic of Nigeria</strong>. Where the Controller or
        Data Subjects are located in the EU/EEA, the provisions of GDPR shall
        apply to the processing of their personal data, and disputes relating
        to GDPR compliance may be brought before the competent courts of the
        relevant EU member state.
      </p>

      <h2 id="contact">17. Contact</h2>
      <p>
        For questions about this DPA or to exercise any rights under it, contact
        us at:
      </p>
      <ul>
        <li>
          <strong>Email:</strong>{" "}
          <a href="mailto:privacy@eaas.dev">privacy@eaas.dev</a>
        </li>
        <li>
          <strong>Location:</strong> Lagos, Nigeria
        </li>
      </ul>
    </>
  );
}
