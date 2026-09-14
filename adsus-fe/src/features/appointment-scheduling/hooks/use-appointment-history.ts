"use client";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  cancelAppointment,
  getCancellationStatusToday,
  getMyAppointments,
} from "../api/booking.api";

/** Lịch hẹn của tôi */
export function useAppointmentHistory() {
  return useQuery({
    queryKey: ["appointments", "my-history"],
    queryFn: () => getMyAppointments(),
    staleTime: 30_000,
  });
}

/** Trạng thái hủy lịch trong ngày — chỉ fetch khi enabled=true */
export function useCancellationStatusToday(enabled: boolean = false) {
  return useQuery({
    queryKey: ["appointments", "cancellation-status-today"],
    queryFn: () => getCancellationStatusToday(),
    enabled,
  });
}

/** Hủy lịch hẹn — invalidate cả history và cancellation-status */
export function useCancelMyAppointment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({
      appointmentId,
      reason,
    }: {
      appointmentId: string;
      reason: string;
    }) => cancelAppointment(appointmentId, reason),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["appointments", "my-history"] });
      void qc.invalidateQueries({
        queryKey: ["appointments", "cancellation-status-today"],
      });
    },
  });
}
