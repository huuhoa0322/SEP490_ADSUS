import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";
import type {
  RelativeResponse,
  RelativesListResponse,
  AddRelativeRequest,
  CheckPhoneResponse,
} from "../types/relatives.types";

/** GET /api/v1/relatives — Lấy danh sách người thân của bệnh nhân */
export async function getRelatives(): Promise<RelativeResponse[]> {
  const { data } = await apiClient.get<RelativesListResponse>("/api/v1/relatives");
  return data.relatives ?? [];
}

/** GET /api/v1/relatives/guardian/{guardianUserId} — Staff lấy danh sách người thân của bệnh nhân */
export async function getRelativesForGuardian(guardianUserId: string): Promise<RelativeResponse[]> {
  try {
    const { data } = await apiClient.get<ApiResponse<RelativesListResponse>>(
      `/api/v1/relatives/guardian/${guardianUserId}`
    );
    return data.data?.relatives ?? [];
  } catch {
    return [];
  }
}

/** POST /api/v1/relatives — Thêm người thân mới (Bệnh nhân tự tạo) */
export async function addRelative(request: AddRelativeRequest): Promise<RelativeResponse> {
  const { data } = await apiClient.post<RelativeResponse>("/api/v1/relatives", request);
  return data;
}

/** POST /api/v1/relatives/guardian/{guardianUserId} — Staff tạo người thân cho bệnh nhân */
export async function addRelativeForGuardian(
  guardianUserId: string,
  request: AddRelativeRequest
): Promise<RelativeResponse> {
  const { data } = await apiClient.post<ApiResponse<RelativeResponse>>(
    `/api/v1/relatives/guardian/${guardianUserId}`,
    request
  );
  return data.data!;
}

/** GET /api/v1/relatives/check-phone?phone={phone} — Kiểm tra số điện thoại đã đăng ký tài khoản chưa */
export async function checkPhoneRegistered(phone: string): Promise<CheckPhoneResponse> {
  const { data } = await apiClient.get<CheckPhoneResponse>("/api/v1/relatives/check-phone", {
    params: { phone },
  });
  return data;
}
