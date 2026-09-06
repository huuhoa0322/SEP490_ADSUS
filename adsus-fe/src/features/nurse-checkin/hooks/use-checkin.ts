"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { checkinAppointment } from "../api/checkin.api";
import { nurseCheckinQueryKeys } from "../query-keys";
import toast from "react-hot-toast";
import type { ApiResponse } from "@/types/api.types";

interface CheckinResponse {
  message?: string;
}

export function useCheckin() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ appointmentId, caseId }: { appointmentId: string; caseId: string }) =>
      checkinAppointment(appointmentId, caseId) as Promise<ApiResponse<CheckinResponse>>,
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: nurseCheckinQueryKeys.all });
      if (data?.data?.message?.includes("tự động hủy")) {
        toast.error(data.data.message, { duration: 5000 });
      } else {
        toast.success(data?.data?.message || "Check-in thành công!");
      }
    },
    onError: (error: { message?: string; response?: { data?: { message?: string } } }) => {
      const message = error?.message || error?.response?.data?.message || "Check-in thất bại";
      if (message?.includes("tự động hủy")) {
        toast.error(message, { duration: 5000 });
      } else {
        toast.error(message);
      }
    },
  });
}
