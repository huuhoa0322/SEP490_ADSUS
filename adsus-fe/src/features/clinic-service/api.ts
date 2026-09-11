import { apiClient as api } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";
import type {
  ClinicService,
  CreateClinicServiceDto,
  UpdateClinicServiceDto,
  CaseClinicService,
  AddCaseClinicServiceDto,
} from "./types";

export async function getClinicServices(isActive?: boolean): Promise<ClinicService[]> {
  const params = isActive !== undefined ? `?isActive=${isActive}` : "";
  const res = await api.get<ApiResponse<ClinicService[]>>(`/api/v1/clinic-services${params}`);
  return res.data.data ?? [];
}

export async function getClinicServiceById(id: string): Promise<ClinicService> {
  const res = await api.get<ApiResponse<ClinicService>>(`/api/v1/clinic-services/${id}`);
  return res.data.data!;
}

export async function createClinicService(dto: CreateClinicServiceDto): Promise<ClinicService> {
  const res = await api.post<ApiResponse<ClinicService>>("/api/v1/clinic-services", dto);
  return res.data.data!;
}

export async function updateClinicService(
  id: string,
  dto: UpdateClinicServiceDto,
): Promise<ClinicService> {
  const res = await api.put<ApiResponse<ClinicService>>(`/api/v1/clinic-services/${id}`, dto);
  return res.data.data!;
}

export async function deactivateClinicService(id: string): Promise<void> {
  await api.put(`/api/v1/clinic-services/${id}/deactivate`);
}

export async function getCaseClinicServices(caseId: string): Promise<CaseClinicService[]> {
  const res = await api.get<ApiResponse<CaseClinicService[]>>(`/api/v1/cases/${caseId}/services`);
  return res.data.data ?? [];
}

export async function addCaseClinicService(
  caseId: string,
  dto: AddCaseClinicServiceDto,
): Promise<void> {
  await api.post(`/api/v1/cases/${caseId}/services`, dto);
}

export async function removeCaseClinicService(caseId: string, id: string): Promise<void> {
  await api.delete(`/api/v1/cases/${caseId}/services/${id}`);
}

export const clinicServiceApi = {
  getAll: getClinicServices,
  getById: getClinicServiceById,
  create: createClinicService,
  update: updateClinicService,
  deactivate: deactivateClinicService,
  getForCase: getCaseClinicServices,
  addToCase: (caseId: string, req: AddCaseClinicServiceDto) => addCaseClinicService(caseId, req),
  removeFromCase: removeCaseClinicService,
};

export const caseClinicServiceApi = {
  getForCase: getCaseClinicServices,
  addToCase: (caseId: string, clinicServiceId: string) =>
    addCaseClinicService(caseId, { clinicServiceId }),
  removeFromCase: removeCaseClinicService,
};
