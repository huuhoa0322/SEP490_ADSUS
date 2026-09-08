"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { getApiErrorMessage } from "@/lib/api-client";
import { invoiceService } from "@/api/invoiceService";
import type { InvoiceFilter, InvoiceDetailResponse, PagedResult, InvoiceResponse } from "@/api/invoiceService";

const keys = {
  list: (filter: InvoiceFilter) => ["invoices", "list", filter] as const,
  detail: (id: string) => ["invoices", "detail", id] as const,
};

/** GET /api/v1/invoices — danh sách hóa đơn có lọc. */
export function useInvoicesList(filter: InvoiceFilter) {
  return useQuery<PagedResult<InvoiceResponse>, Error>({
    queryKey: keys.list(filter),
    queryFn: () => invoiceService.getInvoices(filter),
    placeholderData: (previous) => previous,
  });
}

/** GET /api/v1/invoices/{id} — chi tiết hóa đơn. */
export function useInvoiceDetail(id: string | null) {
  const qc = useQueryClient();
  return useQuery<InvoiceDetailResponse, Error>({
    queryKey: keys.detail(id ?? ""),
    queryFn: () => invoiceService.getInvoiceDetail(id!),
    enabled: Boolean(id),
  });
}

export { keys as invoiceQueryKeys };

/** PUT /api/v1/invoices/{id}/pay — thanh toán + xuất kho FEFO. */
export function usePayInvoice() {
  const qc = useQueryClient();
  return useMutation<void, Error, { id: string; paymentMethod: string }, unknown>({
    mutationFn: ({ id, paymentMethod }) => invoiceService.payAndDispense(id, paymentMethod),
    onSuccess: (_data, variables) => {
      qc.invalidateQueries({ queryKey: keys.detail(variables.id) });
      qc.invalidateQueries({ queryKey: ["invoices"] });
    },
  });
}

/** PUT /api/v1/invoices/{id}/cancel — hủy hóa đơn. */
export function useCancelInvoice() {
  const qc = useQueryClient();
  return useMutation<void, Error, { id: string; reason: string }, unknown>({
    mutationFn: ({ id, reason }) => invoiceService.cancelInvoice(id, reason),
    onSuccess: (_data, variables) => {
      qc.invalidateQueries({ queryKey: keys.detail(variables.id) });
      qc.invalidateQueries({ queryKey: ["invoices"] });
    },
  });
}

/** Helper — viết tiếng Việt cho lỗi từ mutation. Dùng chung với `react-hot-toast`. */
export function translateInvoiceError(error: unknown, fallback: string): string {
  return getApiErrorMessage(error, fallback);
}

/** GET /api/v1/cases/{caseId}/invoices — danh sách hóa đơn của 1 ca khám. */
export function useCaseInvoices(caseId: string | undefined) {
  return useQuery<InvoiceResponse[], Error>({
    queryKey: ["invoices", "case", caseId ?? ""],
    queryFn: () => invoiceService.getCaseInvoices(caseId!),
    enabled: Boolean(caseId),
    staleTime: 30 * 1000,
  });
}
