"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import toast from "react-hot-toast";

import { getApiErrorMessage } from "@/lib/api-client";
import {
  getClinicServices,
  getClinicServiceById,
  createClinicService,
  updateClinicService,
  deactivateClinicService,
  getCaseClinicServices,
  addCaseClinicService,
  removeCaseClinicService,
} from "./api";
import type {
  ClinicService,
  CreateClinicServiceDto,
  UpdateClinicServiceDto,
  CaseClinicService,
  AddCaseClinicServiceDto,
} from "./types";

export const clinicServiceQueryKeys = {
  all: ["clinic-services"] as const,
  lists: () => [...clinicServiceQueryKeys.all, "list"] as const,
  list: (isActive?: boolean) => [...clinicServiceQueryKeys.lists(), { isActive }] as const,
  details: () => [...clinicServiceQueryKeys.all, "detail"] as const,
  detail: (id: string) => [...clinicServiceQueryKeys.details(), id] as const,
  caseServices: (caseId: string) => ["case-clinic-services", caseId] as const,
};

// Admin catalog queries & mutations
export function useClinicServicesList(isActive?: boolean) {
  return useQuery<ClinicService[], Error>({
    queryKey: clinicServiceQueryKeys.list(isActive),
    queryFn: () => getClinicServices(isActive),
  });
}

export function useClinicServiceById(id: string | null) {
  return useQuery<ClinicService, Error>({
    queryKey: clinicServiceQueryKeys.detail(id ?? ""),
    queryFn: () => getClinicServiceById(id!),
    enabled: Boolean(id),
  });
}

export const useClinicServiceDetail = useClinicServiceById;

export function useCreateClinicService() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: CreateClinicServiceDto) => createClinicService(dto),
    onSuccess: () => {
      toast.success("Thêm dịch vụ phòng khám thành công.");
      qc.invalidateQueries({ queryKey: clinicServiceQueryKeys.all });
    },
    onError: (error) => {
      toast.error(getApiErrorMessage(error, "Không thể thêm dịch vụ. Vui lòng thử lại."));
    },
  });
}

export function useUpdateClinicService() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({
      id,
      dto,
      req,
    }: {
      id: string;
      dto?: UpdateClinicServiceDto;
      req?: UpdateClinicServiceDto;
    }) => updateClinicService(id, (dto ?? req)!),
    onSuccess: () => {
      toast.success("Cập nhật thông tin dịch vụ thành công.");
      qc.invalidateQueries({ queryKey: clinicServiceQueryKeys.all });
    },
    onError: (error) => {
      toast.error(getApiErrorMessage(error, "Không thể cập nhật dịch vụ. Vui lòng thử lại."));
    },
  });
}

export function useDeactivateClinicService() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => deactivateClinicService(id),
    onSuccess: () => {
      toast.success("Đã vô hiệu hóa dịch vụ thành công.");
      qc.invalidateQueries({ queryKey: clinicServiceQueryKeys.all });
    },
    onError: (error) => {
      toast.error(getApiErrorMessage(error, "Không thể vô hiệu hóa dịch vụ."));
    },
  });
}

// Case-level service queries & mutations
export function useCaseClinicServices(caseId: string | undefined) {
  return useQuery<CaseClinicService[], Error>({
    queryKey: clinicServiceQueryKeys.caseServices(caseId ?? ""),
    queryFn: () => getCaseClinicServices(caseId!),
    enabled: Boolean(caseId),
  });
}

type AddCaseServiceInput =
  | string
  | AddCaseClinicServiceDto
  | { caseId: string; dto?: AddCaseClinicServiceDto; clinicServiceId?: string };

export function useAddCaseClinicService(boundCaseId?: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (input: AddCaseServiceInput) => {
      if (typeof input === "string") {
        if (!boundCaseId) throw new Error("Mã ca khám không hợp lệ.");
        return addCaseClinicService(boundCaseId, { clinicServiceId: input });
      }
      if ("caseId" in input) {
        const cId = input.caseId;
        const dto: AddCaseClinicServiceDto = input.dto ?? {
          clinicServiceId: input.clinicServiceId!,
        };
        return addCaseClinicService(cId, dto);
      }
      if (!boundCaseId) throw new Error("Mã ca khám không hợp lệ.");
      return addCaseClinicService(boundCaseId, input);
    },
    onSuccess: (_, input) => {
      const cId =
        typeof input === "object" && "caseId" in input ? input.caseId : boundCaseId;
      toast.success("Đã thêm dịch vụ vào ca khám.");
      if (cId) {
        qc.invalidateQueries({ queryKey: clinicServiceQueryKeys.caseServices(cId) });
        qc.invalidateQueries({ queryKey: ["case-clinic-services", cId] });
      }
      qc.invalidateQueries({ queryKey: ["invoices"] });
    },
    onError: (error) => {
      toast.error(getApiErrorMessage(error, "Không thể thêm dịch vụ vào ca khám."));
    },
  });
}

type RemoveCaseServiceInput =
  | string
  | { caseId: string; id: string };

export function useRemoveCaseClinicService(boundCaseId?: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (input: RemoveCaseServiceInput) => {
      if (typeof input === "string") {
        if (!boundCaseId) throw new Error("Mã ca khám không hợp lệ.");
        return removeCaseClinicService(boundCaseId, input);
      }
      return removeCaseClinicService(input.caseId, input.id);
    },
    onSuccess: (_, input) => {
      const cId =
        typeof input === "object" && "caseId" in input ? input.caseId : boundCaseId;
      toast.success("Đã xóa dịch vụ khỏi ca khám.");
      if (cId) {
        qc.invalidateQueries({ queryKey: clinicServiceQueryKeys.caseServices(cId) });
        qc.invalidateQueries({ queryKey: ["case-clinic-services", cId] });
      }
      qc.invalidateQueries({ queryKey: ["invoices"] });
    },
    onError: (error) => {
      toast.error(getApiErrorMessage(error, "Không thể xóa dịch vụ khỏi ca khám."));
    },
  });
}
