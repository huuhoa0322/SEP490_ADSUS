"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  closeScheduleSlot,
  createScheduleSlot,
  getScheduleSlot,
  getScheduleSlots,
} from "../api/schedule-slot.api";
import type {
  CreateScheduleSlotRequest,
  ListSlotsParams,
} from "../types/schedule-slot.types";

/**
 * Hooks cho UC-15 — Manage Clinic Schedule.
 * Roles: Doctor, Nurse.
 */

/**
 * Lấy danh sách slot theo range + filter.
 */
export function useScheduleSlots(params: ListSlotsParams = {}) {
  return useQuery({
    queryKey: ["schedule-slots", "list", params],
    queryFn: () => getScheduleSlots(params),
  });
}

/**
 * Lấy chi tiết 1 slot.
 */
export function useScheduleSlot(id: string | null) {
  return useQuery({
    queryKey: ["schedule-slots", "detail", id],
    queryFn: () => getScheduleSlot(id!),
    enabled: Boolean(id),
  });
}

/**
 * Tạo slot mới.
 */
export function useCreateScheduleSlot() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: CreateScheduleSlotRequest) => createScheduleSlot(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["schedule-slots", "list"] });
    },
  });
}

/**
 * Đóng slot. Nếu slot có booking, gọi với force=true để xác nhận.
 */
export function useCloseScheduleSlot() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, force }: { id: string; force: boolean }) =>
      closeScheduleSlot(id, force),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["schedule-slots"] });
    },
  });
}
