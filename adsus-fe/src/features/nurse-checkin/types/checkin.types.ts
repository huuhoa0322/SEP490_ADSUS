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
  status: "Booked" | "Completed";
}

export interface CheckinQueueResponse {
  items: CheckinQueueItem[];
  totalCount: number;
}
