"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  closeScheduleSlot,
  createScheduleSlot,
  ensureDefaultSlots,
  getScheduleSlot,
  getScheduleSlots,
  reopenScheduleSlot,
  updateScheduleSlot,
} from "../api/schedule-slot.api";
import type {
  CreateScheduleSlotRequest,
  ListSlotsParams,
  UpdateScheduleSlotRequest,
} from "../types/schedule-slot.types";

/** Lấy danh sách slot của Doctor đang đăng nhập. */
export function useScheduleSlots(params: ListSlotsParams = {}) {
  return useQuery({
    queryKey: ["schedule-slots", "list", params],
    queryFn: () => getScheduleSlots(params),
  });
}

/** Lấy chi tiết 1 slot. */
export function useScheduleSlot(id: string | null) {
  return useQuery({
    queryKey: ["schedule-slots", "detail", id],
    queryFn: () => getScheduleSlot(id!),
    enabled: Boolean(id),
  });
}

/** Tạo slot mới cho chính mình. */
export function useCreateScheduleSlot() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: CreateScheduleSlotRequest) => createScheduleSlot(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["schedule-slots", "list"] }),
  });
}

/** Sửa giờ slot (tách ca). */
export function useUpdateScheduleSlot() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: UpdateScheduleSlotRequest }) =>
      updateScheduleSlot(id, payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["schedule-slots"] }),
  });
}

/** Đóng slot (xin nghỉ/bận). */
export function useCloseScheduleSlot() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, force }: { id: string; force: boolean }) =>
      closeScheduleSlot(id, force),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["schedule-slots"] }),
  });
}

/** Tự sinh ca mặc định T2-T6 cho 1 tuần (gọi khi mở trang lần đầu). */
export function useEnsureDefaultSlots() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (weekStart: string) => ensureDefaultSlots(weekStart),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["schedule-slots", "list"] }),
  });
}

/** Mở lại slot đã đóng. */
export function useReopenScheduleSlot() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => reopenScheduleSlot(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["schedule-slots"] }),
  });
}