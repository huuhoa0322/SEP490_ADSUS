"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  closeScheduleSlot,
  createScheduleSlot,
  getScheduleSlot,
  listAppointmentsBySlot,
  searchScheduleSlots,
} from "../api/schedule-slots.api";

import type {
  CreateScheduleSlotRequest,
  ScheduleSlotSearch,
} from "../types/schedule-slot.types";

const keys = {
  all: ["schedule-slots"] as const,
  lists: () => [...keys.all, "list"] as const,
  list: (q: ScheduleSlotSearch) => [...keys.lists(), q] as const,
  details: () => [...keys.all, "detail"] as const,
  detail: (id: string) => [...keys.details(), id] as const,
  appointments: (id: string) => [...keys.all, "appointments", id] as const,
};

export function useScheduleSlotList(q: ScheduleSlotSearch) {
  return useQuery({ queryKey: keys.list(q), queryFn: () => searchScheduleSlots(q) });
}

export function useScheduleSlotDetail(id?: string) {
  return useQuery({
    queryKey: keys.detail(id!),
    queryFn: () => getScheduleSlot(id!),
    enabled: !!id,
  });
}

export function useScheduleSlotAppointments(slotId?: string) {
  return useQuery({
    queryKey: keys.appointments(slotId!),
    queryFn: () => listAppointmentsBySlot(slotId!),
    enabled: !!slotId,
  });
}

export function useCreateScheduleSlot() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: CreateScheduleSlotRequest) => createScheduleSlot(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.lists() }),
  });
}

export function useCloseScheduleSlot() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => closeScheduleSlot(id),
    onSuccess: (closed) => {
      qc.invalidateQueries({ queryKey: keys.lists() });
      qc.setQueryData(keys.detail(closed.slotId), closed);
    },
  });
}