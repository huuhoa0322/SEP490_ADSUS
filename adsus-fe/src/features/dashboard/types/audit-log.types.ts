import type { Role } from "@/types/api.types";

/** Mã hành động do backend sinh ra — xem AccountAuditTrail.cs. */
export type AuditAction =
  | "CREATE_ACCOUNT"
  | "UPDATE_ACCOUNT"
  | "LOCK_ACCOUNT"
  | "UNLOCK_ACCOUNT"
  | "DEACTIVATE_ACCOUNT"
  | "ADMIN_RESET_PASSWORD"
  | "SELF_RESET_PASSWORD";

/** Một dòng nhật ký thao tác. Không chứa ngày sinh hay dữ liệu y tế (UC-04 BR-01). */
export interface AuditLogEntry {
  logId: string;
  actorId: string;
  actorName: string;
  actorRole: Role;
  /**
   * Kiểu để rộng hơn AuditAction: bảng nhật ký dùng chung cả hệ thống, module khác ghi vào
   * những mã mà màn này chưa biết (REGISTER_AI_MODEL, ACTIVATE_AI_MODEL...). Chặt quá thì
   * mỗi lần module khác thêm hành động là màn này vỡ.
   */
  action: AuditAction | string;
  detail: string | null;
  /** Giờ UTC dạng ISO. */
  performedAt: string;
}
