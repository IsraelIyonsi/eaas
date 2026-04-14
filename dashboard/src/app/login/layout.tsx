import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Sign in — SendNex",
  description:
    "Sign in to your SendNex account to manage transactional email, domains, and API keys.",
};

export default function LoginLayout({ children }: { children: React.ReactNode }) {
  return children;
}
