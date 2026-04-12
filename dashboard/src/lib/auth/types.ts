export interface SessionData {
  userId: string;
  email: string;
  displayName: string;
  role: "superadmin" | "admin" | "readonly" | "tenant";
  expiresAt: number; // Unix timestamp in seconds
}
