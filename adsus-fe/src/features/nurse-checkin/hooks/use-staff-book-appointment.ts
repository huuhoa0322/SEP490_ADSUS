"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import toast from "react-hot-toast";
import { getApiErrorMessage } from "@/lib/api-client";
import { staffBookAppointment } from "../api/checkin.api";
import { nurseCheckinQueryKeys } from "../query-keys";

export function useStaffBookAppointment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: staffBookAppointment,
    onSuccess: () => {
      // Invalidate cả nurse-checkin (queue) lẫn medical-record (patient list)
      queryClient.invalidateQueries({ queryKey: nurseCheckinQueryKeys.all });
      queryClient.invalidateQueries({ queryKey: ["medical-record"] });
      toast.success("Đặt lịch khám thành công!");
    },
    onError: (error: unknown) => {
      toast.error(getApiErrorMessage(error, "Đặt lịch thất bại"));
    },
  });
}
