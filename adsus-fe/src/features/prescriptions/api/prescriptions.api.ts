import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";

import type {
  CreatePrescriptionRequest,
  IntakeLogResponse,
  MedicationCatalogItem,
  PrescriptionResponse,
} from "../types/prescriptions.types";

/**
 * Module 7 API — Đơn thuốc & Tuân thủ.
 *
 * apiClient ở @/lib/api-client đã có:
 *   - Authorization: Bearer <token> từ localStorage key 'adsus.accessToken'
 *   - Timeout 15s
 *   - 401 → xoá token + redirect /login?expired=1
 *
 * KHÔNG tự gắn token vào header. KHÔNG dùng fetch/native axios.
 */

/**
 * POST /api/v1/prescriptions — Doctor kê đơn (UC-18).
 *
 * Backend validate Doctor role + case thuộc doctor (GB-04). Trả 201 Created + Location header.
 * §3.1 nghiệp vụ:
 *   - durationDays: 1..365
 *   - scheduleSlots: ≥1 phần tử (Morning/Noon/Evening)
 *   - items: ≥1 phần tử
 */
export async function createPrescription(
  request: CreatePrescriptionRequest,
): Promise<PrescriptionResponse> {
  const { data } = await apiClient.post<ApiResponse<PrescriptionResponse>>(
    "/api/v1/prescriptions",
    request,
  );
  if (!data.data) {
    throw new Error(data.message || "Không tạo được đơn thuốc.");
  }
  return data.data;
}

/**
 * GET /api/v1/prescriptions/{id} — Doctor/Nurse xem chi tiết đơn (UC-17).
 * Controller trả null ở thời điểm hiện tại (§22.6 B TODO) — null → throw.
 */
export async function getPrescription(
  id: string,
): Promise<PrescriptionResponse> {
  const { data } = await apiClient.get<ApiResponse<PrescriptionResponse>>(
    `/api/v1/prescriptions/${id}`,
  );
  if (!data.data) {
    throw new Error(data.message || "Không tải được đơn thuốc.");
  }
  return data.data;
}

/**
 * GET /api/v1/me/medication-intakes — Patient xem danh sách giờ uống (SCR-19).
 * Trả về mảng phẳng — frontend sắp xếp theo scheduledTime asc.
 */
export async function getMyIntakeLogs(): Promise<IntakeLogResponse[]> {
  const { data } = await apiClient.get<ApiResponse<IntakeLogResponse[]>>(
    "/api/v1/me/medication-intakes",
  );
  if (!data.data) {
    return [];
  }
  return data.data;
}

/**
 * GET /api/v1/me/medication-intakes/prescription/{prescriptionId} — Patient xem lịch uống của 1 đơn.
 */
export async function getIntakeLogsByPrescription(
  prescriptionId: string,
): Promise<IntakeLogResponse[]> {
  const { data } = await apiClient.get<ApiResponse<IntakeLogResponse[]>>(
    `/api/v1/me/medication-intakes/prescription/${prescriptionId}`,
  );
  if (!data.data) {
    return [];
  }
  return data.data;
}

/**
 * POST /api/v1/me/medication-intakes/{id}/confirm — Patient xác nhận đã uống (UC-19/20).
 *
 * Backend trả 204 No Content. Idempotent: confirm 2 lần không double-update (§22.2 fix #7).
 * Reject 400 nếu scheduledTime > now (Backend fix #7 — chống gian lận tuân thủ).
 *
 * KHÔNG throw nếu 204 — hàm trả void. Caller dựa vào HTTP status để biết lỗi.
 * Lỗi 400/401/403/404 sẽ bị AxiosError reject lên TanStack Query mutation.
 */
export async function confirmIntake(intakeId: string): Promise<void> {
  await apiClient.post<ApiResponse<null>>(
    `/api/v1/me/medication-intakes/${intakeId}/confirm`,
  );
}

/**
 * GET /api/v1/cases/my?status=Confirmed — Danh sách ca khám đang Confirmed của Doctor hiện tại.
 *
 * Dùng cho form kê đơn (SCR-17 / UC-18): bác sĩ chỉ được kê đơn cho ca của chính mình,
 * và ca phải ở trạng thái Confirmed (GB-04 mock — chỉ check Role + Status, không check license).
 *
 * Response shape tương tự CaseSummary: { caseId, patientProfileId, patientName, patientCode, ... }
 */
export async function listMyCases() {
  const { data } = await apiClient.get<ApiResponse<Array<{
    caseId: string;
    patientProfileId: string;
    patientName: string;
    patientCode: string;
  }>>>("/api/v1/cases/my", {
    params: { status: "Confirmed" },
  });
  return data.data ?? [];
}

/**
 * GET /api/v1/medication-catalog — Danh mục thuốc (Doctor chọn thuốc khi kê đơn).
 * Trả về array trực tiếp, không paginate.
 */
export async function getMedicationCatalog(): Promise<MedicationCatalogItem[]> {
  const { data } = await apiClient.get<ApiResponse<MedicationCatalogItem[]>>(
    "/api/v1/medication-catalog",
  );
  if (!data.data) {
    return [];
  }
  return data.data;
}