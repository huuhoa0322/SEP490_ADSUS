import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";
import type { SymptomCategory } from "../types/medical-record.types";

const BASE = "/api/v1/symptoms";

export async function getSymptomCategories(): Promise<SymptomCategory[]> {
  const { data } = await apiClient.get<ApiResponse<SymptomCategory[]>>(`${BASE}/categories`);

  if (!data.data) throw new Error(data.message || "Không tải được danh mục triệu chứng.");

  return data.data;
}
