"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  getRelatives,
  getRelativesForGuardian,
  addRelative,
  addRelativeForGuardian,
  checkPhoneRegistered,
} from "../api/relatives.api";
import type { AddRelativeRequest } from "../types/relatives.types";

/** Lấy danh sách người thân của bệnh nhân */
export function useRelatives(enabled = true) {
  return useQuery({
    queryKey: ["relatives"],
    queryFn: getRelatives,
    enabled,
  });
}

/** Staff lấy danh sách người thân của bệnh nhân theo guardianUserId */
export function useRelativesForGuardian(guardianUserId?: string) {
  return useQuery({
    queryKey: ["relatives", "guardian", guardianUserId],
    queryFn: () => getRelativesForGuardian(guardianUserId!),
    enabled: Boolean(guardianUserId),
  });
}

/** Thêm người thân mới */
export function useAddRelative() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: AddRelativeRequest) => addRelative(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["relatives"] });
    },
  });
}

/** Staff tạo người thân cho bệnh nhân theo guardianUserId */
export function useAddRelativeForGuardian(guardianUserId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: AddRelativeRequest) => {
      if (!guardianUserId) {
        throw new Error("Không có ID tài khoản bệnh nhân chính.");
      }
      return addRelativeForGuardian(guardianUserId, request);
    },
    onSuccess: () => {
      if (guardianUserId) {
        void queryClient.invalidateQueries({
          queryKey: ["relatives", "guardian", guardianUserId],
        });
      }
      void queryClient.invalidateQueries({ queryKey: ["relatives"] });
    },
  });
}

/** Kiểm tra số điện thoại đã đăng ký tài khoản chưa */
export function useCheckPhone() {
  return useMutation({
    mutationFn: (phone: string) => checkPhoneRegistered(phone),
  });
}
