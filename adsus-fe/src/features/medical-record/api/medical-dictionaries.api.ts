import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";

import type { MedicalAllergyType, MedicalDisease } from "../types/medical-record.types";

const BASE = "/api/v1/medical-dictionaries";

export async function listDiseases(): Promise<MedicalDisease[]> {
  const { data } = await apiClient.get<ApiResponse<MedicalDisease[]>>(`${BASE}/diseases`);

  if (!data.data) throw new Error(data.message || "Không tải được danh mục bệnh nền.");

  return data.data;
}

export async function listAllergyTypes(): Promise<MedicalAllergyType[]> {
  const { data } = await apiClient.get<ApiResponse<MedicalAllergyType[]>>(`${BASE}/allergy-types`);

  if (!data.data) throw new Error(data.message || "Không tải được danh mục dị ứng.");

  return data.data;
}
