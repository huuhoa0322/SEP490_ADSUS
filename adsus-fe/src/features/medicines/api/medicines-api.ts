import { apiClient } from "@/lib/api-client";

export interface MedicineResponse {
  medicineId: string;
  name: string;
  status: string;
  createdAt: string;
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
}

export interface UpdateMedicineRequest {
  name: string;
}

export async function getPagedMedicines(page: number, pageSize: number, search?: string) {
  const params = new URLSearchParams();
  params.append("page", page.toString());
  params.append("pageSize", pageSize.toString());
  if (search) {
    params.append("search", search);
  }
  
  const response = await apiClient.get<PagedResult<MedicineResponse>>(`/api/v1/medicines/admin?${params.toString()}`);
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

