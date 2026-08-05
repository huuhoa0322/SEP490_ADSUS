/**
 * UC-15 — Manage Clinic Schedule (Module 8).
 * BR-01: slot không trong quá khứ; range > 15 phút; không overlap cùng Doctor.
 * BR-02: Closed là terminal; không thể reopen.
 */

export type SlotStatus = "OPEN" | "CLOSED";

export interface ScheduleSlotResponse {
  slotId: string;
  doctorId: string;
  doctorName: string;
  slotDate: string; // ISO date "YYYY-MM-DD"
  startTime: string; // ISO time "HH:mm:ss"
  endTime: string;
  status: SlotStatus;
  activeAppointmentsCount: number;
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
}
