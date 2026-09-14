"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";

import { toast } from "react-hot-toast";
import { getApiErrorMessage } from "@/lib/api-client";
import {
  listDoctorAppointments,
  createFollowUpAppointment,
  doctorReadyNext,
  type FollowUpAppointmentRequest,
} from "../api/doctor-appointment.api";
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

export function useDoctorReadyNext() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => doctorReadyNext(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["appointment-scheduling"] });
      toast.success("Đã gửi thông báo cho lễ tân để mời bệnh nhân vào.");
    },
    onError: (error: unknown) => {
      toast.error(getApiErrorMessage(error, "Không thể gửi thông báo tiếp nhận bệnh nhân."));
    },
  });
}
