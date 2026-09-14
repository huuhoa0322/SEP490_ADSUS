import { apiClient } from "@/lib/api-client";
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

/** POST /api/v1/relatives — Thêm người thân mới */
export async function addRelative(request: AddRelativeRequest): Promise<RelativeResponse> {
  const { data } = await apiClient.post<RelativeResponse>("/api/v1/relatives", request);
  return data;
}

/** GET /api/v1/relatives/check-phone?phone={phone} — Kiểm tra số điện thoại đã đăng ký tài khoản chưa */
export async function checkPhoneRegistered(phone: string): Promise<CheckPhoneResponse> {
  const { data } = await apiClient.get<CheckPhoneResponse>("/api/v1/relatives/check-phone", {
    params: { phone },
  });
  return data;
}
