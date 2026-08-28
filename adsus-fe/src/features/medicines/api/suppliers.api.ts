import { apiClient } from "@/lib/api-client";
import type { PagedResult } from "./medicines-api"; // Re-using PagedResult type

export interface SupplierResponse {
  supplierId: string;
  name: string;
  phoneNumber: string;
  email: string;
  address: string;
  taxCode: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateSupplierRequest {
  name: string;
  phoneNumber: string;
  email: string;
  address: string;
  taxCode: string;
}

export interface UpdateSupplierRequest {
  name: string;
  phoneNumber: string;
  email: string;
  address: string;
}

export async function getSuppliers(page: number = 1, pageSize: number = 10, search?: string) {
  const params = new URLSearchParams();
  params.append("page", page.toString());
  params.append("pageSize", pageSize.toString());
  if (search) {
    params.append("search", search);
  }
  
  const response = await apiClient.get<PagedResult<SupplierResponse>>(`/api/v1/suppliers?${params.toString()}`);
  return response.data;
}

export async function getSupplierById(id: string) {
  const response = await apiClient.get<SupplierResponse>(`/api/v1/suppliers/${id}`);
  return response.data;
}

export async function createSupplier(request: CreateSupplierRequest) {
  const response = await apiClient.post<SupplierResponse>("/api/v1/suppliers", request);
  return response.data;
}

export async function updateSupplier(id: string, request: UpdateSupplierRequest) {
  const response = await apiClient.put<SupplierResponse>(`/api/v1/suppliers/${id}`, request);
  return response.data;
}

export async function updateSupplierStatus(id: string, isActive: boolean) {
  await apiClient.patch(`/api/v1/suppliers/${id}/status?isActive=${isActive}`);
}
