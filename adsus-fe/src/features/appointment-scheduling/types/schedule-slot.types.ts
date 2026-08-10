/**
 * UC-15 — Manage Clinic Schedule (Module 8).
 * BR-01: slot không trong quá khứ; range > 15 phút; không overlap cùng Doctor.
 * BR-02: Closed là terminal; không thể reopen.
 */

export type SlotStatus = "OPEN" | "BOOKED" | "CLOSED";

export type AppointmentStatus = "BOOKED" | "CANCELLED";

/** Thông tin booking bên trong ScheduleSlotResponse */
export interface BookedAppointmentInfo {
  appointmentId: string;
  patientProfileId: string;
  patientFullName: string;
  reason: string | null;
  status: AppointmentStatus;
}

export interface ScheduleSlotResponse {
  slotId: string;
  doctorId: string;
  doctorName: string;
  slotDate: string; // ISO date "YYYY-MM-DD"
  startTime: string; // ISO time "HH:mm:ss"
  endTime: string;
  status: SlotStatus;
  activeAppointmentsCount: number;
  bookedAppointments: BookedAppointmentInfo[]; // Chi tiết bệnh nhân đã book
  createdAt: string;
  updatedAt: string;
}

export interface CreateScheduleSlotRequest {
  visitDate: string;
  startTime: string;
  endTime: string;
}

export interface UpdateScheduleSlotRequest {
  startTime: string;
  endTime: string;
}

export interface CloseSlotImpactResponse {
  slotId: string;
  affectedBookingsCount: number;
}

export interface ListSlotsParams {
  fromDate?: string;
  toDate?: string;
  status?: SlotStatus;
  pageSize?: number; // Mặc định 200 để lấy đủ 14 ngày × 16 slots
}
