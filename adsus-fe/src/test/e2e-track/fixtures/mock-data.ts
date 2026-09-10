/**
 * Authoritative Mock Data Generators and Fixtures for E2E Testing Track
 */
import {
  type CheckinQueueItemResponse,
  type AuditLogResponse,
  type AdminFeedbackItemResponse,
  type AppointmentStatus,
} from "./contracts";

export function generateCheckinItem(
  overrides: Partial<CheckinQueueItemResponse> = {}
): CheckinQueueItemResponse {
  const id = Math.floor(1000 + Math.random() * 9000);
  return {
    appointmentId: `app-${id}-uuid`,
    slotTime: "2026-09-10T08:30:00Z",
    patientFullName: `Nguyễn Văn ${id}`,
    patientPhone: `090${id}123`,
    patientProfileId: `pat-${id}-profile`,
    caseId: `case-${id}-uuid`,
    reason: "Khám siêu âm tổng quát",
    doctorName: "BS. Trần Văn Minh",
    status: "BOOKED",
    ...overrides,
  };
}

export function generateCheckinQueue(
  count: number,
  statusDist: Record<AppointmentStatus, number> = { BOOKED: count, APPROVED: 0, COMPLETED: 0, CANCELLED: 0 }
): CheckinQueueItemResponse[] {
  const items: CheckinQueueItemResponse[] = [];
  const statuses: AppointmentStatus[] = ["BOOKED", "APPROVED", "COMPLETED", "CANCELLED"];

  let generated = 0;
  for (const status of statuses) {
    const quota = statusDist[status] ?? 0;
    for (let i = 0; i < quota && generated < count; i++) {
      items.push(
        generateCheckinItem({
          status,
          slotTime: `2026-09-10T08:${String((generated % 60)).padStart(2, "0")}:00Z`,
        })
      );
      generated++;
    }
  }

  while (items.length < count) {
    items.push(generateCheckinItem({ status: "BOOKED" }));
  }

  return items;
}

export const AUDIT_ACTIONS = {
  ACCOUNT_CREATE: "CREATE_ACCOUNT",
  ACCOUNT_UPDATE: "UPDATE_ACCOUNT",
  ACCOUNT_DEACTIVATE: "DEACTIVATE_ACCOUNT",
  ACCOUNT_REACTIVATE: "REACTIVATE_ACCOUNT",
  ADMIN_RESET_PASSWORD: "ADMIN_RESET_PASSWORD",
  SELF_RESET_PASSWORD: "SELF_RESET_PASSWORD",
  AI_REGISTER: "REGISTER_AI_MODEL",
  AI_UPDATE: "UPDATE_AI_MODEL",
  AI_ACTIVATE: "ACTIVATE_AI_MODEL",
  NURSE_CREATE_PATIENT: "NURSE_CREATE_PATIENT_ACCOUNT",
  NURSE_UPDATE_PATIENT: "NURSE_UPDATE_PATIENT_ACCOUNT",
  NURSE_RESET_PASSWORD: "NURSE_RESET_PATIENT_PASSWORD",
  UNKNOWN_CUSTOM: "UNKNOWN_FUTURE_ACTION",
} as const;

export function generateAuditLog(
  overrides: Partial<AuditLogResponse> = {}
): AuditLogResponse {
  const id = Math.floor(1000 + Math.random() * 9000);
  return {
    logId: `log-${id}-uuid`,
    actorId: `user-${id}-actor`,
    actorName: "Admin Root",
    actorRole: "ADMIN",
    action: AUDIT_ACTIONS.ACCOUNT_CREATE,
    detail: `Tạo tài khoản bác sĩ 090${id}999`,
    performedAt: "2026-09-10T09:15:00Z",
    ...overrides,
  };
}

export function generateAuditLogList(count: number): AuditLogResponse[] {
  const actions = Object.values(AUDIT_ACTIONS);
  return Array.from({ length: count }, (_, i) => {
    const action = actions[i % actions.length];
    return generateAuditLog({
      logId: `log-${i + 1}-uuid`,
      action,
      actorName: i % 2 === 0 ? "Admin System" : "Nurse Sarah",
      actorRole: i % 2 === 0 ? "ADMIN" : "NURSE",
      performedAt: new Date(Date.now() - i * 60000).toISOString(),
    });
  });
}

export function generateFeedbackItem(
  overrides: Partial<AdminFeedbackItemResponse> = {}
): AdminFeedbackItemResponse {
  const id = Math.floor(1000 + Math.random() * 9000);
  return {
    id: `fb-${id}-uuid`,
    rating: (id % 5) + 1,
    content: "Dịch vụ phòng khám rất chu đáo, bác sĩ giải thích cặn kẽ.",
    submittedAt: "2026-09-10T10:00:00Z",
    caseId: `case-${id}-uuid`,
    doctorName: "BS. Lê Hoàng Nam",
    doctorId: `doc-${id}-uuid`,
    patientProfileId: `pat-${id}-profile`,
    patientName: "Phạm Thị Lan",
    patientPhone: "0912345678",
    ...overrides,
  };
}

export function generateFeedbackList(count: number): AdminFeedbackItemResponse[] {
  return Array.from({ length: count }, (_, i) => {
    const rating = ((i % 5) + 1) as 1 | 2 | 3 | 4 | 5;
    return generateFeedbackItem({
      id: `fb-${i + 1}-uuid`,
      rating,
      patientName: `Bệnh nhân ${i + 1}`,
      doctorName: i % 2 === 0 ? "BS. Lê Hoàng Nam" : "BS. Trần Văn Minh",
      caseId: `case-code-${1000 + i}`,
      content:
        rating >= 4
          ? "Rất hài lòng với chất lượng điều trị và thái độ của bác sĩ."
          : "Thời gian chờ đợi hơi lâu, cần cải thiện tốc độ tiếp nhận.",
      submittedAt: new Date(Date.now() - i * 3600000).toISOString(),
    });
  });
}
