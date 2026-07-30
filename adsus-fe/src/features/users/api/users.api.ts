import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";

import type {
  CreateUserAccountRequest,
  PagedResult,
  UpdateUserAccountRequest,
  UserAccount,
  UserListQuery,
} from "../types/user.types";

/** UC-04 — mọi endpoint dưới đây đều đòi vai trò ADMIN, backend chặn bằng [Authorize]. */
const BASE = "/api/v1/admin/users";

export async function searchUsers(query: UserListQuery): Promise<PagedResult<UserAccount>> {
  const { data } = await apiClient.get<ApiResponse<PagedResult<UserAccount>>>(BASE, {
    // Bỏ hẳn tham số rỗng thay vì gửi chuỗi rỗng, để backend hiểu là "không lọc".
    params: {
      keyword: query.keyword || undefined,
      role: query.role || undefined,
      status: query.status || undefined,
      page: query.page ?? 1,
      pageSize: query.pageSize ?? 20,
    },
  });

  if (!data.data) throw new Error(data.message || "Không tải được danh sách tài khoản.");

  return data.data;
}

export async function getUserById(userId: string): Promise<UserAccount> {
  const { data } = await apiClient.get<ApiResponse<UserAccount>>(`${BASE}/${userId}`);

  if (!data.data) throw new Error(data.message || "Không tìm thấy tài khoản.");

  return data.data;
}

export async function createUser(payload: CreateUserAccountRequest): Promise<UserAccount> {
  const { data } = await apiClient.post<ApiResponse<UserAccount>>(BASE, payload);

  if (!data.data) throw new Error(data.message || "Tạo tài khoản thất bại.");

  return data.data;
}

export async function updateUser(
  userId: string,
  payload: UpdateUserAccountRequest,
): Promise<void> {
  await apiClient.put<ApiResponse<null>>(`${BASE}/${userId}`, payload);
}

/** FT-08 — khoá và mở khoá đều thủ công, không có job tự mở (UC-04 BR-04). */
export async function setUserLocked(userId: string, locked: boolean): Promise<void> {
  await apiClient.put<ApiResponse<null>>(`${BASE}/${userId}/${locked ? "lock" : "unlock"}`);
}

/** FT-08 AF-02 — MỘT CHIỀU, không có đường quay lại (BR-05). Phải hỏi xác nhận trước khi gọi. */
export async function deactivateUser(userId: string): Promise<void> {
  await apiClient.put<ApiResponse<null>>(`${BASE}/${userId}/deactivate`);
}

/**
 * UC-03 AF-02 — Admin cấp lại mật khẩu hộ, dùng khi chủ tài khoản không vào được email.
 *
 * Mật khẩu tạm CHỈ đi qua email; API không trả nó về và giao diện không bao giờ hiển thị
 * (BR-03). Tài khoản chưa khai email thì backend từ chối.
 */
export async function resetUserPassword(userId: string): Promise<void> {
  await apiClient.put<ApiResponse<null>>(`${BASE}/${userId}/reset-password`);
}
