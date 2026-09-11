/**
 * Types cho Nurse Check-In feature
 */

export interface CheckinQueueItem {
  appointmentId: string;
  slotTime: string; // ISO datetime
  patientFullName: string;
  patientPhone: string | null;
  patientProfileId: string;
  caseId: string;
  reason: string | null;
  doctorName: string;
  status: "Booked" | "Approved" | "Completed" | "Cancelled" | "NoShow" | string;
  doctorId?: string;
  caseStatus?: string;
}

export interface RescheduleRequest {
  newScheduleSlotId: string;
  rescheduleReason: string;
  newReason?: string;
  autoCheckin: boolean;
}

export interface AvailableSlot {
  slotId: string;
  doctorId: string;
  doctorName: string;
  slotDate: string; // "yyyy-MM-dd"
  startTime: string; // "HH:mm:ss" or "HH:mm"
  endTime: string; // "HH:mm:ss" or "HH:mm"
}

export interface DoctorSummary {
  doctorId?: string;
  userId?: string;
  fullName: string;
  email?: string;
  specialty?: string;
  phone?: string;
}

export interface CheckinQueueParams {
  fromDate?: string;
  toDate?: string;
  date?: string;
  status?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface CheckinQueueResponse {
  items: CheckinQueueItem[];
  totalCount: number;
  page?: number;
  pageSize?: number;
  totalPages?: number;
  bookedCount?: number;
  checkedInCount?: number;
  cancelledCount?: number;
}

