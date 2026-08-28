import { apiClient } from "@/lib/api-client";

export interface MedicineResponse {
  medicineId: string;
  name: string;
  usageUnit?: string;       // Đơn vị kê đơn
  baseUnitName?: string;   // Tên đơn vị cơ bản kho (IsBaseUnit=true)
  volumePerBaseUnit?: number;
  status: string;
  createdAt: string;
  totalInventoryBase: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface CreateMedicineRequest {
  name: string;
  medicineUnitId: string;
  salePrice: number;
  usageUnit?: string;
  volumePerBaseUnit?: number;
}

export interface UpdateMedicineRequest {
  name: string;
  usageUnit?: string;
  volumePerBaseUnit?: number;
}

export async function getPagedMedicines(page: number, pageSize: number, search?: string, inStock?: boolean) {
  const params = new URLSearchParams();
  params.append("page", page.toString());
  params.append("pageSize", pageSize.toString());
  if (search) {
    params.append("search", search);
  }
  if (inStock !== undefined) {
    params.append("inStock", inStock.toString());
  }
  
  const response = await apiClient.get<PagedResult<MedicineResponse>>(`/api/v1/medicines/admin?${params.toString()}`);
  return response.data;
}

export async function getMedicineById(id: string) {
  const response = await apiClient.get<MedicineResponse>(`/api/v1/medicines/${id}`);
  return response.data;
}

export async function createMedicine(request: CreateMedicineRequest) {
  const response = await apiClient.post<MedicineResponse>("/api/v1/medicines", request);
  return response.data;
}

export async function updateMedicine(id: string, request: UpdateMedicineRequest) {
  const response = await apiClient.put<MedicineResponse>(`/api/v1/medicines/${id}`, request);
  return response.data;
}


export async function deleteMedicine(id: string) {
  await apiClient.delete(`/api/v1/medicines/${id}`);
}

export async function activateMedicine(id: string) {
  await apiClient.patch(`/api/v1/medicines/${id}/activate`);
}


export interface MedicinePackagingResponse {
  id: string;
  medicineId: string;
  medicineUnitId: string;
  unitName: string;
  conversionFactor: number;
  isBaseUnit: boolean;
  salePrice: number;
  isSellable: boolean;
}

export interface CreateMedicinePackagingRequest {
  medicineUnitId: string;
  conversionFactor: number;
  isBaseUnit: boolean;
  salePrice: number;
  isSellable: boolean;
}

export interface UpdateMedicinePackagingRequest {
  medicineUnitId: string;
  conversionFactor: number;
  isBaseUnit: boolean;
  salePrice: number;
  isSellable: boolean;
}

export async function getPackagingsByMedicineId(medicineId: string) {
  const response = await apiClient.get<MedicinePackagingResponse[]>(`/api/v1/medicines/${medicineId}/packagings`);
  return response.data;
}

export async function addPackaging(medicineId: string, request: CreateMedicinePackagingRequest) {
  const response = await apiClient.post<MedicinePackagingResponse>(`/api/v1/medicines/${medicineId}/packagings`, request);
  return response.data;
}

export async function updatePackaging(id: string, request: UpdateMedicinePackagingRequest) {
  const response = await apiClient.put<MedicinePackagingResponse>(`/api/v1/medicines/packagings/${id}`, request);
  return response.data;
}

export async function deletePackaging(id: string) {
  await apiClient.delete(`/api/v1/medicines/packagings/${id}`);
}

export interface MedicineUnitResponse {
  medicineUnitId: string;
  name: string;
}

export async function getMedicineUnits() {
  const response = await apiClient.get<MedicineUnitResponse[]>('/api/v1/medicines/units');
  return response.data;
}
