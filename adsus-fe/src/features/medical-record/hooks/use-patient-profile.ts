"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  createPatientProfile,
  getPatientProfile,
  updatePatientProfile,
} from "../api/patient-profiles.api";
import type {
  CreatePatientProfileRequest,
  UpdatePatientProfileRequest,
} from "../types/medical-record.types";

import { medicalRecordQueryKeys } from "./query-keys";

/** SCR-10 và SCR-12 — nạp hồ sơ nền. */
export function usePatientProfile(profileId: string | undefined) {
  return useQuery({
    queryKey: medicalRecordQueryKeys.profile(profileId ?? ""),
    queryFn: () => getPatientProfile(profileId!),
    enabled: Boolean(profileId),
  });
}

export function useCreatePatientProfile() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: CreatePatientProfileRequest) => createPatientProfile(payload),
    onSuccess: () => {
      // Bệnh nhân chuyển từ "chưa có hồ sơ nền" sang "đã có" — dòng của họ trên SCR-09 đổi
      // hẳn nút hành động, nên phải làm mới danh sách.
      queryClient.invalidateQueries({ queryKey: medicalRecordQueryKeys.all });
    },
  });
}

export function useUpdatePatientProfile(profileId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: UpdatePatientProfileRequest) => updatePatientProfile(profileId, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: medicalRecordQueryKeys.profile(profileId) });
    },
  });
}
