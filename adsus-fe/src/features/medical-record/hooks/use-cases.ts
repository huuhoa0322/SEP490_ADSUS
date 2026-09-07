"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  confirmCase,
  createCase,
  endCaseWithoutPrescription,
  getCaseDetail,
  listCasesByPatient,
  listUltrasoundImages,
  saveCaseConclusion,
} from "../api/cases.api";
import type {
  CaseConclusionInput,
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

/**
 * Thêm 07/08/2026 — "Lưu kết luận". KHÔNG đổi trạng thái ca, chỉ làm mới chi tiết ca này (badge
 * trạng thái ở SCR-12 không đổi nên không cần invalidate danh sách).
 */
export function useSaveCaseConclusion(caseId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: CaseConclusionInput) => saveCaseConclusion(caseId, input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: medicalRecordQueryKeys.case(caseId) });
    },
  });
}

/**
 * Thêm 07/08/2026 — "Kết thúc ca khám". Bác sĩ phụ trách khoá ca, ca chuyển CONFIRMED. Làm
 * mới cả danh sách lần khám (SCR-12 badge trạng thái đổi) lẫn chi tiết ca này.
 */
export function useConfirmCase(caseId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: CaseConclusionInput) => confirmCase(caseId, input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: medicalRecordQueryKeys.case(caseId) });
      queryClient.invalidateQueries({ queryKey: medicalRecordQueryKeys.all });
    },
  });
}

/**
 * Kết thúc ca bệnh trực tiếp (chuyển CONFIRMED sang END) cho bệnh nhân không lấy thuốc.
 */
export function useEndCaseWithoutPrescription(caseId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => endCaseWithoutPrescription(caseId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: medicalRecordQueryKeys.case(caseId) });
      queryClient.invalidateQueries({ queryKey: medicalRecordQueryKeys.all });
    },
  });
}
