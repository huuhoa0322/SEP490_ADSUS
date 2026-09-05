/**
 * Vỏ bọc chung của mọi response từ ADSUS_BE — theo api_design_rules nhóm đã chốt.
 * Backend luôn trả về đúng hình dạng này, kể cả khi lỗi.
 */
export interface ApiResponse<T> {
  code: number;
  message: string;
  data: T | null;
}

/**
 * Paginated response wrapper — dùng cho list endpoints (Module 08 và các module khác).
 * Backend trả về: { items: T[], page, pageSize, totalItems, totalPages }
 */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

/**
 * Vai trò tài khoản. Khớp với enum user_role trong database.
 * NURSE có quyền giống hệt DOCTOR (theo quyết định ghi đè PRD trong UCS).
 */
export type Role = "ADMIN" | "DOCTOR" | "PATIENT" | "NURSE" | "PHARMACIST";
