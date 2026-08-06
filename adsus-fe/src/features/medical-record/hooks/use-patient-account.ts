"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  createPatientAccount,
  getPatientAccount,
  resetPatientAccountPassword,
  updatePatientAccountContact,
} from "../api/patient-accounts.api";
import type {
  CreatePatientAccountRequest,
  UpdatePatientAccountRequest,
} from "../types/medical-record.types";

import { medicalRecordQueryKeys } from "./query-keys";

/**
 * UC-06 AF-01/AF-02/AF-03 — CHỈ Điều dưỡng.
 *
 * Backend chặn bằng [Authorize(Roles="NURSE")]. Component gọi các hook này phải tự ẩn nút
 * khỏi Bác sĩ (xem `patient-account-actions.tsx`) — để anh ấy bấm rồi nhận 403 là trải
 * nghiệm tệ, và trái nguyên tắc "không bày ra hành động không dùng được".
 */
export function useCreatePatientAccount() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: CreatePatientAccountRequest) => createPatientAccount(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: medicalRecordQueryKeys.all });
    },
  });
}

/**
 * AF-02 phần đọc (thêm 06/08/2026, review Task C11) — PatientProfile không có email, nên
 * form Sửa thông tin tài khoản tự nạp email thật qua đây thay vì nhận từ prop tĩnh. Nạp ngay
 * khi khối tài khoản hiện ra (không đợi bấm Sửa) để có sẵn khi Điều dưỡng cần.
 */
export function usePatientAccount(userId: string, enabled: boolean) {
  return useQuery({
    queryKey: medicalRecordQueryKeys.account(userId),
    queryFn: () => getPatientAccount(userId),
    // Endpoint chỉ [Authorize(Roles="NURSE")] — không gọi khi vai trò khác, tránh một request
    // chắc chắn nhận 403 mỗi lần Bác sĩ mở màn này.
    enabled,
  });
}

export function useUpdatePatientAccountContact(userId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: UpdatePatientAccountRequest) =>
      updatePatientAccountContact(userId, payload),
    onSuccess: () => {
      // Họ tên và SĐT hiện ở cả danh sách bệnh nhân lẫn hồ sơ nền, nên làm mới từ gốc.
      queryClient.invalidateQueries({ queryKey: medicalRecordQueryKeys.all });
    },
  });
}

/**
 * UC-06 AF-03 — cấp lại mật khẩu.
 *
 * KHÔNG invalidate gì: thao tác này chỉ đổi mật khẩu và cờ buộc đổi, không đổi thứ gì đang
 * hiển thị trên màn hình.
 */
export function useResetPatientAccountPassword(userId: string) {
  return useMutation({
    mutationFn: () => resetPatientAccountPassword(userId),
  });
}
