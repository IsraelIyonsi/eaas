import type { Metadata } from "next";
import Link from "next/link";

export const metadata: Metadata = {
  title: "Sub-Processor List - SendNex",
  description:
    "List of third-party sub-processors that process personal data on behalf of SendNex customers.",
};

export default function SubProcessorsPage() {
  return (
    <>
      <h1>Sub-Processor List</h1>
      <p className="text-xs !text-muted-foreground/60">
        Last updated: April 2026
      </p>

      <p>
        Under <strong>GDPR Article 28(2)</strong>, SendNex is required to maintain
        a list of sub-processors that process personal data on behalf of our
        customers. This page lists every third-party service provider that may
        process personal data as part of delivering the SendNex platform.
      </p>
      <p>
        For full details on how sub-processors are governed, see Section 8 of
        our{" "}
        <Link href="/dpa" className="text-primary hover:underline">
          Data Processing Agreement
        </Link>
        .
      </p>

      <h2 id="current-sub-processors">1. Current Sub-Processors</h2>

      <div className="overflow-x-auto">
        <table>
          <thead>
            <tr>
              <th>Sub-Processor</th>
              <th>Purpose</th>
              <th>Data Processed</th>
              <th>Location</th>
              <th>DPA Status</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td>
                <strong>Amazon Web Services (AWS)</strong>
              </td>
              <td>
                Cloud hosting, email delivery (SES), object storage (S3),
                database hosting
              </td>
              <td>All platform data</td>
              <td>eu-west-1 (Ireland)</td>
              <td>AWS DPA signed</td>
            </tr>
            <tr>
              <td>
                <strong>PayStack</strong>
              </td>
              <td>Payment processing (Africa)</td>
              <td>Customer name, email, payment details</td>
              <td>Nigeria</td>
              <td>PayStack DPA available</td>
            </tr>
            <tr>
              <td>
                <strong>Stripe</strong>
              </td>
              <td>Payment processing (International)</td>
              <td>Customer name, email, payment details</td>
              <td>US / EU</td>
              <td>Stripe DPA signed</td>
            </tr>
            <tr>
              <td>
                <strong>Flutterwave</strong>
              </td>
              <td>Payment processing (Africa)</td>
              <td>Customer name, email, payment details</td>
              <td>Nigeria / US</td>
              <td>Flutterwave DPA available</td>
            </tr>
            <tr>
              <td>
                <strong>PayPal</strong>
              </td>
              <td>Payment processing (Global)</td>
              <td>Customer name, email, payment details</td>
              <td>US / EU</td>
              <td>PayPal DPA signed</td>
            </tr>
          </tbody>
        </table>
      </div>

      <h2 id="non-sub-processors">2. Services That Are Not Sub-Processors</h2>
      <p>
        The following services are used internally by SendNex but do not process
        customer personal data:
      </p>
      <ul>
        <li>
          <strong>Grafana / Prometheus</strong> — monitoring and observability
          (self-hosted, no personal data shared)
        </li>
      </ul>

      <h2 id="change-notification">3. Notification of Changes</h2>
      <p>
        When SendNex intends to add or replace a sub-processor, we will:
      </p>
      <ul>
        <li>
          Send an <strong>email notification</strong> to all active customers at
          least <strong>30 days</strong> before the change takes effect
        </li>
        <li>
          Update this page with the new sub-processor details
        </li>
        <li>
          Provide a summary of the data processing activities the new
          sub-processor will perform
        </li>
      </ul>

      <h2 id="right-to-object">4. Right to Object</h2>
      <p>
        Under our{" "}
        <Link href="/dpa" className="text-primary hover:underline">
          Data Processing Agreement
        </Link>
        , customers have the right to object to the appointment of a new
        sub-processor. To exercise this right:
      </p>
      <ul>
        <li>
          Submit your objection in writing to{" "}
          <a href="mailto:privacy@sendnex.xyz">privacy@sendnex.xyz</a> within{" "}
          <strong>14 days</strong> of receiving the notification
        </li>
        <li>
          Include the specific reasons for your objection
        </li>
        <li>
          SendNex will work with you to find a reasonable alternative. If no
          alternative is available, either party may terminate the affected
          service
        </li>
      </ul>

      <h2 id="contact">5. Contact</h2>
      <p>
        For questions about our sub-processors or to receive notifications of
        changes, contact us at:
      </p>
      <ul>
        <li>
          <strong>Email:</strong>{" "}
          <a href="mailto:privacy@sendnex.xyz">privacy@sendnex.xyz</a>
        </li>
        <li>
          <strong>Location:</strong> Lagos, Nigeria
        </li>
      </ul>
    </>
  );
}
