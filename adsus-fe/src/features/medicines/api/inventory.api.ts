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

export const useValidateImport = () => {
  return useMutation({
    mutationFn: async (data: ImportInventoryRequest) => {
      const response = await apiClient.post<{ isValid: boolean; errorMessage?: string }>('/api/v1/inventory/validate-import', data);
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

export interface AdjustInventoryRequest {
  batchId: string;
  newQuantityBase: number;
  reason: string;
}

export interface AdjustInventoryResponse {
  transactionId: string;
  previousQuantity: number;
  newQuantity: number;
  delta: number;
}

export const useAdjustInventory = () => {
  return useMutation({
    mutationFn: async (data: AdjustInventoryRequest) => {
      const response = await apiClient.put<AdjustInventoryResponse>('/api/v1/inventory/adjust', data);
      return response.data;
    },
  });
};

export interface InventoryHistoryFilter {
  search?: string;
  type?: string;
  batchId?: string;
  sortBy?: string;   // txnDate | quantityBase
  sortDir?: string;  // asc | desc
  page?: number;
  pageSize?: number;
}

export interface InventoryHistoryResponse {
  transactionId: string;
  batchId: string;
  lotNumber: string;
  medicineName: string;
  supplierName?: string;
  unitName: string;        // Đơn vị đóng gói
  baseUnitName?: string;   // Đơn vị cơ bản (usageUnit của thuốc)
  txnType: 'Import' | 'Dispense' | 'Adjustment';
  quantityBase: number;
  quantityInUnit: number;
  txnDate: string;
  unitImportPrice?: number;
  prescriptionItemId?: string;
  reason?: string;
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

export interface MedicineBatchResponse {
  batchId: string;
  medicineId: string;
  lotNumber: string;
  expiryDate: string;
  quantityBase: number;
  baseUnitAvgImportPrice: number;
  usageUnit?: string;   // Đơn vị cơ bản
}

export interface MedicineBatchFilter {
  medicineId: string;
  search?: string;   // Tìm theo mã lô
  sortBy?: string;   // expiryDate | quantityBase | avgPrice
  sortDir?: string;  // asc | desc
  page?: number;
  pageSize?: number;
}

export const useMedicineBatches = (medicineId: string) => {
  return useQuery({
    queryKey: ['medicine-batches', medicineId],
    queryFn: async () => {
      const response = await apiClient.get<MedicineBatchResponse[]>('/api/v1/inventory/batches', {
        params: { medicineId },
      });
      return response.data;
    },
    enabled: !!medicineId,
  });
};

export const usePagedMedicineBatches = (filter: MedicineBatchFilter) => {
  return useQuery({
    queryKey: ['medicine-batches-paged', filter],
    queryFn: async () => {
      const response = await apiClient.get<PagedResult<MedicineBatchResponse>>(
        '/api/v1/inventory/batches',
        { params: filter }
      );
      return response.data;
    },
    enabled: !!filter.medicineId,
  });
};

export interface LowStockAlertResponse {
  medicineId: string;
  medicineName: string;
  currentStock: number;
  threshold: number;
  baseUnitName: string;
  severity: 'WARNING' | 'CRITICAL';
}

export interface ExpiryAlertResponse {
  batchId: string;
  medicineId: string;
  medicineName: string;
  lotNumber: string;
  expiryDate: string;
  daysUntilExpiry: number;
  quantityBase: number;
  baseUnitName: string;
  severity: 'WARNING' | 'CRITICAL' | 'EXPIRED';
}

export interface InventoryAlertSummary {
  lowStockCount: number;
  expiringSoonCount: number;
  expiredCount: number;
  lowStockAlerts: LowStockAlertResponse[];
  expiryAlerts: ExpiryAlertResponse[];
}

export const useInventoryAlerts = () => {
  return useQuery({
    queryKey: ['inventory-alerts'],
    queryFn: async () => {
      const response = await apiClient.get<InventoryAlertSummary>('/api/v1/inventory/alerts');
      return response.data;
    },
  });
};
