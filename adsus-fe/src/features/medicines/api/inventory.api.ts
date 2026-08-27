import { useMutation, useQuery } from '@tanstack/react-query';
import { apiClient } from '@/lib/api-client';
import type { PagedResult } from '@/types/api.types';

export interface ImportInventoryRequest {
  medicineId: string;
  supplierId: string;
  medicinePackagingId: string;
  lotNumber: string;
  expiryDate: string; // ISO String format
  quantity: number;
  importPricePerUnit: number;
}

export const useImportInventory = () => {
  return useMutation({
    mutationFn: async (data: ImportInventoryRequest) => {
      const response = await apiClient.post('/api/v1/inventory/import', data);
      return response.data;
    },
  });
};

export const useBulkImportInventory = () => {
  return useMutation({
    mutationFn: async (data: ImportInventoryRequest[]) => {
      const response = await apiClient.post('/api/v1/inventory/import/bulk', data);
      return response.data;
    },
  });
};

export interface InventoryHistoryFilter {
  search?: string;
  type?: string;
  page?: number;
  pageSize?: number;
}

export interface InventoryHistoryResponse {
  transactionId: string;
  batchId: string;
  lotNumber: string;
  medicineName: string;
  supplierName?: string;
  unitName: string;
  txnType: 'Import' | 'Dispense' | 'Adjustment';
  quantityBase: number;
  quantityInUnit: number;
  txnDate: string;
  unitImportPrice?: number;
  prescriptionItemId?: string;
}

export const useInventoryHistory = (filter: InventoryHistoryFilter) => {
  return useQuery({
    queryKey: ['inventory-history', filter],
    queryFn: async () => {
      const response = await apiClient.get<PagedResult<InventoryHistoryResponse>>('/api/v1/inventory/history', {
        params: filter,
      });
      return response.data;
    },
  });
};
