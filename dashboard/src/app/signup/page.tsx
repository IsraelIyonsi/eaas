"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog";
import { CopyButton } from "@/components/shared/copy-button";
import { Zap, Loader2 } from "lucide-react";
import Link from "next/link";
import { Routes } from "@/lib/constants/routes";

export default function SignupPage() {
  const router = useRouter();
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [companyName, setCompanyName] = useState("");
  const [legalEntityName, setLegalEntityName] = useState("");
  const [postalAddress, setPostalAddress] = useState("");
  const [error, setError] = useState("");
  const [agreedToTerms, setAgreedToTerms] = useState(false);
  const [loading, setLoading] = useState(false);
  const [apiKey, setApiKey] = useState<string | null>(null);
  const [showApiKeyDialog, setShowApiKeyDialog] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError("");

    if (!agreedToTerms) {
      setError(
        "Please agree to the Terms of Service, Privacy Policy, and Acceptable Use Policy to continue.",
      );
      return;
    }

    if (password !== confirmPassword) {
      setError("Passwords do not match.");
      return;
    }

    if (password.length < 8) {
      setError("Password must be at least 8 characters.");
      return;
    }

    setLoading(true);

    try {
      const res = await fetch("/api/auth/register", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name,
          email,
          password,
          companyName: companyName || undefined,
          legalEntityName,
          postalAddress,
        }),
      });

      const data = await res.json().catch(() => ({}));

      if (!res.ok) {
        setError(data.error ?? "Registration failed. Please try again.");
        return;
      }

      if (data.data?.apiKey) {
        setApiKey(data.data.apiKey);
        setShowApiKeyDialog(true);
      } else {
        router.refresh();
      router.push("/overview");
      }
    } catch {
      setError(
        "Unable to connect. Please check your connection and try again.",
      );
    } finally {
      setLoading(false);
    }
  }

  function handleDialogClose() {
    setShowApiKeyDialog(false);
    window.location.href = "/overview";
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-4">
      <Card className="w-full max-w-sm border-border bg-card">
        <CardHeader className="space-y-4 text-center">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-xl bg-primary">
            <Zap className="h-6 w-6 text-primary-foreground" />
          </div>
          <h1
            data-slot="card-title"
            className="font-heading text-xl leading-snug font-bold text-foreground"
          >
            Create your SendNex account
          </h1>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="name" className="text-sm text-foreground/80">
                Name
              </Label>
              <Input
                id="name"
                name="name"
                type="text"
                autoComplete="name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="Your full name"
                className="h-11 border-border bg-muted text-foreground placeholder:text-muted-foreground/40 sm:h-10"
                required
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="email" className="text-sm text-foreground/80">
                Email
              </Label>
              <Input
                id="email"
                name="email"
                type="email"
                autoComplete="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="you@example.com"
                className="h-11 border-border bg-muted text-foreground placeholder:text-muted-foreground/40 sm:h-10"
                required
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="password" className="text-sm text-foreground/80">
                Password
              </Label>
              <Input
                id="password"
                name="password"
                type="password"
                autoComplete="new-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="At least 8 characters"
                className="h-11 border-border bg-muted text-foreground placeholder:text-muted-foreground/40 sm:h-10"
                required
              />
            </div>
            <div className="space-y-2">
              <Label
                htmlFor="confirmPassword"
                className="text-sm text-foreground/80"
              >
                Confirm Password
              </Label>
              <Input
                id="confirmPassword"
                name="confirmPassword"
                type="password"
                autoComplete="new-password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                placeholder="Repeat your password"
                className="h-11 border-border bg-muted text-foreground placeholder:text-muted-foreground/40 sm:h-10"
                required
              />
            </div>
            <div className="space-y-2">
              <Label
                htmlFor="companyName"
                className="text-sm text-foreground/80"
              >
                Company Name{" "}
                <span className="text-muted-foreground/50">(optional)</span>
              </Label>
              <Input
                id="companyName"
                name="companyName"
                type="text"
                autoComplete="organization"
                value={companyName}
                onChange={(e) => setCompanyName(e.target.value)}
                placeholder="Your company"
                className="h-11 border-border bg-muted text-foreground placeholder:text-muted-foreground/40 sm:h-10"
              />
            </div>
            <div className="space-y-2">
              <Label
                htmlFor="legalEntityName"
                className="text-sm text-foreground/80"
              >
                Legal Entity Name
              </Label>
              <Input
                id="legalEntityName"
                name="legalEntityName"
                type="text"
                value={legalEntityName}
                onChange={(e) => setLegalEntityName(e.target.value)}
                placeholder="Acme, Inc."
                className="h-11 border-border bg-muted text-foreground placeholder:text-muted-foreground/40 sm:h-10"
                required
              />
              <p className="text-xs text-muted-foreground/60">
                Required by CAN-SPAM §7704(a)(5). Appears in compliance
                footers of your emails.
              </p>
            </div>
            <div className="space-y-2">
              <Label
                htmlFor="postalAddress"
                className="text-sm text-foreground/80"
              >
                Postal Address
              </Label>
              <Input
                id="postalAddress"
                name="postalAddress"
                type="text"
                autoComplete="street-address"
                value={postalAddress}
                onChange={(e) => setPostalAddress(e.target.value)}
                placeholder="123 Main St, City, State, ZIP, Country"
                className="h-11 border-border bg-muted text-foreground placeholder:text-muted-foreground/40 sm:h-10"
                required
              />
              <p className="text-xs text-muted-foreground/60">
                A valid physical postal address required by CAN-SPAM.
              </p>
            </div>
            {/* 44x44 hit area on the checkbox per WCAG 2.5.5 Target Size (AAA)
                — wrap the visual 16x16 input in a padded label so the whole
                bounding box is tappable on mobile. */}
            <div className="flex items-start gap-2">
              <label
                htmlFor="agreeToTerms"
                className="inline-flex h-11 w-11 shrink-0 items-center justify-center -my-1 -ml-1 cursor-pointer sm:h-9 sm:w-9"
              >
                <input
                  id="agreeToTerms"
                  name="agreeToTerms"
                  type="checkbox"
                  checked={agreedToTerms}
                  onChange={(e) => setAgreedToTerms(e.target.checked)}
                  className="h-4 w-4 shrink-0 rounded border-border accent-primary"
                  required
                />
              </label>
              <label
                htmlFor="agreeToTerms"
                className="pt-2.5 text-xs leading-relaxed text-muted-foreground sm:pt-1.5"
              >
                I agree to the{" "}
                <Link
                  href={Routes.TERMS}
                  className="whitespace-nowrap text-primary hover:underline"
                  target="_blank"
                >
                  Terms of Service
                </Link>
                ,{" "}
                <Link
                  href={Routes.PRIVACY}
                  className="whitespace-nowrap text-primary hover:underline"
                  target="_blank"
                >
                  Privacy Policy
                </Link>
                , and{" "}
                <Link
                  href={Routes.ACCEPTABLE_USE}
                  className="whitespace-nowrap text-primary hover:underline"
                  target="_blank"
                >
                  Acceptable Use Policy
                </Link>
              </label>
            </div>
            {error && (
              <p className="rounded-lg border border-red-500/30 bg-red-500/10 px-3 py-2 text-xs text-red-400">
                {error}
              </p>
            )}
            <Button
              type="submit"
              disabled={loading}
              className="h-11 w-full bg-primary text-primary-foreground hover:bg-primary/90 sm:h-10"
            >
              {loading ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Creating account...
                </>
              ) : (
                "Create Account"
              )}
            </Button>
          </form>
          <div className="mt-4 space-y-2 text-center text-xs text-muted-foreground">
            <p>
              Already have an account?{" "}
              <Link
                href={Routes.LOGIN}
                className="text-primary hover:underline"
              >
                Sign in
              </Link>
            </p>
            <p className="pt-1">
              <Link
                href={Routes.PRIVACY}
                className="text-muted-foreground hover:text-foreground hover:underline"
              >
                Privacy Policy
              </Link>
              {" · "}
              <Link
                href={Routes.TERMS}
                className="text-muted-foreground hover:text-foreground hover:underline"
              >
                Terms of Service
              </Link>
              {" · "}
              <Link
                href={Routes.COOKIES}
                className="text-muted-foreground hover:text-foreground hover:underline"
              >
                Cookie Policy
              </Link>
            </p>
          </div>
        </CardContent>
      </Card>

      <Dialog open={showApiKeyDialog} onOpenChange={handleDialogClose}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Your API Key</DialogTitle>
            <DialogDescription>
              Save this API key now. You will not be able to see it again.
            </DialogDescription>
          </DialogHeader>
          <div className="flex items-center gap-2 rounded-md border border-border bg-muted px-3 py-2">
            <code className="flex-1 break-all font-mono text-sm text-foreground">
              {apiKey}
            </code>
            <CopyButton value={apiKey ?? ""} label="API Key" />
          </div>
          <DialogFooter>
            <Button onClick={handleDialogClose} className="w-full">
              Continue to Dashboard
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
