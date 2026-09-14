/**
 * UC-13 — Patient đặt lịch khám (Module 8).
 * BR-01: Slot phải tồn tại và có status = OPEN.
 * BR-02: Patient không được đặt trùng slot đã có BOOKED appointment.
 */

export type AppointmentStatus = "BOOKED" | "CANCELLED" | "COMPLETED" | "APPROVED" | "NO_SHOW";

/** Slot còn trống — trả về từ GET /api/v1/appointments/slots */
export interface OpenSlotResponse {
  slotId: string;
  doctorId: string;
  doctorName: string;
  /** Trạng thái tài khoản bác sĩ — dùng để filter bác sĩ active. */
  doctorStatus: string;
  /** Giới tính bác sĩ. */
  doctorGender: string | null;
  slotDate: string; // ISO date "YYYY-MM-DD"
  startTime: string; // ISO time "HH:mm:ss"
  endTime: string;
  createdAt: string;
}

/** Request đặt lịch hẹn */
export interface BookAppointmentRequest {
  scheduleSlotId: string;
  reason?: string;
  symptoms?: SymptomInput[];
  relationshipId?: string;
}

export interface SymptomInput {
  categoryId: string;
  symptomId?: string | null;
  otherNote?: string | null;
}

export const MAX_BOOKING_DAYS = 30; // Đồng bộ Mobile & Backend

/** Chi tiết lịch hẹn sau khi đặt thành công */
export interface AppointmentResponse {
  appointmentId: string;
  scheduleSlotId: string;
  slotDate: string;
  startTime: string;
  endTime: string;
  doctorName: string;
  status: AppointmentStatus;
  reason: string | null;
  cancellationReason: string | null;
  calendarSyncedAt: string | null;
  createdAt: string;
  caseId: string | null;
}

/** Tóm tắt lịch hẹn — dùng cho danh sách "Lịch hẹn của tôi" */
export interface AppointmentSummaryResponse {
  appointmentId: string;
  scheduleSlotId: string;
  doctorId: string;
  slotDate: string;
  startTime: string;
  endTime: string;
  doctorName: string;
  status: AppointmentStatus;
  createdAt: string;
  reason: string | null;
  cancellationReason: string | null;
  caseId: string | null;
}
