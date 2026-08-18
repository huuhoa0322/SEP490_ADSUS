"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { getApiErrorMessage } from "@/lib/api-client";

import {
  confirmIntake,
  createPrescription,
  getCasePrescriptionWithCompliance,
  getMedicationCatalog,
  getMyIntakeLogs,
  getPrescription,
} from "../api/prescriptions.api";
import type {
  CreatePrescriptionRequest,
  IntakeLogResponse,
  MedicationCatalogItem,
  PrescriptionResponse,
  PrescriptionWithComplianceResponse,
} from "../types/prescriptions.types";

/**
 * Module 7 — TanStack Query hooks.
 *
 * Quy ước queryKey:
 *   - ['prescriptions', 'detail', id]   — đơn lẻ
 *   - ['prescriptions', 'my-intakes']   — patient lịch uống
 *   - ['medication-catalog']            — danh mục thuốc
 *
 * Mutation invalidate đúng key để UI tự refetch khi cần (vd: confirmIntake
 * xong → refetch my-intakes).
 */

const keys = {
  detail: (id: string) => ["prescriptions", "detail", id] as const,
  myIntakes: () => ["prescriptions", "my-intakes"] as const,
  catalog: () => ["medication-catalog"] as const,
  caseCompliance: (caseId: string) => ["prescriptions", "case-compliance", caseId] as const,
};

/** POST /api/v1/prescriptions — Doctor kê đơn (UC-18). */
export function useCreatePrescription() {
  return useMutation<PrescriptionResponse, Error, CreatePrescriptionRequest>({
    mutationFn: createPrescription,
  });
}

/** GET /api/v1/prescriptions/{id} — Doctor/Nurse xem chi tiết (UC-17). */
export function usePrescription(id: string | null) {
  return useQuery<PrescriptionResponse, Error>({
    queryKey: keys.detail(id ?? ""),
    queryFn: () => getPrescription(id!),
    enabled: Boolean(id),
  });
}

/** GET /api/v1/me/medication-intakes — Patient xem lịch uống (SCR-19). */
export function useMyIntakeLogs() {
  return useQuery<IntakeLogResponse[], Error>({
    queryKey: keys.myIntakes(),
    queryFn: getMyIntakeLogs,
  });
}

/** POST /api/v1/me/medication-intakes/{id}/confirm — Patient xác nhận đã uống. */
export function useConfirmIntake() {
  const qc = useQueryClient();
  return useMutation<void, Error, string, { previous: IntakeLogResponse[] | undefined }>({
    mutationFn: confirmIntake,
    // Optimistic update: client biết intake -> taken trước khi server trả 204.
    // Idempotent §22.2 fix #7, nếu server reject (400/409) sẽ rollback.
    onMutate: async (intakeId) => {
      await qc.cancelQueries({ queryKey: keys.myIntakes() });
      const previous = qc.getQueryData<IntakeLogResponse[]>(keys.myIntakes());
      if (previous) {
        qc.setQueryData<IntakeLogResponse[]>(
          keys.myIntakes(),
          previous.map((log) =>
            log.intakeId === intakeId
              ? { ...log, status: "TAKEN", confirmedAt: new Date().toISOString() }
              : log,
          ),
        );
      }
      return { previous };
    },
    onError: (_err, _intakeId, context) => {
      if (context?.previous) {
        qc.setQueryData(keys.myIntakes(), context.previous);
      }
    },
    onSettled: () => {
      qc.invalidateQueries({ queryKey: keys.myIntakes() });
    },
  });
}

/** GET /api/v1/cases/{caseId}/prescriptions/with-compliance — đơn + compliance (Task 4). */
export function useCasePrescriptionWithCompliance(caseId: string) {
  return useQuery<PrescriptionWithComplianceResponse[], Error>({
    queryKey: keys.caseCompliance(caseId),
    queryFn: () => getCasePrescriptionWithCompliance(caseId),
    staleTime: 30 * 1000,
  });
}

/** GET /api/v1/medication-catalog — Doctor chọn thuốc khi kê đơn. */
export function useMedicationCatalog() {
  return useQuery<MedicationCatalogItem[], Error>({
    queryKey: keys.catalog(),
    queryFn: getMedicationCatalog,
    // Catalog ít thay đổi — 5 phút mới refetch, tránh gọi lại mỗi lần mở form.
    staleTime: 5 * 60 * 1000,
  });
}

/** Helper — viết tiếng Việt cho lỗi từ mutation. Dùng chung với `react-hot-toast`. */
export function translatePrescriptionError(error: unknown, fallback: string): string {
  return getApiErrorMessage(error, fallback);
}