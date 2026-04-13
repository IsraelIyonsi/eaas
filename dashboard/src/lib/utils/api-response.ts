import type { PaginatedResponse } from "@/types/common";

/**
 * Safely extracts an array of items from an API response.
 * Handles both flat arrays and paginated responses uniformly.
 *
 * @example
 * const items = extractItems(data); // works whether data is T[] or PaginatedResponse<T>
 */
export function extractItems<T>(
  data: T[] | PaginatedResponse<T> | undefined | null,
): T[] {
  if (!data) return [];
  if (Array.isArray(data)) return data;
  if ("items" in data && Array.isArray(data.items)) return data.items;
  return [];
}

/**
 * Extracts total count from an API response.
 */
export function extractTotalCount<T>(
  data: T[] | PaginatedResponse<T> | undefined | null,
): number {
  if (!data) return 0;
  if (Array.isArray(data)) return data.length;
  if ("totalCount" in data && typeof data.totalCount === "number") return data.totalCount;
  if ("total" in data && typeof data.total === "number") return data.total;
  return 0;
}

/**
 * Computes total pages from an API response.
 */
export function extractTotalPages<T>(
  data: T[] | PaginatedResponse<T> | undefined | null,
  pageSize = 20,
): number {
  const total = extractTotalCount(data);
  return Math.max(1, Math.ceil(total / pageSize));
}

/**
 * Safely looks up a config value by key with a fallback.
 * Prevents "Cannot read properties of undefined" when API returns
 * a status value not present in the config map.
 *
 * @example
 * const { label, color } = safeConfigLookup(EmailStatusConfig, email.status, { label: email.status, color: "bg-gray-500" });
 */
export function safeConfigLookup<TConfig extends Record<string, unknown>>(
  config: TConfig,
  key: string | undefined | null,
  fallback: TConfig[keyof TConfig],
): TConfig[keyof TConfig] {
  if (!key) return fallback;
  return (config as Record<string, unknown>)[key] as TConfig[keyof TConfig] ?? fallback;
}
