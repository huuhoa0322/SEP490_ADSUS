"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { getDiagnosisItems, updateCaseDiagnoses } from "../api/diagnosis.api";
import type { CaseDiagnosisInput } from "../types/medical-record.types";
import { medicalRecordQueryKeys } from "./query-keys";

export function useDiagnosisItems() {
  return useQuery({
    queryKey: ["diagnosis-items"],
    queryFn: getDiagnosisItems,
  });
}

export function useUpdateCaseDiagnoses(caseId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (diagnoses: CaseDiagnosisInput[]) => updateCaseDiagnoses(caseId, diagnoses),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: medicalRecordQueryKeys.case(caseId) });
    },
  });
}
