"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
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
  const [error, setError] = useState("");
  const [agreedToTerms, setAgreedToTerms] = useState(false);
  const [loading, setLoading] = useState(false);
  const [apiKey, setApiKey] = useState<string | null>(null);
  const [showApiKeyDialog, setShowApiKeyDialog] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError("");

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
        window.location.href = "/emails";
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
    window.location.href = "/emails";
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-4">
      <Card className="w-full max-w-sm border-border bg-card">
        <CardHeader className="space-y-4 text-center">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-xl bg-primary">
            <Zap className="h-6 w-6 text-primary-foreground" />
          </div>
          <CardTitle className="text-xl font-bold text-foreground">
            Create your SendNex account
          </CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="name" className="text-sm text-foreground/80">
                Name
              </Label>
              <Input
                id="name"
                type="text"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="Your full name"
                className="border-border bg-muted text-foreground placeholder:text-muted-foreground/40"
                required
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="email" className="text-sm text-foreground/80">
                Email
              </Label>
              <Input
                id="email"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="you@example.com"
                className="border-border bg-muted text-foreground placeholder:text-muted-foreground/40"
                required
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="password" className="text-sm text-foreground/80">
                Password
              </Label>
              <Input
                id="password"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="At least 8 characters"
                className="border-border bg-muted text-foreground placeholder:text-muted-foreground/40"
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
                type="password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                placeholder="Repeat your password"
                className="border-border bg-muted text-foreground placeholder:text-muted-foreground/40"
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
                type="text"
                value={companyName}
                onChange={(e) => setCompanyName(e.target.value)}
                placeholder="Your company"
                className="border-border bg-muted text-foreground placeholder:text-muted-foreground/40"
              />
            </div>
            <div className="flex items-start gap-2">
              <input
                id="agreeToTerms"
                type="checkbox"
                checked={agreedToTerms}
                onChange={(e) => setAgreedToTerms(e.target.checked)}
                className="mt-0.5 h-4 w-4 rounded border-border accent-primary"
                required
              />
              <Label
                htmlFor="agreeToTerms"
                className="text-xs leading-relaxed text-muted-foreground"
              >
                I agree to the{" "}
                <Link
                  href={Routes.TERMS}
                  className="text-primary hover:underline"
                  target="_blank"
                >
                  Terms of Service
                </Link>
                ,{" "}
                <Link
                  href={Routes.PRIVACY}
                  className="text-primary hover:underline"
                  target="_blank"
                >
                  Privacy Policy
                </Link>
                , and{" "}
                <Link
                  href={Routes.ACCEPTABLE_USE}
                  className="text-primary hover:underline"
                  target="_blank"
                >
                  Acceptable Use Policy
                </Link>
              </Label>
            </div>
            {error && (
              <p className="rounded-lg border border-red-500/30 bg-red-500/10 px-3 py-2 text-xs text-red-400">
                {error}
              </p>
            )}
            <Button
              type="submit"
              disabled={loading || !agreedToTerms}
              className="w-full bg-primary text-primary-foreground hover:bg-primary/90"
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
