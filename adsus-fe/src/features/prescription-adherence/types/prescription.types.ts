/**
 * Module 7 — Prescription & Adherence (UC-11 + UC-18).
 * Khớp với response DTO ở ADSUS_BE.BLL.PrescriptionAdherence.DTOs.
 */

export type PrescriptionStatus = "ACTIVE" | "COMPLETED";
export type AdherenceLevel = "good" | "warning" | "poor";
export type IntakeStatus = "PENDING" | "TAKEN";
export type ScheduleSlotType = "MORNING" | "NOON" | "EVENING";

export interface PrescriptionListQuery {
  patientProfileId: string;
  status?: "ALL" | "ACTIVE" | "COMPLETED";
  from?: string; // ISO date YYYY-MM-DD
  to?: string;
  page?: number;
  pageSize?: number;
}

export interface PrescriptionListItem {
  prescriptionId: string;
  caseId: string;
  doctorId: string;
  doctorName: string;
  prescribedDate: string;
  status: PrescriptionStatus;
  itemCount: number;
  adherencePercent: number;
  adherenceLevel: AdherenceLevel;
  createdAt: string;
}

export interface PrescriptionListResponse {
  items: PrescriptionListItem[];
  total: number;
  page: number;
  pageSize: number;
}

export interface PrescriptionItemDetail {
  prescriptionItemId: string;
  medicineId: string;
  medicineName: string;
  dosage: string;
  durationDays: number;
  startDate: string;
  instructions: string | null;
  totalDoses: number;
  takenDoses: number;
  pendingDoses: number;
  adherencePercent: number;
  adherenceLevel: AdherenceLevel;
}

export interface PrescriptionDetail {
  prescriptionId: string;
  caseId: string;
  patientProfileId: string;
  patientName: string;
  doctorId: string;
  doctorName: string;
  prescribedDate: string;
  status: PrescriptionStatus;
  generalNote: string | null;
  createdAt: string;
  updatedAt: string;
  items: PrescriptionItemDetail[];
  adherencePercent: number;
  adherenceLevel: AdherenceLevel;
}

export interface IntakeLogItem {
  intakeId: string;
  prescriptionItemId: string;
  medicineName: string;
  scheduledTime: string;
  confirmedAt: string | null;
  status: IntakeStatus;
}

export interface IntakeLogListResponse {
  prescriptionId: string;
  items: IntakeLogItem[];
}

export interface MedicineItem {
  medicineId: string;
  name: string;
}

export interface ScheduleSlotPayload {
  slot: ScheduleSlotType;
}

export interface CreatePrescriptionItemRequest {
  medicineId: string;
  dosage: string;
  durationDays: number;
  startDate: string;
  instructions?: string | null;
  scheduleSlots: ScheduleSlotPayload[];
}

export interface CreatePrescriptionRequest {
  caseId: string;
  generalNote?: string | null;
  items: CreatePrescriptionItemRequest[];
}
