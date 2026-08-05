import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";

import type {
  CreatePatientProfileRequest,
  PatientProfile,
  UpdatePatientProfileRequest,
} from "../types/medical-record.types";

/** UC-06 — hồ sơ y tế nền. Cả Bác sĩ và Điều dưỡng đều xem và sửa được. */
const BASE = "/api/v1/patient-profiles";

export async function getPatientProfile(profileId: string): Promise<PatientProfile> {
  const { data } = await apiClient.get<ApiResponse<PatientProfile>>(`${BASE}/${profileId}`);

  if (!data.data) throw new Error(data.message || "Không tìm thấy hồ sơ nền.");

  return data.data;
}

/**
 * #17 — createdBy KHÔNG nằm trong payload: backend lấy từ token của người đang thao tác.
 * Nhận từ body thì ai cũng ghi tên người khác vào cột "người lập hồ sơ" được.
 */
export async function createPatientProfile(
  payload: CreatePatientProfileRequest,
): Promise<PatientProfile> {
  const { data } = await apiClient.post<ApiResponse<PatientProfile>>(BASE, payload);

  if (!data.data) throw new Error(data.message || "Tạo hồ sơ nền thất bại.");

  return data.data;
}

/**
 * #18 — thay TOÀN BỘ hồ sơ, phải gửi lại cả giá trị không đổi.
 *
 * Payload cố ý KHÔNG có fullName / phone / dateOfBirth: ba trường đó lấy từ bảng users và
 * chỉ đọc ở màn này (UC-06 bước 2). Muốn sửa thì đi qua endpoint tài khoản (chỉ Điều dưỡng).
 */
export async function updatePatientProfile(
  profileId: string,
  payload: UpdatePatientProfileRequest,
): Promise<PatientProfile> {
  const { data } = await apiClient.put<ApiResponse<PatientProfile>>(`${BASE}/${profileId}`, payload);

  if (!data.data) throw new Error(data.message || "Cập nhật hồ sơ nền thất bại.");

  return data.data;
}
