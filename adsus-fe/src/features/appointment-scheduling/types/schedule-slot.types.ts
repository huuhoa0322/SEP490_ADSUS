/**
 * Module 8 UC-15 — types khớp với BLL DTOs.
 */

export type SlotStatus = "OPEN" | "CLOSED";
export type AppointmentStatusType = "BOOKED" | "CANCELLED";

export interface ScheduleSlot {
  slotId: string;
  doctorId: string;
  doctorName: string;
  slotDate: string; // YYYY-MM-DD
  startTime: string; // HH:mm:ss
  endTime: string;
  status: SlotStatus;
  createdAt: string;
  updatedAt: string;
}

export interface PagedResult<T> {
  items: T[];
  totalItems: number;
  totalPages: number;
  page: number;
  pageSize: number;
}

export interface AppointmentSummary {
  appointmentId: string;
  patientProfileId: string;
  patientName: string;
  status: AppointmentStatusType;
  reason: string | null;
  cancelledReason: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface ScheduleSlotSearch {
  doctorId?: string;
  slotDate?: string;
  status?: SlotStatus;
  page?: number;
  pageSize?: number;
}

export interface CreateScheduleSlotRequest {
  doctorId: string;
  slotDate: string;
  startTime: string;
  endTime: string;
}

export interface UpdateScheduleSlotStatusRequest {
  status: "CLOSED";
}