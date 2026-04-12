"use client";

import { PageHeader } from "@/components/shared/page-header";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Book, Code, Webhook, Rocket, ArrowRight, FlaskConical } from "lucide-react";
import Link from "next/link";
import { Routes } from "@/lib/constants/routes";

const sections = [
  {
    title: "API Reference",
    description: "Complete REST API documentation with request/response examples for all endpoints.",
    icon: Code,
    href: Routes.DOCS_SANDBOX,
    badge: "10 endpoints",
    items: [
      "POST /api/v1/emails/send — Send email",
      "GET /api/v1/emails — List sent emails",
      "POST /api/v1/inbound/rules — Create routing rule",
      "GET /api/v1/inbound/emails — List received emails",
      "GET /api/v1/analytics/summary — Email metrics",
    ],
  },
  {
    title: "Webhook Integration",
    description: "Set up webhooks to receive real-time notifications when emails are sent, delivered, bounced, or received.",
    icon: Webhook,
    href: Routes.WEBHOOKS,
    badge: "9 event types",
    items: [
      "Payload schema with field reference",
      "HMAC-SHA256 signature verification",
      "Idempotency and retry handling",
      "Code examples in Node.js, Python, C#, Go",
      "Testing with ngrok",
    ],
  },
  {
    title: "Inbound Email Setup",
    description: "Configure your domain to receive inbound emails, set up routing rules, and process incoming mail.",
    icon: Rocket,
    href: Routes.INBOUND_RULES,
    badge: "5-step guide",
    items: [
      "MX record configuration",
      "Domain verification",
      "Routing rules (webhook, forward, store)",
      "Reply tracking via In-Reply-To headers",
      "Webhook payload for email.received",
    ],
  },
  {
    title: "Getting Started",
    description: "Quick start guide to send your first email in under 5 minutes.",
    icon: Book,
    href: Routes.DOCS_SANDBOX,
    badge: "5 min quickstart",
    items: [
      "Create an API key",
      "Verify your sending domain",
      "Send your first email via API",
      "Set up delivery webhooks",
      "Monitor with the dashboard",
    ],
  },
  {
    title: "API Sandbox",
    description: "Test endpoints directly from your browser with the interactive API playground.",
    icon: FlaskConical,
    href: Routes.DOCS_SANDBOX,
    badge: "interactive",
    items: [
      "Select from all available endpoints",
      "Build requests with query params and JSON bodies",
      "View formatted responses with status codes",
      "Pre-filled sample bodies for POST/PUT endpoints",
      "No API key needed — uses your session",
    ],
  },
];

export default function DocsPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Documentation"
        description="Guides, API reference, and integration tutorials for SendNex."
      />

      <div className="grid gap-4 md:grid-cols-2">
        {sections.map((section) => {
          const Icon = section.icon;
          return (
            <Card key={section.title} className="border-border bg-card hover:border-primary/30 transition-colors">
              <CardHeader className="pb-3">
                <div className="flex items-start justify-between">
                  <div className="flex items-center gap-3">
                    <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary/10">
                      <Icon className="h-4 w-4 text-primary" />
                    </div>
                    <div>
                      <CardTitle className="text-base font-semibold text-foreground">
                        {section.title}
                      </CardTitle>
                    </div>
                  </div>
                  <Badge variant="outline" className="text-[10px] text-muted-foreground">
                    {section.badge}
                  </Badge>
                </div>
                <p className="text-sm text-muted-foreground mt-2">
                  {section.description}
                </p>
              </CardHeader>
              <CardContent className="pt-0">
                <ul className="space-y-1.5 mb-4">
                  {section.items.map((item) => (
                    <li key={item} className="flex items-center gap-2 text-xs text-muted-foreground">
                      <span className="h-1 w-1 rounded-full bg-primary/40 shrink-0" />
                      {item}
                    </li>
                  ))}
                </ul>
                <Link
                  href={section.href}
                  className="inline-flex items-center gap-1.5 text-xs font-medium text-primary hover:text-primary/80 transition-colors"
                >
                  View details
                  <ArrowRight className="h-3 w-3" />
                </Link>
              </CardContent>
            </Card>
          );
        })}
      </div>

      <Card className="border-border bg-card">
        <CardContent className="py-6">
          <div className="text-center space-y-2">
            <p className="text-sm text-muted-foreground">
              Need help? Check the{" "}
              <Link href={Routes.SETTINGS} className="text-primary hover:underline">
                settings page
              </Link>{" "}
              for API configuration or contact support.
            </p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
