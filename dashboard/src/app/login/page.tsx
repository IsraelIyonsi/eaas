"use client";

import { useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { Zap, Loader2 } from "lucide-react";
import Link from "next/link";
import { Routes } from "@/lib/constants/routes";

export default function LoginPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const callbackUrl = searchParams.get("callbackUrl");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setLoading(true);

    try {
      const res = await fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password }),
      });

      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        setError(data.error ?? "Invalid email or password. Please try again.");
        return;
      }

      // Only accept relative callback URLs to prevent open-redirect.
      const safeCallback =
        callbackUrl && callbackUrl.startsWith("/") && !callbackUrl.startsWith("//")
          ? callbackUrl
          : "/overview";
      // router.refresh() forces Next.js to re-run server components (and the
      // middleware) against the fresh Set-Cookie we just received, so the
      // subsequent router.push lands on the protected route with the session
      // attached instead of being bounced back to /login.
      router.refresh();
      router.push(safeCallback);
    } catch {
      setError(
        "Unable to connect. Please check your connection and try again.",
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-4">
      <Card className="w-full max-w-sm border-border bg-card">
        <CardHeader className="space-y-4 text-center">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-xl bg-primary">
            <Zap className="h-6 w-6 text-primary-foreground" />
          </div>
          <CardTitle className="text-xl font-bold text-foreground">
            Sign in to SendNex
          </CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="email" className="text-sm text-foreground/80">
                Email
              </Label>
              <Input
                id="email"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="you@company.com"
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
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Enter your password"
                className="border-border bg-muted text-foreground placeholder:text-muted-foreground/40"
                required
              />
            </div>
            {error && (
              <p className="rounded-lg border border-red-500/30 bg-red-500/10 px-3 py-2 text-xs text-red-400">
                {error}
              </p>
            )}
            <Button
              type="submit"
              disabled={loading}
              className="w-full bg-primary text-primary-foreground hover:bg-primary/90"
            >
              {loading ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Signing in...
                </>
              ) : (
                "Sign In"
              )}
            </Button>
          </form>
          <div className="mt-4 space-y-2 text-center text-xs text-muted-foreground">
            <p>
              Don&apos;t have an account?{" "}
              <Link
                href={Routes.SIGNUP}
                className="text-primary hover:underline"
              >
                Create one
              </Link>
            </p>
            <p>
              <a
                href="mailto:support@sendnex.xyz"
                className="text-primary hover:underline"
              >
                Forgot your password?
              </a>
            </p>
            <p className="pt-2">
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
    </div>
  );
}
