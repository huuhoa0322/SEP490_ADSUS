"use client";

import { useQuery } from "@tanstack/react-query";

import { searchPatients } from "../api/patients.api";
import type { PatientListQuery } from "../types/medical-record.types";

import { medicalRecordQueryKeys } from "./query-keys";

/** SCR-09 — danh sách toàn bộ bệnh nhân (UC-09). */
export function usePatientList(query: PatientListQuery) {
  return useQuery({
    queryKey: medicalRecordQueryKeys.patients(query),
    queryFn: () => searchPatients(query),
    // Giữ dữ liệu trang cũ trong lúc tải trang mới, để bảng không nháy trắng mỗi lần gõ.
    placeholderData: (previous) => previous,
  });
}
