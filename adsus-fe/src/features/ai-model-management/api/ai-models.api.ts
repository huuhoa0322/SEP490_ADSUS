import { apiClient } from "@/lib/api-client";
import { translateApiMessage } from "@/lib/api-messages";
import type { ApiResponse } from "@/types/api.types";

import type {
  ActivateVersionRequest,
  AiModelListQuery,
  AiModelVersion,
  PagedResult,
  RegisterModelVersionRequest,
  UpdateModelVersionRequest,
} from "../types/ai-model.types";

const BASE = "/api/v1/ai-model-versions";

export async function getAiModels(query: AiModelListQuery): Promise<PagedResult<AiModelVersion>> {
  const { data } = await apiClient.get<ApiResponse<PagedResult<AiModelVersion>>>(BASE, {
    params: {
      keyword: query.keyword || undefined,
      page: query.page ?? 1,
      pageSize: query.pageSize ?? 20,
    },
  });
  if (!data.data) throw new Error(data.message || "Không tải được danh sách mô hình.");
  return data.data;
}

export async function getAiModelById(id: string): Promise<AiModelVersion> {
  const { data } = await apiClient.get<ApiResponse<AiModelVersion>>(`${BASE}/${id}`);
  if (!data.data) throw new Error(data.message || "Không tìm thấy phiên bản mô hình.");
  return data.data;
}

export async function registerAiModel(payload: RegisterModelVersionRequest): Promise<{ data: AiModelVersion; message: string }> {
  const { data } = await apiClient.post<ApiResponse<AiModelVersion>>(BASE, payload);
  if (!data.data) throw new Error(data.message || "Đăng ký mô hình thất bại.");
  return {
    data: data.data,
    message: data.message ? translateApiMessage(data.message) : "Đã đăng ký phiên bản mô hình mới.",
  };
}

export async function updateAiModel(id: string, payload: UpdateModelVersionRequest): Promise<void> {
  await apiClient.put<ApiResponse<null>>(`${BASE}/${id}`, payload);
}

export async function activateAiModel(id: string): Promise<void> {
  const payload: ActivateVersionRequest = { status: "ACTIVE" };
  await apiClient.patch<ApiResponse<null>>(`${BASE}/${id}`, payload);
}
