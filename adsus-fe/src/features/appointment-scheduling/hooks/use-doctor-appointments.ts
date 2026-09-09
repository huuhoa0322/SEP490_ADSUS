"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";

import { listDoctorAppointments, createFollowUpAppointment, type FollowUpAppointmentRequest } from "../api/doctor-appointment.api";
import type { DoctorAppointmentQuery } from "../types/doctor-appointment.types";

/** Lịch bệnh nhân của Doctor đang đăng nhập, theo khoảng ngày (thường là 1 tuần). */
export function useDoctorAppointments(query: DoctorAppointmentQuery) {
  return useQuery({
    queryKey: ["appointment-scheduling", "doctor-appointments", query] as const,
    queryFn: () => listDoctorAppointments(query),
    // Giữ dữ liệu tuần cũ trong lúc chuyển tuần, để bảng không nháy trắng.
    placeholderData: (previous) => previous,
  });
}

export function useCreateFollowUpAppointment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: FollowUpAppointmentRequest) => createFollowUpAppointment(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["appointment-scheduling"] });
    },
  });
}
