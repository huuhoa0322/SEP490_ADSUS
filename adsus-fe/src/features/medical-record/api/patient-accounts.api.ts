import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";

import type {
  CreatePatientAccountRequest,
  PatientAccount,
  PatientAccountCreated,
  UpdatePatientAccountRequest,
} from "../types/medical-record.types";

/**
 * UC-06 AF-01/AF-02/AF-03 (quyết định ghi đè 04/08/2026) — CHỈ Điều dưỡng.
 *
 * Backend chặn bằng [Authorize(Roles="NURSE")]; giao diện phải ẩn hẳn các nút này khỏi Bác
 * sĩ chứ đừng để anh ấy bấm rồi nhận 403.
 *
 * Ba endpoint dưới đây nằm ngoài API Catalog v1.1 — xem Flags Summary.
 */
const BASE = "/api/v1/patients";

/**
 * AF-01 — sửa lại 06/08/2026: response giờ trả `temporaryPassword` plaintext đúng một lần,
 * không còn gửi email chứa mật khẩu. Xem `PatientAccountCreated`.
 */
export async function createPatientAccount(
  payload: CreatePatientAccountRequest,
): Promise<PatientAccountCreated> {
  const { data } = await apiClient.post<ApiResponse<PatientAccountCreated>>(BASE, payload);

  if (!data.data) throw new Error(data.message || "Tạo tài khoản bệnh nhân thất bại.");

  return data.data;
}

export async function updatePatientAccountContact(
  userId: string,
  payload: UpdatePatientAccountRequest,
): Promise<PatientAccount> {
  const { data } = await apiClient.put<ApiResponse<PatientAccount>>(`${BASE}/${userId}`, payload);

  if (!data.data) throw new Error(data.message || "Cập nhật thông tin tài khoản thất bại.");

  return data.data;
}

/**
 * UC-06 AF-03 — sinh mật khẩu tạm và gửi email.
 *
 * TRẢ VỀ void, có chủ ý: BR-05 nói Điều dưỡng không bao giờ thấy mật khẩu. API không trả nó
 * về, và kiểu void khiến component không thể vô tình hiển thị thứ gì.
 */
export async function resetPatientAccountPassword(userId: string): Promise<void> {
  await apiClient.put<ApiResponse<null>>(`${BASE}/${userId}/reset-password`);
}
