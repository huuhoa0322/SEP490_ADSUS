import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";

import type { DoctorSummary } from "../types/medical-record.types";

/**
 * GB-04 — danh sách Bác sĩ để chọn người phụ trách ca khám (UC-07 bước 5).
 *
 * Endpoint này nằm ngoài API Catalog v1.1, thêm mới cùng đợt Module 04 Frontend — xem
 * `Documents/05_APIs/API_Spec/00_API_Spec_Flags_Summary.md`.
 */
export async function listDoctors(): Promise<DoctorSummary[]> {
  const { data } = await apiClient.get<ApiResponse<DoctorSummary[]>>("/api/v1/doctors");

  if (!data.data) throw new Error(data.message || "Không tải được danh sách bác sĩ.");

  return data.data;
}
