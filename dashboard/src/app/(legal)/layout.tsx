import Link from "next/link";
import { ArrowLeft } from "lucide-react";

export default function LegalLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <div className="min-h-screen bg-background px-4 py-12">
      <div className="mx-auto max-w-3xl">
        <Link
          href="/"
          className="mb-8 inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground transition-colors"
        >
          <ArrowLeft className="h-4 w-4" />
          Back to home
        </Link>
        <article className="legal-prose max-w-none">
          {children}
        </article>
        <footer className="mt-12 border-t border-border pt-6 text-xs text-muted-foreground">
          <nav className="flex flex-wrap gap-x-4 gap-y-2">
            <Link href="/privacy" className="hover:text-foreground hover:underline">
              Privacy Policy
            </Link>
            <Link href="/terms" className="hover:text-foreground hover:underline">
              Terms of Service
            </Link>
            <Link href="/cookies" className="hover:text-foreground hover:underline">
              Cookie Policy
            </Link>
            <Link href="/dpa" className="hover:text-foreground hover:underline">
              DPA
            </Link>
            <Link href="/sub-processors" className="hover:text-foreground hover:underline">
              Sub-Processors
            </Link>
            <Link href="/acceptable-use" className="hover:text-foreground hover:underline">
              Acceptable Use
            </Link>
          </nav>
        </footer>
      </div>
    </div>
  );
}
