export interface SessionData {
  userId: string;
  email: string;
  displayName: string;
  role: "superadmin" | "admin" | "readonly";
  expiresAt: number; // Unix timestamp in seconds
}
