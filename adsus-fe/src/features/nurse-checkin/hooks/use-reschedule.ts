"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import toast from "react-hot-toast";
import { getApiErrorMessage } from "@/lib/api-client";
import {
  getAvailableSlots,
  getDoctorList,
  rescheduleAppointment,
} from "../api/checkin.api";
import { nurseCheckinQueryKeys } from "../query-keys";
import type { RescheduleRequest } from "../types/checkin.types";

/**
 * Hook tải danh sách bác sĩ cho dropdown đổi lịch
 */
export function useDoctorList() {
  return useQuery({
    queryKey: nurseCheckinQueryKeys.doctors(),
    queryFn: getDoctorList,
    staleTime: 5 * 60 * 1000,
  });
}

/**
 * Hook tải danh sách khung giờ trống (OPEN) theo bác sĩ và ngày
 */
export function useAvailableSlots(params: {
  doctorId?: string;
  fromDate?: string;
  toDate?: string;
}) {
  const isEnabled = Boolean(params.doctorId && (params.fromDate || params.toDate));

  return useQuery({
    queryKey: nurseCheckinQueryKeys.availableSlots(params),
    queryFn: () => getAvailableSlots(params),
    enabled: isEnabled,
  });
}

/**
 * Hook thực hiện đổi lịch hẹn / tái đặt lịch hẹn
 */
export function useRescheduleAppointment() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      appointmentId,
      request,
    }: {
      appointmentId: string;
      request: RescheduleRequest;
    }) => rescheduleAppointment(appointmentId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: nurseCheckinQueryKeys.all });
      toast.success("Đổi lịch hẹn thành công!");
    },
    onError: (error: unknown) => {
      toast.error(getApiErrorMessage(error, "Đổi lịch thất bại"));
    },
  });
}
