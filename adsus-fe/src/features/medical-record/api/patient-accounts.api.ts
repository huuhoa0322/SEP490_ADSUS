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

/**
 * AF-02 phần đọc (thêm 06/08/2026, review Task C11) — PatientProfile không có email nên form
 * Sửa thông tin tài khoản cần nguồn riêng, không thì lưu là xoá mất email đang có (UpdateContact
 * thay TOÀN BỘ 4 trường, BR-04).
 */
export async function getPatientAccount(userId: string): Promise<PatientAccount> {
  const { data } = await apiClient.get<ApiResponse<PatientAccount>>(`${BASE}/${userId}`);

  if (!data.data) throw new Error(data.message || "Không tải được thông tin tài khoản.");

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
 * UC-06 AF-03 — sinh mật khẩu tạm mới. Luôn trả plaintext MỘT LẦN để hiển thị ngay (quyết định
 * ghi đè 06/08/2026, mở rộng lần 2 — không còn phân biệt có/không có email nữa, không còn gửi
 * email ở đường này nữa), giống hệt cơ chế đã dùng cho luồng tạo tài khoản
 * (`PatientAccountCreated`).
 */
export async function resetPatientAccountPassword(userId: string): Promise<string> {
  const { data } = await apiClient.put<ApiResponse<string>>(`${BASE}/${userId}/reset-password`);

  if (!data.data) throw new Error(data.message || "Cấp lại mật khẩu thất bại.");

  return data.data;
}
