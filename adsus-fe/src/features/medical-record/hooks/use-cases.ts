"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  addUltrasoundImages,
  createCase,
  getCaseDetail,
  listCasesByPatient,
  listUltrasoundImages,
} from "../api/cases.api";
import type {
  AddUltrasoundImagesInput,
  CaseListQuery,
  CreateCaseInput,
} from "../types/medical-record.types";

import { medicalRecordQueryKeys } from "./query-keys";

/** SCR-12 — danh sách lần khám của một bệnh nhân (#24, có phân trang). */
export function useCaseList(query: CaseListQuery) {
  return useQuery({
    queryKey: medicalRecordQueryKeys.cases(query),
    queryFn: () => listCasesByPatient(query),
    enabled: Boolean(query.patientProfileId),
    placeholderData: (previous) => previous,
  });
}

/** SCR-30 — chi tiết một ca khám (#23). */
export function useCaseDetail(caseId: string | undefined) {
  return useQuery({
    queryKey: medicalRecordQueryKeys.case(caseId ?? ""),
    queryFn: () => getCaseDetail(caseId!),
    enabled: Boolean(caseId),
  });
}

/**
 * #22 — danh sách ảnh siêu âm.
 *
 * `#23` đã nhúng sẵn `ultrasoundImages`, nên màn chi tiết KHÔNG cần gọi hook này. Nó tồn tại
 * để làm mới riêng phần ảnh sau khi bổ sung, thay vì tải lại cả ca khám.
 */
export function useUltrasoundImages(caseId: string | undefined) {
  return useQuery({
    queryKey: medicalRecordQueryKeys.images(caseId ?? ""),
    queryFn: () => listUltrasoundImages(caseId!),
    enabled: Boolean(caseId),
  });
}

export function useCreateCase() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: CreateCaseInput) => createCase(input),
    onSuccess: () => {
      // Ca mới đổi cả danh sách lần khám lẫn "lần khám gần nhất" trên SCR-09.
      queryClient.invalidateQueries({ queryKey: medicalRecordQueryKeys.all });
    },
  });
}

/** #21 — bổ sung ảnh. Component phải chặn khi ca đã CONFIRMED (GB-01) trước khi gọi. */
export function useAddUltrasoundImages(caseId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: Omit<AddUltrasoundImagesInput, "caseId">) =>
      addUltrasoundImages({ ...input, caseId }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: medicalRecordQueryKeys.case(caseId) });
      queryClient.invalidateQueries({ queryKey: medicalRecordQueryKeys.images(caseId) });
    },
  });
}
