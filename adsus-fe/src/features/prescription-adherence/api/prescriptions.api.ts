import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";

import type {
  CreatePrescriptionRequest,
  IntakeLogListResponse,
  MedicineItem,
  PrescriptionDetail,
  PrescriptionListQuery,
  PrescriptionListResponse,
} from "../types/prescription.types";

/**
 * Module 7 UC-11 — danh sách đơn thuốc của 1 bệnh nhân, có filter + phân trang.
 */
export async function getPrescriptionsByPatient(
  query: PrescriptionListQuery,
): Promise<PrescriptionListResponse> {
  const { data } = await apiClient.get<ApiResponse<PrescriptionListResponse>>(
    `/api/v1/patient-profiles/${query.patientProfileId}/prescriptions`,
    {
      params: {
        status: query.status,
        from: query.from,
        to: query.to,
        page: query.page ?? 1,
        pageSize: query.pageSize ?? 20,
      },
    },
  );
  if (!data.data) throw new Error(data.message || "Không tải được danh sách đơn thuốc.");
  return data.data;
}

/** Module 7 UC-11 — chi tiết 1 đơn + adherence. */
export async function getPrescriptionDetail(prescriptionId: string): Promise<PrescriptionDetail> {
  const { data } = await apiClient.get<ApiResponse<PrescriptionDetail>>(
    `/api/v1/prescriptions/${prescriptionId}`,
  );
  if (!data.data) throw new Error(data.message || "Không tải được chi tiết đơn thuốc.");
  return data.data;
}

/** Module 7 UC-11 — timeline liều thuốc. */
export async function getIntakeLogs(prescriptionId: string): Promise<IntakeLogListResponse> {
  const { data } = await apiClient.get<ApiResponse<IntakeLogListResponse>>(
    `/api/v1/prescriptions/${prescriptionId}/intake-logs`,
  );
  if (!data.data) throw new Error(data.message || "Không tải được lịch sử uống thuốc.");
  return data.data;
}

/** Module 7 UC-18 BR-01 — autocomplete thuốc cho bác sĩ khi kê đơn. */
export async function searchMedicines(keyword: string): Promise<MedicineItem[]> {
  const { data } = await apiClient.get<ApiResponse<MedicineItem[]>>("/api/v1/medicines", {
    params: { keyword },
  });
  return data.data ?? [];
}

/** Module 7 UC-18 — bác sĩ kê đơn mới. */
export async function createPrescription(
  payload: CreatePrescriptionRequest,
): Promise<PrescriptionDetail> {
  const { data } = await apiClient.post<ApiResponse<PrescriptionDetail>>(
    "/api/v1/prescriptions",
    payload,
  );
  if (!data.data) throw new Error(data.message || "Kê đơn thất bại.");
  return data.data;
}
