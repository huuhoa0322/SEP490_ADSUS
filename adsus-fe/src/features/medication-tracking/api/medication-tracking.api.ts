import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";

// ─── Types ────────────────────────────────────────────────────────────────────

export interface AdherenceDto {
  taken: number;
  total: number;
  percent: number;
}

export interface TodayDoseDto {
  intakeId: string;
  medicineName: string;
  dosage: string;
  scheduledTime: string; // "HH:mm" local
  status: "PENDING" | "OVERTIME" | "TAKEN";
}

export interface PrescriptionCardDto {
  prescriptionId: string;
  caseId: string;
  caseName: string;
  todayDoses: TodayDoseDto[];
  adherenceToday: AdherenceDto;
  adherenceOverall: AdherenceDto;
}

export interface DoctorPatientDto {
  patientProfileId: string;
  patientName: string;
  todayTaken: number;
  todayTotal: number;
  todayAdherencePercent: number;
  adherenceLevel: "good" | "warning" | "poor";
  hasOverdueToday: boolean;
  activePrescriptionCount: number;
}

export interface DoctorPatientListResponse {
  patients: DoctorPatientDto[];
  totalCount: number;
}

export interface PatientPrescriptionDetailResponse {
  patientName: string;
  prescriptions: PrescriptionCardDto[];
}

export interface RemindRequest {
  prescriptionId: string;
}

export interface RemindResponse {
  sentCount: number;
  message: string;
}

// ─── API calls ────────────────────────────────────────────────────────────────

/**
 * GET /api/v1/me/medication-tracking/patients
 * List bệnh nhân có đơn Active do bác sĩ kê.
 */
export async function getPatientList(params?: {
  search?: string;
  adherenceLevel?: string;
  hasOverdueDoses?: boolean;
}): Promise<DoctorPatientListResponse> {
  const { data } = await apiClient.get<
    ApiResponse<DoctorPatientListResponse>
  >("/api/v1/me/medication-tracking/patients", { params });
  if (!data.data) {
    return { patients: [], totalCount: 0 };
  }
  return data.data;
}

/**
 * GET /api/v1/me/medication-tracking/patients/{patientId}/prescriptions
 * Đơn Active của bệnh nhân + liều hôm nay + adherence.
 */
export async function getPatientPrescriptions(
  patientId: string,
): Promise<PatientPrescriptionDetailResponse> {
  const { data } = await apiClient.get<
    ApiResponse<PatientPrescriptionDetailResponse>
  >(
    `/api/v1/me/medication-tracking/patients/${patientId}/prescriptions`,
  );
  if (!data.data) {
    throw new Error(data.message || "Không tải được đơn thuốc.");
  }
  return data.data;
}

/**
 * POST /api/v1/me/medication-tracking/patients/{patientId}/remind
 * Gửi notification nhắc liều PENDING/OVERTIME hôm nay.
 */
export async function sendReminders(
  patientId: string,
  request: RemindRequest,
): Promise<RemindResponse> {
  const { data } = await apiClient.post<
    ApiResponse<RemindResponse>
  >(
    `/api/v1/me/medication-tracking/patients/${patientId}/remind`,
    request,
  );
  if (!data.data) {
    throw new Error(data.message || "Không gửi được nhắc nhở.");
  }
  return data.data;
}
