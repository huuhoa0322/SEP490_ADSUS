/**
 * Authoritative API Envelopes and Schema Contracts for ADSUS E2E Testing Track
 * Derived from PROJECT.md Interface Contracts and survey_qa_spec.md
 */

export interface ApiResponse<T> {
  code: number;
  message: string;
  data: T;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export type AppointmentStatus = "BOOKED" | "APPROVED" | "COMPLETED" | "CANCELLED";

export interface CheckinQueueItemResponse {
  appointmentId: string;
  slotTime: string; // ISO DateTime or "HH:mm"
  patientFullName: string;
  patientPhone: string;
  patientProfileId: string;
  caseId: string | null;
  reason: string;
  doctorName: string;
  status: AppointmentStatus;
}

export interface CheckinQueueResponse {
  items: CheckinQueueItemResponse[];
  totalCount: number;
  page?: number;
  pageSize?: number;
  totalPages?: number;
}

export interface AuditLogResponse {
  logId: string;
  actorId: string;
  actorName: string;
  actorRole: string;
  action: string;
  detail: string | null;
  performedAt: string; // ISO DateTime
}

export interface AdminFeedbackItemResponse {
  id: string;
  rating: number; // 1 to 5
  content: string;
  submittedAt: string; // ISO DateTime
  caseId: string;
  doctorName: string;
  doctorId: string;
  patientProfileId: string;
  patientName: string;
  patientPhone: string;
}

/**
 * Validates that a payload adheres strictly to the ApiResponse envelope structure.
 */
export function validateApiResponse<T>(
  response: unknown,
  dataValidator?: (data: T) => boolean
): response is ApiResponse<T> {
  if (typeof response !== "object" || response === null) return false;
  const res = response as Record<string, unknown>;
  if (typeof res.code !== "number") return false;
  if (typeof res.message !== "string") return false;
  if (!("data" in res)) return false;
  if (dataValidator && res.data !== null && res.data !== undefined) {
    return dataValidator(res.data as T);
  }
  return true;
}

/**
 * Validates that a payload adheres strictly to the PagedResult structure.
 * Enforces uniform 15 items/page requirement where applicable.
 */
export function validatePagedResult<T>(
  paged: unknown,
  itemValidator?: (item: T) => boolean,
  strictPageSize = 15
): paged is PagedResult<T> {
  if (typeof paged !== "object" || paged === null) return false;
  const p = paged as Record<string, unknown>;
  if (!Array.isArray(p.items)) return false;
  if (typeof p.page !== "number" || p.page < 1) return false;
  if (typeof p.pageSize !== "number") return false;
  if (strictPageSize > 0 && p.pageSize !== strictPageSize) return false;
  if (typeof p.totalItems !== "number" || p.totalItems < 0) return false;
  if (typeof p.totalPages !== "number" || p.totalPages < 0) return false;

  // Verify totalPages math consistency
  const expectedTotalPages = p.totalItems === 0 ? 0 : Math.ceil(p.totalItems / p.pageSize);
  if (p.totalPages !== expectedTotalPages) return false;

  if (itemValidator) {
    for (const item of p.items) {
      if (!itemValidator(item as T)) return false;
    }
  }
  return true;
}

export function validateCheckinQueueItem(item: unknown): item is CheckinQueueItemResponse {
  if (typeof item !== "object" || item === null) return false;
  const i = item as Record<string, unknown>;
  return (
    typeof i.appointmentId === "string" &&
    typeof i.slotTime === "string" &&
    typeof i.patientFullName === "string" &&
    typeof i.patientPhone === "string" &&
    typeof i.patientProfileId === "string" &&
    (i.caseId === null || typeof i.caseId === "string") &&
    typeof i.reason === "string" &&
    typeof i.doctorName === "string" &&
    ["BOOKED", "APPROVED", "COMPLETED", "CANCELLED"].includes(i.status as string)
  );
}

export function validateAuditLogEntry(item: unknown): item is AuditLogResponse {
  if (typeof item !== "object" || item === null) return false;
  const a = item as Record<string, unknown>;
  return (
    typeof a.logId === "string" &&
    typeof a.actorId === "string" &&
    typeof a.actorName === "string" &&
    typeof a.actorRole === "string" &&
    typeof a.action === "string" &&
    (a.detail === null || typeof a.detail === "string") &&
    typeof a.performedAt === "string"
  );
}

export function validateFeedbackItem(item: unknown): item is AdminFeedbackItemResponse {
  if (typeof item !== "object" || item === null) return false;
  const f = item as Record<string, unknown>;
  return (
    typeof f.id === "string" &&
    typeof f.rating === "number" &&
    f.rating >= 1 &&
    f.rating <= 5 &&
    typeof f.content === "string" &&
    typeof f.submittedAt === "string" &&
    typeof f.caseId === "string" &&
    typeof f.doctorName === "string" &&
    typeof f.doctorId === "string" &&
    typeof f.patientProfileId === "string" &&
    typeof f.patientName === "string" &&
    typeof f.patientPhone === "string"
  );
}
