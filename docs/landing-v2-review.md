# Landing v2 — Self Review

Branch: `feat/landing-overhaul-v2`
File: `landing/index.html` (single file, inline CSS + JS)
Size: ~36.5 KB (target was <100 KB)

## What changed vs v1

- Full IA rewrite. New section order: Hero → Logos → Features → Code Example → Pricing → Trust/Deliverability → FAQ → Final CTA → Footer. Removed unfocused mid-page filler from v1.
- New design system. Single brand colour `--brand: #4f46e5` (indigo) with a cyan `--accent: #22d3ee` for code highlights. Subtle dual radial gradients on `body` for depth without animation cost.
- Pricing prominence. Flex PAYG is now its own dedicated section with a side-by-side competitor comparison table (Resend, Postmark, SendGrid, Mailgun). Hero copy and final CTA both restate the price.
- Headline rewritten to communicate speed, simplicity, and price advantage in one sentence.
- Hero code snippet upgraded to an Apple-style window chrome with cURL by default plus tab affordances for Node and Python.
- Real Node `fetch` POST + `202` response shape now lives in its own section.
- Trust section reframed as Deliverability (DKIM/SPF/DMARC, dedicated IPs, bounce handling, security). SOC 2 phrased as "committed for 2026", not claimed.
- 8-question FAQ using semantic `<details>`. No JS needed for accordion behaviour.
- New footer with 4-column structure, legal address placeholder, dynamic copyright year.
- SEO: title rewritten, description rewritten, OG + Twitter tags added, canonical URL, JSON-LD Organization schema, theme-color meta.
- Accessibility: skip link, semantic landmarks (`header`/`main`/`section`/`footer`), `aria-label` on nav and code card, `aria-selected` on tabs, focus-visible rings on all buttons, `prefers-reduced-motion` honoured.
- Performance: zero JS dependencies (lucide CDN dropped — replaced with inline SVGs), no images at all on initial load, fonts preconnected, single render-blocking stylesheet (the Google Fonts CSS).

## Trade-offs

- Inline SVG instead of lucide CDN. Saves a network request and ~80 KB of JS, costs a few KB of inline markup. Worth it.
- No light theme toggle. The brief listed it as optional and adding it would have added ~3 KB of CSS plus JS. Punted.
- Code "tabs" in the hero are visual only (no working tab switcher). They communicate "we have SDKs" without the maintenance burden of a real tab component. Real reference lives in `/docs`.
- No customer count, no testimonials, no real logos. Placeholders are clearly marked.

## Known gaps / owner action required before launch

- `{LEGAL_ADDRESS}` placeholder in the footer must be replaced with the registered Nigerian business address.
- 6 placeholder customer logos in the social proof strip (`<!-- TODO: real logos -->`). Replace with real ones or remove the entire section.
- No real customer count anywhere on the page (deliberately — better silent than fabricated).
- `og.png` and `logo.png` are referenced but the assets are not in the repo. Need a 1200x630 OG image and a square logo.
- Status page link points to `https://status.sendnex.xyz` — confirm this subdomain exists or remove.
- Competitor pricing in the comparison table is "as of today". Add a periodic check or change to "starting from".
- `/about`, `/blog`, `/contact`, `/dpa` footer links — confirm pages exist before launch or remove.

## Requirement checklist

1. Single static HTML, inline CSS + JS — done.
2. Portfolio-grade design (Linear/Resend/Vercel feel) — done, dark theme primary, indigo+cyan palette.
3. All required sections in order — done.
4. <100 KB total — done (36.5 KB).
5. Accessibility (semantic, alt, contrast, focus-visible, reduced-motion) — done.
6. Mobile-first responsive at 640/768/1024 — done; sanity-checked at 375.
7. SEO (title, meta, OG, Twitter, JSON-LD, canonical) — done.
8. Branding consistency — "SendNex" everywhere; one brand colour as CSS var.
9. No broken links — all links point to real domains/anchors. Some pages need owner confirmation (see gaps).
10. Copy tone — concrete, dev-friendly, no em dashes outside of HTML entities used as visual separators in the footer.

## Would I ship this?

Yes, with the gaps above resolved. The page is honest about what SendNex is, leads with the price advantage without sounding desperate, and gets a developer to a working request in two scrolls. The remaining work is content (logos, address, OG image), not engineering.
