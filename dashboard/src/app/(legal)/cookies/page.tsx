import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Cookie Policy - SendNex",
  description: "How SendNex uses cookies.",
};

export default function CookiePolicyPage() {
  return (
    <>
      <h1>Cookie Policy</h1>
      <p className="text-xs !text-muted-foreground/60">
        Last updated: April 2026
      </p>

      <h2 id="what-are-cookies">1. What Are Cookies</h2>
      <p>
        Cookies are small text files stored on your device by your web browser.
        They are widely used to make websites work and to provide information to
        site operators.
      </p>

      <h2 id="cookies-we-use">2. Cookies We Use</h2>
      <p>
        SendNex uses <strong>essential cookies only</strong>. We do not use
        tracking cookies, analytics cookies, or third-party cookies.
      </p>

      <table>
        <thead>
          <tr>
            <th>Cookie Name</th>
            <th>Purpose</th>
            <th>Type</th>
            <th>Expiry</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td>
              <code>eaas_session</code>
            </td>
            <td>
              Authenticates your session after login. Required for the dashboard
              to function.
            </td>
            <td>Essential, httpOnly, secure</td>
            <td>8 hours</td>
          </tr>
        </tbody>
      </table>

      <h2 id="no-tracking">3. No Tracking Cookies</h2>
      <p>We want to be clear about what we do <strong>not</strong> do:</p>
      <ul>
        <li>
          <strong>No analytics cookies</strong> — we do not use Google
          Analytics, Mixpanel, or similar services
        </li>
        <li>
          <strong>No advertising cookies</strong> — we do not serve ads or track
          you for advertising purposes
        </li>
        <li>
          <strong>No third-party cookies</strong> — no external services set
          cookies through our site
        </li>
        <li>
          <strong>No cross-site tracking</strong> — we do not track your
          activity on other websites
        </li>
      </ul>

      <h2 id="managing-cookies">4. Managing Cookies</h2>
      <p>
        You can manage cookies through your browser settings. Most browsers
        allow you to:
      </p>
      <ul>
        <li>View which cookies are stored</li>
        <li>Delete individual cookies or all cookies</li>
        <li>Block cookies from specific sites</li>
        <li>Block all cookies (note: this will prevent you from logging in)</li>
      </ul>
      <p>
        Since we only use essential cookies, blocking them will prevent you from
        using the SendNex dashboard. The API itself does not require cookies — it
        uses API key authentication.
      </p>

      <h2 id="changes">5. Changes to This Policy</h2>
      <p>
        If we ever add non-essential cookies, we will update this policy and
        request your consent before setting them.
      </p>

      <h2 id="contact">6. Contact</h2>
      <p>For questions about our cookie practices, contact us at:</p>
      <ul>
        <li>
          <strong>Email:</strong>{" "}
          <a href="mailto:privacy@sendnex.xyz">privacy@sendnex.xyz</a>
        </li>
      </ul>
    </>
  );
}
