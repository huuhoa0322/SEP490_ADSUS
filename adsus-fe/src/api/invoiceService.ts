import { apiClient as api } from '@/lib/api-client';
import type { ApiResponse, PagedResult } from '@/types/api.types';
export type { PagedResult };

export interface InvoiceFilter {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: string;
  sortBy?: string;
  sortDir?: string;
}

export interface InvoiceResponse {
  id: string;
  caseId: string;
  caseName: string;
  totalAmount: number;
  createdAt: string;
  paidAt?: string;
  status: string;
  paymentMethod?: string;
}

export interface InvoiceItemResponse {
  id: string;
  description: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
}

export interface InvoiceDetailResponse extends InvoiceResponse {
  items: InvoiceItemResponse[];
}

export const invoiceService = {
  getInvoices: async (filter: InvoiceFilter): Promise<PagedResult<InvoiceResponse>> => {
    const params = new URLSearchParams();
    if (filter.page) params.append('page', filter.page.toString());
    if (filter.pageSize) params.append('pageSize', filter.pageSize.toString());
    if (filter.search) params.append('search', filter.search);
    if (filter.status) params.append('status', filter.status);
    if (filter.sortBy) params.append('sortBy', filter.sortBy);
    if (filter.sortDir) params.append('sortDir', filter.sortDir);

    const response = await api.get<ApiResponse<PagedResult<InvoiceResponse>>>(`/api/v1/invoices?${params.toString()}`);
    return response.data.data as PagedResult<InvoiceResponse>;
  },

  getInvoiceDetail: async (id: string): Promise<InvoiceDetailResponse> => {
    const response = await api.get<ApiResponse<InvoiceDetailResponse>>(`/api/v1/invoices/${id}`);
    return response.data.data as InvoiceDetailResponse;
  },

  payAndDispense: async (id: string, paymentMethod: string): Promise<void> => {
    const response = await api.put<ApiResponse<void>>(`/api/v1/invoices/${id}/pay`, { paymentMethod });
    // no return value needed, throwing if not 2xx
  },
};
