// ============================================================
// EaaS Dashboard - Common Types
// ============================================================

export interface ApiResponse<T> {
  success: boolean;
  data: T;
  error?: string;
}

export interface PaginatedResponse<T> {
  items: T[];
  /** Returned by endpoints using the shared PagedResponse contract */
  total?: number;
  /** Returned by endpoints with custom result types */
  totalCount?: number;
  page: number;
  pageSize: number;
  totalPages?: number;
}

export interface PaginationParams {
  page?: number;
  page_size?: number;
}

export interface SortParams {
  sort_by?: string;
  sort_dir?: 'asc' | 'desc';
}

export interface DateRangeParams {
  date_from?: string;
  date_to?: string;
}
