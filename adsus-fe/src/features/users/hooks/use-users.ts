"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  createUser,
  deactivateUser,
  getUserById,
  resetUserPassword,
  searchUsers,
  setUserLocked,
  updateUser,
} from "../api/users.api";
import type {
  CreateUserAccountRequest,
  UpdateUserAccountRequest,
  UserListQuery,
} from "../types/user.types";

/**
 * Khoá cache. Gom về một chỗ để sau khi tạo/sửa/khoá tài khoản thì làm mới đúng danh sách,
 * không phải nhớ chuỗi khoá rải rác khắp nơi.
 */
export const usersQueryKeys = {
  all: ["admin", "users"] as const,
  list: (query: UserListQuery) => [...usersQueryKeys.all, "list", query] as const,
  detail: (userId: string) => [...usersQueryKeys.all, "detail", userId] as const,
};

/** SCR-06 — danh sách tài khoản. */
export function useUserList(query: UserListQuery) {
  return useQuery({
    queryKey: usersQueryKeys.list(query),
    queryFn: () => searchUsers(query),
    // Giữ dữ liệu trang cũ trong lúc tải trang mới, để bảng không nháy trắng mỗi lần gõ tìm kiếm.
    placeholderData: (previous) => previous,
  });
}

/** SCR-07 — nạp tài khoản vào form sửa. */
export function useUserDetail(userId: string | undefined) {
  return useQuery({
    queryKey: usersQueryKeys.detail(userId ?? ""),
    queryFn: () => getUserById(userId!),
    enabled: Boolean(userId),
  });
}

export function useCreateUser() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: CreateUserAccountRequest) => createUser(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: usersQueryKeys.all });
    },
  });
}

export function useUpdateUser(userId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: UpdateUserAccountRequest) => updateUser(userId, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: usersQueryKeys.all });
    },
  });
}

/** FT-08 — khoá / mở khoá. Đổi trạng thái xong phải nạp lại danh sách để thấy nhãn mới. */
export function useSetUserLocked() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ userId, locked }: { userId: string; locked: boolean }) =>
      setUserLocked(userId, locked),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: usersQueryKeys.all });
    },
  });
}

/**
 * UC-03 AF-02 — Admin cấp lại mật khẩu hộ.
 *
 * Không cần làm mới danh sách: thao tác này chỉ đổi mật khẩu và cờ buộc đổi, không đổi thứ
 * gì đang hiển thị trên bảng.
 */
export function useResetUserPassword() {
  return useMutation({
    mutationFn: (userId: string) => resetUserPassword(userId),
  });
}

/** FT-08 AF-02 — vô hiệu hoá vĩnh viễn. Màn hình phải hỏi xác nhận TRƯỚC khi gọi hook này. */
export function useDeactivateUser() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (userId: string) => deactivateUser(userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: usersQueryKeys.all });
    },
  });
}
