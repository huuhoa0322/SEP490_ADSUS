import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";
import type { CaseDetail, CaseDiagnosisInput, DiagnosisItem } from "../types/medical-record.types";

export async function getDiagnosisItems(): Promise<DiagnosisItem[]> {
  const { data } = await apiClient.get<ApiResponse<DiagnosisItem[]>>(
    "/api/v1/medical-dictionaries/diagnosis-items",
  );
  if (!data.data) throw new Error(data.message || "Không tải được danh mục chẩn đoán.");
  return data.data;
}

export async function updateCaseDiagnoses(
  caseId: string,
  diagnoses: CaseDiagnosisInput[],
): Promise<CaseDetail> {
  const { data } = await apiClient.put<ApiResponse<CaseDetail>>(
    `/api/v1/cases/${caseId}/diagnoses`,
    { diagnoses },
  );
  if (!data.data) throw new Error(data.message || "Cập nhật chẩn đoán thất bại.");
  return data.data;
}
