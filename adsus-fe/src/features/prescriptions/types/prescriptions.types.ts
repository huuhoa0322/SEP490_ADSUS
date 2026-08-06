/**
 * Types cho Module 7 — Đơn thuốc & Tuân thủ (Prescription & Compliance).
 *
 * Backend API: /api/v1/prescriptions, /api/v1/me/medication-intakes, /api/v1/medication-catalog
 * Source: ADSUS_BE.BLL/PrescriptionAdherence/DTOs/{CreatePrescriptionRequest, IntakeLogResponse, PrescriptionResponse, AdherenceSummary}.cs
 *
 * Backend đã bật `PropertyNameCaseInsensitive = true` ở Program.cs (xem CLAUDE.md §22.2 fix #3),
 * nên JSON camelCase từ client bind được vào record PascalCase. Status ở IntakeLogResponse là
 * string thuần ("PENDING" / "TAKEN") — derive từ ConfirmedAt.
 */

/**
 * Khung giờ uống thuốc. Phải khớp enum ScheduleSlot ở backend C# (Morning/Noon/Evening).
 * JSON serialize mặc định của System.Text.Json giữ nguyên tên PascalCase.
 * §3.1 GB-03 nghiệp vụ: 1 dòng thuốc phải có ≥1 khung uống — validate ở frontend lẫn handler.
 */
export type ScheduleSlot = "Morning" | "Noon" | "Evening";

/** Trạng thái 1 intake log — derive từ ConfirmedAt ở backend. */
export type IntakeStatus = "PENDING" | "TAKEN";

/** 1 dòng thuốc trong đơn kê (POST /api/v1/prescriptions body). */
export interface CreatePrescriptionItemDto {
  medicineId: string;
  dosage: string;
  /** 1..365 — backend validate [Range(1, 365)]. */
  durationDays: number;
  /** ISO yyyy-MM-dd. */
  startDate: string;
  instructions?: string;
  /** ≥1 phần tử — backend validate [MinLength(1)]. */
  scheduleSlots: ScheduleSlot[];
}

/** Body POST /api/v1/prescriptions (UC-18). */
export interface CreatePrescriptionRequest {
  caseId: string;
  doctorId: string;
  generalNote?: string;
  items: CreatePrescriptionItemDto[];
}

/** 1 dòng thuốc trong response (include MedicineName). */
export interface PrescriptionItemResponse {
  prescriptionItemId: string;
  medicineId: string;
  medicineName: string;
  dosage: string;
  durationDays: number;
  /** ISO yyyy-MM-dd. */
  startDate: string;
  instructions?: string;
}

/** Response POST /api/v1/prescriptions + GET /api/v1/prescriptions/{id}. */
export interface PrescriptionResponse {
  prescriptionId: string;
  caseId: string;
  doctorId: string;
  /** ISO yyyy-MM-dd. */
  prescribedDate: string;
  generalNote?: string;
  /** ISO 8601 UTC. */
  createdAt: string;
  /** ISO 8601 UTC. */
  updatedAt: string;
  items: PrescriptionItemResponse[];
}

/** 1 lịch uống (response GET /me/medication-intakes). */
export interface IntakeLogResponse {
  intakeId: string;
  prescriptionItemId: string;
  /** ISO 8601 UTC — client convert sang local time để hiển thị. */
  scheduledTime: string;
  confirmedAt: string | null;
  status: IntakeStatus;
}

/** 1 mục trong medication catalog (GET /api/v1/medication-catalog). */
export interface MedicationCatalogItem {
  /** Tên trường theo entity Medicine — backend chỉ trả MedicineId + Name. */
  medicineId: string;
  name: string;
}

/** Response của GET /api/v1/medication-catalog (trả về array trực tiếp, không paginate). */
export type MedicationCatalogResponse = MedicationCatalogItem[];