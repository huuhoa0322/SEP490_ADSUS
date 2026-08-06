"use client";

import { useQuery } from "@tanstack/react-query";

import { listDoctors } from "../api/doctors.api";

import { medicalRecordQueryKeys } from "./query-keys";

/**
 * SCR-11 — đổ ô "Bác sĩ phụ trách" (GB-04).
 *
 * Danh sách nhân sự phòng khám gần như không đổi trong một ca làm việc, nên giữ 5 phút để
 * không gọi lại mỗi lần mở form tạo ca.
 */
export function useDoctorList() {
  return useQuery({
    queryKey: medicalRecordQueryKeys.doctors(),
    queryFn: () => listDoctors(),
    staleTime: 5 * 60 * 1000,
  });
}
