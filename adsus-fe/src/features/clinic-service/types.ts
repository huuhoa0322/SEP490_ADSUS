/**
 * Data contracts for Clinic Services and Case Clinic Services.
 */

export interface ClinicService {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  price: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export type ClinicServiceResponse = ClinicService;

export interface CreateClinicServiceDto {
  code: string;
  name: string;
  description?: string | null;
  price: number;
}

export type CreateClinicServiceRequest = CreateClinicServiceDto;

export interface UpdateClinicServiceDto {
  name?: string;
  description?: string | null;
  price?: number;
  isActive?: boolean;
}

export type UpdateClinicServiceRequest = UpdateClinicServiceDto;

export interface CaseClinicService {
  id: string;
  caseId: string;
  clinicServiceId: string;
  serviceName: string;
  serviceCode: string;
  priceAtTime: number;
  createdAt: string;
}

export type CaseClinicServiceResponse = CaseClinicService;

export interface AddCaseClinicServiceDto {
  clinicServiceId: string;
}

export type AddCaseClinicServiceRequest = AddCaseClinicServiceDto;
