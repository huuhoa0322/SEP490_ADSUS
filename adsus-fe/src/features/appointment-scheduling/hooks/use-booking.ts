"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  bookAppointment,
  cancelAppointment,
  getMyAppointments,
  getOpenSlots,
} from "../api/booking.api";
import type { BookAppointmentRequest } from "../types/booking.types";

/** Danh sách slot còn trống. */
export function useOpenSlots(params?: {
  doctorId?: string;
  fromDate?: string;
  toDate?: string;
}) {
  return useQuery({
    queryKey: ["appointments", "open-slots", params],
    queryFn: () => getOpenSlots(params),
  });
}

/** Lịch hẹn của tôi. */
export function useMyAppointments(status?: string) {
  return useQuery({
    queryKey: ["appointments", "my", status ? { status } : {}],
    queryFn: () => getMyAppointments(status ? { status } : {}),
  });
}

/** Đặt lịch hẹn mới. */
export function useBookAppointment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (request: BookAppointmentRequest) => bookAppointment(request),
    onSuccess: () => {
      // Invalidate cả open slots lẫn my appointments để list cập nhật
      void qc.invalidateQueries({ queryKey: ["appointments", "open-slots"] });
      void qc.invalidateQueries({ queryKey: ["appointments", "my"] });
    },
  });
}

/** Hủy lịch hẹn. */
export function useCancelAppointment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ appointmentId, reason }: { appointmentId: string; reason: string }) =>
      cancelAppointment(appointmentId, reason),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["appointments", "my"] });
      void qc.invalidateQueries({ queryKey: ["appointments", "open-slots"] });
    },
  });
}
