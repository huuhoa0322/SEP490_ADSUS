"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  createPrescription,
  getIntakeLogs,
  getPrescriptionDetail,
  getPrescriptionsByPatient,
  searchMedicines,
} from "../api/prescriptions.api";

import type {
  CreatePrescriptionRequest,
  PrescriptionListQuery,
} from "../types/prescription.types";

const queryKeys = {
  all: ["prescriptions"] as const,
  lists: () => [...queryKeys.all, "list"] as const,
  list: (q: PrescriptionListQuery) => [...queryKeys.lists(), q] as const,
  details: () => [...queryKeys.all, "detail"] as const,
  detail: (id: string) => [...queryKeys.details(), id] as const,
  intakes: (id: string) => [...queryKeys.all, "intakes", id] as const,
  medicines: (kw: string) => ["medicines", "search", kw] as const,
};

export function usePrescriptionList(query: PrescriptionListQuery) {
  return useQuery({
    queryKey: queryKeys.list(query),
    queryFn: () => getPrescriptionsByPatient(query),
  });
}

export function usePrescriptionDetail(prescriptionId?: string) {
  return useQuery({
    queryKey: queryKeys.detail(prescriptionId!),
    queryFn: () => getPrescriptionDetail(prescriptionId!),
    enabled: !!prescriptionId,
  });
}

export function useIntakeLogs(prescriptionId?: string) {
  return useQuery({
    queryKey: queryKeys.intakes(prescriptionId!),
    queryFn: () => getIntakeLogs(prescriptionId!),
    enabled: !!prescriptionId,
  });
}

export function useMedicineSearch(keyword: string) {
  return useQuery({
    queryKey: queryKeys.medicines(keyword),
    queryFn: () => searchMedicines(keyword),
    enabled: keyword.length >= 2,
  });
}

export function useCreatePrescription() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: CreatePrescriptionRequest) => createPrescription(payload),
    onSuccess: (created) => {
      qc.invalidateQueries({ queryKey: queryKeys.lists() });
      qc.setQueryData(queryKeys.detail(created.prescriptionId), created);
    },
  });
}