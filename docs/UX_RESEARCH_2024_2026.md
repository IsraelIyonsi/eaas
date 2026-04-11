# UX/UI Design Research Report: Email Service Dashboard & Developer Tools (2024-2026)

**Date:** 2026-04-01
**Purpose:** Design inspiration and pattern reference for EaaS dashboard UX team
**Scope:** Modern design patterns from 2024-2026 only

---

## Table of Contents

1. [Modern Dashboard Design Patterns](#1-modern-dashboard-design-patterns)
2. [Onboarding / Setup Wizard Patterns](#2-onboarding--setup-wizard-patterns)
3. [Rules / Configuration UI Patterns](#3-rules--configuration-ui-patterns)
4. [Developer Documentation Design](#4-developer-documentation-design)
5. [Analytics Dashboard Patterns](#5-analytics-dashboard-patterns)
6. [Design System & Component Trends](#6-design-system--component-trends)
7. [Email-Specific UI Patterns](#7-email-specific-ui-patterns)
8. [Recommended Tech Stack](#8-recommended-tech-stack)
9. [Reference Links & Inspiration Sources](#9-reference-links--inspiration-sources)

---

## 1. Modern Dashboard Design Patterns

### 1.1 The "Linear-Style" Interface (Dominant Trend 2024-2026)

Linear has become THE reference point for modern SaaS dashboard design. Their 2024 redesign established patterns now copied across the industry:

**Core Principles:**
- **Dark-first theming** with warm gray tones (moved away from cold blue-ish hues)
- **Monochrome black/white** with very few bold accent colors
- **4px spacing unit** for consistent rhythm
- **Inter / Inter Display typography** (the de facto SaaS typeface)
- **~200ms ease-out animations** for all transitions
- **Optimistic updates** with inline feedback (no loading spinners blocking UI)
- **1px panel separators** -- ultra-thin dividers, not heavy borders

**Keyboard-First Design:**
- `Cmd+K` command palette with fuzzy search (now table stakes for dev tools)
- `G + letter` navigation shortcuts (e.g., G+I = Inbox, G+B = Backlog)
- `C` to create, `E` to edit, `X` to select, `Esc` to go back
- Every single action accessible without a mouse
- Multiple access patterns: buttons, shortcuts, context menus, and command palette all lead to the same actions

**Visual Hierarchy:**
- Reduced visual noise: tabs, headers, filters, and panels adjusted for clarity
- Current view, available actions, and meta properties presented with clear hierarchy
- Density-appropriate layouts (no wasted whitespace, but not cramped)

### 1.2 Resend's Dashboard Philosophy

Resend ("Email for developers") represents the gold standard for email service DX:

**Key Patterns:**
- **Emails page as home** -- users land directly on their email list, no server setup required first
- **Stripped-down interface** -- removes unnecessary UI layers, focuses on API-based workflows
- **Clear DNS status indicators** -- each DNS record shows verification status with clear badges
- **Actionable error messages** -- when something is misconfigured, the dashboard tells you exactly what and how to fix it
- **React Email integration** -- visual email builder using React components
- **Regional sending visibility** -- US/EU data center selection is prominent

### 1.3 Postmark's Dashboard Design

Postmark represents clean, functional email monitoring:

**Key Patterns:**
- **Color-coded status system:** Green = delivered, Yellow = opened, Red = failed
- **Server + Message Streams architecture** visible in UI (transactional, broadcast, inbound)
- **45 days of email activity** with content previews
- **Detailed timestamps** showing delivery duration for each email
- **Purposeful, focused interface** -- no promotional add-ons or clutter
- **Fast task completion** -- template editing, webhook config take seconds

### 1.4 Vercel's Dashboard Patterns

Vercel's 2024-2025 dashboard redesign exemplifies B2D (Business-to-Developer) design:

**Key Principles:**
- **Speed over aesthetics** -- no fancy animations unless they add clarity
- **Friction reduction** -- developers are "allergic to bad UX"
- **Respects terminal/GitHub workflows** -- acknowledges devs live across multiple tools
- **Sidebar navigation** -- streamlines project/deployment navigation
- **Simplify without hiding** -- advanced capabilities remain accessible
- **shadcn/ui + Tailwind CSS** as the component foundation

---

## 2. Onboarding / Setup Wizard Patterns

### 2.1 DNS/Domain Setup Wizards (2024-2026 Best Practices)

**Amazon SES (March 2024 Update):**
- Guided onboarding wizard for meeting Gmail/Yahoo 2024 authentication requirements
- Step-by-step: verify sending identity -> custom MAIL FROM domain -> publish DMARC record
- Contextual guidance at each step explaining WHY each record matters

**Modern Wizard Design Principles:**
- **Tailored instructions** per domain provider (GoDaddy, Cloudflare, Namecheap, etc.)
- **Save & Resume** -- users can leave and come back without losing progress
- **Proactive troubleshooting** -- if DNS records aren't verified, provide clear explanations
- **Embedded checklists** inside the flow (not floating on top of the dashboard)
- **Contextual onboarding** beats traditional wizards -- guide users in context of what they're doing

### 2.2 Copy-to-Clipboard Patterns for DNS Records

This is critical for email service onboarding:

**Best Practices:**
- **Read-only clipboard fields** with prominent copy button for DNS record values
- **Expandable clipboard** for long TXT records (DKIM keys are long)
- **Visual feedback** on copy: icon changes from clipboard to checkmark, brief "Copied!" toast
- **Grouped record display:** Type | Name/Host | Value | TTL in a clean table
- **One-click copy per field** -- don't make users select text manually
- **navigator.clipboard.writeText()** API -- the modern, secure approach

**Implementation Reference:**
- PatternFly Clipboard Copy component
- Cloudscape Design System Copy to Clipboard
- Flowbite Tailwind CSS Clipboard component
- shadcn/ui community discussion #4052 for a copy component

### 2.3 Verification / "Test Connection" Patterns

**Best Practices from Resend, Clerk, Stripe:**
- **"Verify" button** that checks DNS propagation in real-time
- **Polling with visual feedback** -- animated spinner during check, then green checkmark or red X
- **Partial success states** -- show which records passed and which failed individually
- **Estimated propagation time** -- "DNS changes can take up to 48 hours" messaging
- **Auto-retry** -- periodically re-check without user clicking again
- **Send test email** button after domain verification succeeds

### 2.4 Progress Indicators

**Current Best Practices:**
- **Step indicators** with numbers and labels (Step 1 of 4: Add Domain)
- **Completed steps** shown with checkmarks, current step highlighted, future steps grayed
- **Wizard keeps steps focused** -- each step does ONE thing
- **Progress persists** in URL state (user can share/bookmark their progress)

---

## 3. Rules / Configuration UI Patterns

### 3.1 Cloudflare Email Routing Rules

Cloudflare's email routing represents the current standard for rule configuration:

**Pattern:**
- **Rule = Custom email address + Destination (address or Worker)**
- **Wildcard support** for catch-all rules
- **Filter expressions** instead of simple URL patterns -- fields, functions, operators
- **Priority ordering** -- rules evaluated top-to-bottom
- **Authentication requirements** built into rule system (SPF/DKIM since July 2025)

### 3.2 Conditional Logic Builder Patterns

**Zapier Paths (Reference Implementation):**
- **If/Then branching** -- if "A" happens, do this; if "B" happens, do something else
- **Flexible conditions:** text, numbers, dates, boolean, custom logic
- **AND logic** (must match all rules) vs **OR logic** (match at least one)
- **Fallback rules** -- default path when no conditions match
- **Visual flow** -- paths shown as branching tree diagram

**Recommended Pattern for Email Routing Rules:**

```
+------------------------------------------+
| Rule: Marketing Emails                   |
| ---------------------------------------- |
| IF  [recipient] [contains] [marketing@]  |
|  AND [subject]  [matches]  [newsletter*] |
| THEN [forward to] [team@company.com]     |
|                                           |
| Priority: 1    [Edit] [Delete] [Drag]    |
+------------------------------------------+
```

**UI Components Needed:**
- **Condition rows** with dropdowns for field, operator, and value input
- **Add condition** button (AND/OR toggle)
- **Action selector** (forward, reject, webhook, Worker)
- **Drag-and-drop reordering** for priority (use dnd-kit library)
- **Enable/disable toggle** per rule
- **Test rule** button with sample email input

### 3.3 Gmail Filters UI Pattern

**Relevant Features:**
- Search-based filter creation (create filter from search criteria)
- Multiple actions per filter (forward, label, archive, star, etc.)
- Filter import/export capability
- "Also apply to matching conversations" option

---

## 4. Developer Documentation Design

### 4.1 The Stripe Docs Standard (Three-Column Layout)

Stripe popularized and continues to refine the gold standard:

**Layout:**
- **Left column:** Navigation tree (collapsible sections)
- **Center column:** Explanatory content, guides, parameter descriptions
- **Right column:** Code examples, response samples, error codes

**Key Features:**
- **Language switcher** (Node.js, Python, Ruby, Go, PHP, Java, .NET) -- persists across pages
- **Markdoc** for content authoring -- enables interactivity without mixing code and content
- **Quickstart guides** with links between related topics
- **Copy-paste code snippets** with syntax highlighting
- **Consistent, clean formatting** -- manages huge information density without feeling cluttered

### 4.2 Documentation Tools (2024-2026)

**Top Tools for Stripe-Quality Docs:**
- **Mintlify** -- AI-native, cloud-based, produces Stripe-level docs. Most popular choice for 2024-2026 startups
- **Bump.sh** -- "Stripe-like" three-column API reference from OpenAPI/AsyncAPI specs
- **Markdoc** (Stripe's own) -- open-source, content-first markup

### 4.3 Interactive Documentation Patterns

**Must-Have Features:**
- **API Explorer / Playground** -- test endpoints without leaving docs
- **Webhook testing tools** -- send test webhooks to user's endpoint
- **Live code examples** that can be edited and run in-browser
- **Request/response pairs** shown side by side
- **Authentication helper** -- auto-populate API keys in examples for logged-in users
- **SDK code generation** -- show equivalent code in multiple languages

### 4.4 Recommended Documentation Structure

```
Getting Started
  - Quickstart (send first email in 5 minutes)
  - Authentication (API keys)
  - SDKs & Libraries

Sending Email
  - Send via API
  - Send via SMTP
  - Templates
  - Attachments
  - Batch sending

Receiving Email (Inbound)
  - Setup guide
  - Webhook format
  - Parsing emails
  - Routing rules

Domain Setup
  - DNS configuration
  - SPF / DKIM / DMARC
  - Verification

Webhooks
  - Event types
  - Delivery & retry
  - Testing webhooks

API Reference
  - Emails
  - Domains
  - API Keys
  - Webhooks
  - Inbound Rules
```

---

## 5. Analytics Dashboard Patterns

### 5.1 Metric Cards (KPI Cards)

**Modern Pattern:**
- **Large primary number** (e.g., "12,847 emails sent")
- **Trend indicator** with percentage change and arrow (green up / red down)
- **Sparkline** inline showing 7-day or 30-day trend
- **Time comparison** ("vs last period")
- **Card grid layout** -- typically 3-4 cards across the top of the dashboard

```
+-------------------+  +-------------------+  +-------------------+  +-------------------+
| Emails Sent       |  | Delivery Rate     |  | Open Rate         |  | Bounce Rate       |
| 12,847            |  | 99.2%             |  | 34.7%             |  | 0.8%              |
| [sparkline~~~~]   |  | [sparkline~~~~]   |  | [sparkline~~~~]   |  | [sparkline~~~~]   |
| +12.3% vs last wk |  | +0.1% vs last wk  |  | -2.1% vs last wk  |  | -0.3% vs last wk  |
+-------------------+  +-------------------+  +-------------------+  +-------------------+
```

### 5.2 Time-Range Selectors

**Standard Options:**
- Last 24 hours, 7 days, 30 days, 90 days, Custom range
- **Relative presets** are primary, custom date picker is secondary
- **URL-persistent** -- time range encoded in URL params for sharing
- Compare toggle: "Compare with previous period"

### 5.3 Chart Patterns

**Area Charts:** Best for email volume over time (fills give visual weight)
**Bar Charts:** Best for categorical comparisons (emails by domain, by template)
**Donut/Pie Charts:** Best for status distribution (delivered vs bounced vs deferred)
**Sparklines:** Best for inline trend indicators on metric cards

**Interaction Patterns:**
- **Hover tooltips** showing exact values
- **Click-to-drill-down** from chart to filtered data table
- **Responsive sizing** -- charts adapt to container width
- **Loading skeletons** while data fetches (animate-pulse gray rectangles)

### 5.4 Real-Time vs Aggregated

**Modern Approach:**
- **Real-time feed** for recent email activity (last 100 events, WebSocket updates)
- **Aggregated charts** for historical analytics (hourly/daily rollups)
- **Visual distinction** between real-time (live dot indicator) and aggregated data

---

## 6. Design System & Component Trends

### 6.1 shadcn/ui (The Default Choice for 2024-2026)

**Why shadcn/ui:**
- 90,000+ GitHub stars, 250,000+ weekly npm installs
- Used by Vercel, Supabase, and thousands of production apps
- **Copy-paste model** -- full control over every component (no fighting library APIs)
- Built on Radix UI primitives (accessibility built in)
- Tailwind CSS v4 officially supported as of early 2026

**Key Components for EaaS Dashboard:**
- `DataTable` (with TanStack Table) -- email lists, logs, domain lists
- `Command` (Cmd+K palette) -- global search and actions
- `Dialog` / `Sheet` -- email detail views, configuration panels
- `Tabs` -- switching between views (sent, received, bounced)
- `Badge` -- status indicators (verified, pending, failed)
- `Toast` -- action confirmations, error notifications
- `Skeleton` -- loading states for all data-driven components
- `Card` -- metric cards, summary panels
- `Form` -- rule builders, settings forms

### 6.2 shadcn/ui Charts (Built on Recharts v3)

**Available Chart Types:**
- Area, Bar, Line, Pie, Radar, Radial charts
- **Composition-based** -- build with Recharts components, add shadcn custom tooltips
- **No abstraction lock-in** -- follows Recharts upgrade path directly
- ChartTooltip, ChartLegend as custom overlay components

### 6.3 Tremor (Alternative for Analytics-Heavy Pages)

**When to Use Tremor:**
- 35+ open-source components specifically designed for dashboards
- Built on React + Tailwind CSS + Radix UI + Recharts
- Higher-level API than raw Recharts -- faster to build analytics pages
- Ideal for: KPI cards, filter controls, table actions, spark charts
- Requires React 18.2+ and Tailwind CSS v4+

### 6.4 Dark Mode Implementation

**2024-2026 Best Practices:**
- **Dark mode as default** for developer tools (follows Linear/Vercel pattern)
- **CSS custom properties** with `.dark` selector override
- **System preference detection** + manual toggle + persistence in localStorage
- **Warm grays** (not pure black) for backgrounds
- **Reduced color saturation** in dark mode for less eye strain
- **Test all states** -- empty, loading, error, success in both modes

### 6.5 Tailwind CSS v4 Patterns

**Key Changes in v4:**
- CSS-first configuration (no more tailwind.config.js)
- Native CSS cascade layers
- Faster build times
- Container queries built in
- shadcn/ui CLI auto-detects v4 and configures accordingly

### 6.6 Animation / Micro-Interactions (Framer Motion / Motion)

**Package Note:** `framer-motion` was renamed to `motion` in late 2024. Use `motion` package.

**Essential Animations for EaaS Dashboard:**
- **Page transitions** -- fade/slide between routes (~200ms)
- **List animations** -- staggered entrance for email lists (AnimatePresence + stagger)
- **Status changes** -- color transitions for delivery status updates
- **Skeleton loaders** -- `animate-pulse` for loading states
- **Toast notifications** -- slide in from top-right, auto-dismiss
- **Expand/collapse** -- smooth height transitions for rule builders, detail panels
- **Hover states** -- subtle scale/shadow changes on interactive elements

**Performance Rules:**
- Only animate `transform` and `opacity` (never width, height, top, left, margin)
- Use `will-change: transform` sparingly
- Respect `prefers-reduced-motion` media query
- Target 60fps -- test on lower-end devices
- 73% of users associate smooth animations with trust (Smashing Magazine research)
- Animated feedback reduces user mistakes by 22% (2024 UX Collective)

### 6.7 Accessibility (WCAG 2.2)

**New in WCAG 2.2 (Enforced 2024+):**

- **2.4.11 Focus Not Obscured (Minimum):** Focus indicators must not be hidden behind sticky headers, modals, or overlays
- **2.4.13 Focus Appearance (AAA):** Strict requirements for focus indicator visibility and prominence
- **Touch targets:** Minimum 24x24 CSS pixels (AA), recommended 44x44 points (Apple) or 48x48dp (Google Material)
- **Roving tabindex** for composite widgets (tabs, menus, toolbars) -- only one item in tab order, arrow keys move within
- **Dialog focus management:** When a modal opens, focus MUST move into it immediately; use native `<dialog>` element with `showModal()` for built-in focus trapping

**Focus Indicator Design:**
- Minimum 2px solid outline with sufficient contrast
- Consistent across ALL interactive elements
- Never rely on color alone for focus indication
- Build a focus management system that tracks context and manages transitions

---

## 7. Email-Specific UI Patterns

### 7.1 Email List / Data Table

**Recommended Implementation: shadcn/ui DataTable + TanStack Table**

**Features to Implement:**
- **Column visibility toggling** -- let users choose which columns to show
- **Column resizing** -- drag column borders to resize
- **Multi-column sorting** -- click column headers, shift+click for multi-sort
- **Faceted filters** for categorical data (status, domain, template)
- **Debounced search** -- filter as user types, with ~300ms debounce
- **URL-based state persistence** -- filters/sort encoded in URL (use `nuqs` library)
- **Virtualization** for large datasets -- smooth scrolling with thousands of rows
- **Row selection** with bulk actions (resend, delete, export)

**Filter Bar Pattern (OpenStatus Reference):**
```
[Search emails...] [Status: All v] [Domain: All v] [Date range v] [More filters v]
                                                                    [Clear all]
```

**Email List Columns:**
| To | Subject | Status | Sent At | Delivery Time |
| Click row to expand detail view |

### 7.2 Email Detail View

**Layout: Side Panel (Sheet) or Full Page**

```
+------------------------------------------------------------------+
| < Back to emails                                    [Resend] [Raw] |
| ----------------------------------------------------------------- |
| To: user@example.com                                              |
| From: noreply@yourapp.com                                         |
| Subject: Welcome to Our Platform                                  |
| Sent: 2026-04-01 14:32:07 UTC                                    |
| Status: [Delivered] (badge)                                       |
| ----------------------------------------------------------------- |
| Timeline:                                                         |
|   14:32:07 -- Queued                                              |
|   14:32:08 -- Sent to MTA                                         |
|   14:32:09 -- Delivered                                            |
|   14:32:45 -- Opened                                              |
| ----------------------------------------------------------------- |
| Authentication:                                                   |
|   SPF: [PASS] (badge)  DKIM: [PASS] (badge)  DMARC: [PASS]      |
| ----------------------------------------------------------------- |
| [Preview Tab] [HTML Source Tab] [Headers Tab]                     |
|                                                                   |
|   (Rendered HTML email preview in sandboxed iframe)               |
|                                                                   |
+------------------------------------------------------------------+
```

### 7.3 Thread / Conversation View

**Modern Patterns:**
- **Indentation levels** with reply counts to track conversation depth
- **Inline thread view** within main list + dedicated thread panel
- **Collapsible messages** -- show latest expanded, older collapsed with summary
- **Visual connection lines** between messages in a thread
- **Timestamp relative display** ("2 hours ago") with absolute on hover

### 7.4 HTML Email Preview

**Implementation Pattern:**
- **Sandboxed iframe** to render HTML email safely (prevent script execution)
- **Desktop/mobile toggle** -- preview at different viewport widths
- **HTML source view** with syntax highlighting (use `prism.js` or `shiki`)
- **Plain text fallback** tab
- **"Send test email"** button to preview in actual email client

### 7.5 Attachment Display

**Pattern:**
- **File icon** based on MIME type (PDF, image, document, archive, etc.)
- **File name + size** displayed inline
- **Image thumbnails** for image attachments
- **Download button** per attachment
- **Virus/malware scan status** badge (clean/suspicious/scanning)

### 7.6 Security Verdict Display

**Authentication Badges (SPF/DKIM/DMARC):**

```
SPF:  [PASS]   -- green badge with checkmark
DKIM: [PASS]   -- green badge with checkmark
DMARC: [FAIL]  -- red badge with X icon

[PASS] = green background, white text, checkmark icon
[FAIL] = red background, white text, X icon
[NONE] = gray background, muted text, dash icon
[PENDING] = yellow background, muted text, clock icon
```

**Spam Score Display:**
- Numeric score with visual gauge/meter
- Color gradient: green (0-3) -> yellow (4-6) -> red (7-10)
- Hover for breakdown of individual scoring factors

### 7.7 Empty States

**Best Practices for 2024-2026:**
- **Single strong sentence** + supporting text
- **Single prominent CTA** ("Send your first email" or "Add a domain")
- **Simple monochrome illustration** (Linear/Notion style) -- warm but minimal
- **Different empty states per context:**
  - First-time user: onboarding-focused ("Get started by adding your domain")
  - No search results: "No emails match your filters" + clear filters button
  - Error loading: "Failed to load emails" + retry button
  - Post-completion: Reward ("All caught up!" with encouraging illustration)
- **No repetitive illustrations** when multiple widgets are empty -- use text-only in that case

### 7.8 Loading Skeletons

**Pattern:**
- **Match the layout** of the content that will appear
- **animate-pulse** (Tailwind) for subtle pulsing gray rectangles
- **Skeleton for every data-driven component:** tables, charts, cards, detail views
- **Progressive loading** -- show skeleton, then populate sections as data arrives
- **Never show blank white space** while loading

### 7.9 Error States

**Design:**
- Clear error icon + message explaining what went wrong
- Actionable: "Retry" button or alternative action
- Non-blocking when possible: show partial data with error indicator for failed sections
- Log error details collapsible for debugging

---

## 8. Recommended Tech Stack for EaaS Dashboard

Based on 2024-2026 industry patterns:

| Layer | Technology | Rationale |
|-------|-----------|-----------|
| Framework | **Next.js 15** | Industry standard, Vercel-backed |
| Components | **shadcn/ui** | 90k+ stars, full control, accessible |
| Styling | **Tailwind CSS v4** | CSS-first config, native layers |
| Data Tables | **TanStack Table v8** | Sorting, filtering, pagination, virtualization |
| Charts | **shadcn/ui Charts** (Recharts v3) | Native shadcn integration |
| Charts (analytics-heavy) | **Tremor** | Higher-level API for dashboards |
| Animation | **motion** (formerly framer-motion) | Spring physics, AnimatePresence, layout |
| State (URL) | **nuqs** | URL-based state for filters/search |
| State (client) | **zustand** | Lightweight, simple, composable |
| Forms | **react-hook-form + zod** | Type-safe validation |
| Drag & Drop | **dnd-kit** | Accessible, performant, modern |
| Command Palette | **cmdk** | shadcn Command component uses this |
| Docs | **Mintlify** or **Markdoc** | Stripe-quality documentation |
| Icons | **Lucide React** | shadcn default icon set |
| Date handling | **date-fns** | Tree-shakeable, lightweight |
| Code highlighting | **shiki** | VS Code-quality syntax highlighting |

---

## 9. Reference Links & Inspiration Sources

### Design Pattern Libraries
- [SaaSFrame Dashboard Examples](https://www.saasframe.io/categories/dashboard) -- 166 real SaaS dashboard screenshots
- [SaaSFrame Analytics Examples](https://www.saasframe.io/categories/analytics) -- 67 SaaS analytics page examples
- [SaaSFrame Empty State Examples](https://www.saasframe.io/patterns/empty-state) -- 151 empty state examples
- [SaaSFrame Copy to Clipboard Examples](https://www.saasframe.io/patterns/copy-to-clipboard) -- 78 copy-to-clipboard examples
- [SaaS Interface Dashboard Examples](https://saasinterface.com/pages/dashboard/) -- 149 dashboard examples
- [Muzli Dashboard Inspiration](https://muz.li/inspiration/dashboard-inspiration/) -- 60+ dashboard designs (2026 trends)
- [Mobbin Command Palette Examples](https://mobbin.com/glossary/command-palette) -- real app command palette patterns
- [Mobbin Empty State Examples](https://mobbin.com/glossary/empty-state) -- real app empty state patterns

### Product References (Study These Dashboards)
- [Resend](https://resend.com) -- email for developers (benchmark DX)
- [Resend UI/UX on SaaSFrame](https://www.saasframe.io/saas/resend) -- Resend design patterns catalog
- [Postmark](https://postmarkapp.com) -- clean email delivery dashboard
- [Postmark Email Analytics](https://postmarkapp.com/email-analytics) -- 45-day analytics view
- [Linear](https://linear.app) -- the UI that defined modern SaaS design
- [Linear UI Redesign Blog Post](https://linear.app/now/how-we-redesigned-the-linear-ui) -- design decisions explained
- [Linear Design Refresh Blog](https://linear.app/now/behind-the-latest-design-refresh) -- latest 2025 update details
- [Vercel Dashboard](https://vercel.com) -- developer-centric dashboard
- [Vercel Dashboard Redesign Blog](https://vercel.com/blog/dashboard-redesign) -- design philosophy
- [Cloudflare Email Routing](https://developers.cloudflare.com/email-routing/) -- rule configuration reference

### Component Libraries & Templates
- [shadcn/ui Documentation](https://ui.shadcn.com) -- official component docs
- [shadcn/ui Charts](https://ui.shadcn.com/charts/area) -- chart gallery with all types
- [shadcn/ui DataTable](https://ui.shadcn.com/docs/components/radix/data-table) -- official data table docs
- [OpenStatus Data Table Filters](https://data-table.openstatus.dev/) -- advanced filter patterns with shadcn
- [Tremor Components](https://www.tremor.so/) -- chart and dashboard components
- [shadcncraft Design System](https://shadcncraft.com) -- Figma design system for shadcn
- [shadcnuikit Dashboards](https://shadcnuikit.com/) -- admin dashboard templates
- [All Shadcn Components](https://allshadcn.com) -- community component gallery

### Documentation Design References
- [Stripe API Reference](https://docs.stripe.com/api) -- the gold standard three-column layout
- [Stripe Markdoc Blog Post](https://stripe.dev/blog/markdoc) -- how Stripe builds interactive docs
- [Mintlify](https://mintlify.com) -- build Stripe-quality docs
- [Bump.sh](https://bump.sh) -- OpenAPI to three-column docs

### Design Trend Articles
- [Top Dashboard Design Trends 2025 (UITop)](https://uitop.design/blog/design/top-dashboard-design-trends/)
- [Top SaaS Design Trends 2026](https://www.designstudiouiux.com/blog/top-saas-design-trends/)
- [Linear Design Trend Explained (LogRocket)](https://blog.logrocket.com/ux-design/linear-design/)
- [Rise of Linear Style Design (Medium)](https://medium.com/design-bootcamp/the-rise-of-linear-style-design-origins-trends-and-techniques-4fd96aab7646)
- [Vercel Dashboard UX Analysis (Medium)](https://medium.com/design-bootcamp/vercels-new-dashboard-ux-what-it-teaches-us-about-developer-centric-design-93117215fe31)
- [Rise of shadcn/ui (SaaSIndie)](https://saasindie.com/blog/shadcn-ui-trends-and-future)
- [Motion UI with Framer Motion 2025 (DEV)](https://dev.to/shoaibsid/building-scalable-ui-systems-with-tailwind-css-v4-and-shadcnui-59o4)
- [WCAG 2.2 Complete Guide for Developers](https://www.accessibility.build/blog/complete-guide-wcag-2-2-compliance-developers-2024)
- [Focus Indicators Guide (Sara Soueidan)](https://www.sarasoueidan.com/blog/focus-indicators/)
- [API Documentation Best Practices 2025 (Theneo)](https://www.theneo.io/blog/api-documentation-best-practices-guide-2025)

### Visual Inspiration (Dribbble / Behance)
- [Dribbble: Email Dashboard](https://dribbble.com/tags/email-dashboard)
- [Dribbble: Dashboard UI](https://dribbble.com/tags/dashboard-ui)
- [Dribbble: Email Thread](https://dribbble.com/tags/email-thread)
- [Dribbble: Dashboard Design 2024](https://dribbble.com/tags/dashbord-ui-design-2024)
- [Behance: Fintech Dashboard UI](https://www.behance.net/search/projects/fintech%20dashboard%20ui%20design)

### Email Authentication Tools (UI Reference)
- [EasyDMARC Dashboard](https://easydmarc.com) -- DMARC report analyzer UI
- [MxToolbox](https://mxtoolbox.com) -- DNS record verification UI
- [Postmark DMARC Monitoring](https://dmarc.postmarkapp.com/) -- free DMARC monitoring dashboard

---

## Summary: Top 10 Design Decisions for EaaS Dashboard

1. **Dark-first, Linear-style interface** -- warm grays, minimal color, Inter typeface
2. **Cmd+K command palette** -- keyboard-first navigation as a core feature
3. **shadcn/ui + TanStack Table** -- for all data tables (email lists, logs, domains)
4. **Resend-style email list as home** -- land on emails, not a generic overview
5. **Postmark-style color-coded statuses** -- green/yellow/red for delivery states
6. **Step-by-step DNS wizard** with copy-to-clipboard, live verification, and save/resume
7. **Three-column API docs** (Stripe pattern) built with Mintlify or Markdoc
8. **shadcn/ui Charts + Tremor** for analytics -- metric cards with sparklines on top
9. **Conditional rule builder** with dropdowns, AND/OR logic, drag-and-drop priority
10. **WCAG 2.2 compliance** from day one -- focus management, touch targets, reduced motion support
