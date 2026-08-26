import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import toast from "react-hot-toast";
import {
  createSupplier,
  getSupplierById,
  getSuppliers,
  updateSupplier,
  updateSupplierStatus,
  type CreateSupplierRequest,
  type UpdateSupplierRequest,
} from "../api/suppliers.api";
import { getApiErrorMessage } from "@/lib/api-client";

export const SUPPLIERS_QUERY_KEY = ["suppliers"];

export function useSuppliers(page: number = 1, pageSize: number = 10, search?: string) {
  return useQuery({
    queryKey: [...SUPPLIERS_QUERY_KEY, page, pageSize, search],
    queryFn: () => getSuppliers(page, pageSize, search),
  });
}

export function useSupplier(id: string | null) {
  return useQuery({
    queryKey: [...SUPPLIERS_QUERY_KEY, id],
    queryFn: () => getSupplierById(id!),
    enabled: !!id,
  });
}

export function useCreateSupplier() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CreateSupplierRequest) => createSupplier(request),
    onSuccess: () => {
      toast.success("Thêm nhà cung cấp thành công!");
      queryClient.invalidateQueries({ queryKey: SUPPLIERS_QUERY_KEY });
    },
    onError: (error: unknown) => {
      toast.error(getApiErrorMessage(error, "Đã có lỗi xảy ra khi thêm nhà cung cấp."));
    },
  });
}

export function useUpdateSupplier() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateSupplierRequest }) =>
      updateSupplier(id, request),
    onSuccess: (_, { id }) => {
      toast.success("Cập nhật nhà cung cấp thành công!");
      queryClient.invalidateQueries({ queryKey: SUPPLIERS_QUERY_KEY });
      queryClient.invalidateQueries({ queryKey: [...SUPPLIERS_QUERY_KEY, id] });
    },
    onError: (error: unknown) => {
      toast.error(getApiErrorMessage(error, "Đã có lỗi xảy ra khi cập nhật nhà cung cấp."));
    },
  });
}

export function useUpdateSupplierStatus() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => updateSupplierStatus(id, isActive),
    onSuccess: (_, { id, isActive }) => {
      toast.success(isActive ? "Đã kích hoạt nhà cung cấp" : "Đã vô hiệu hóa nhà cung cấp");
      queryClient.invalidateQueries({ queryKey: SUPPLIERS_QUERY_KEY });
      queryClient.invalidateQueries({ queryKey: [...SUPPLIERS_QUERY_KEY, id] });
    },
    onError: (error: unknown) => {
      toast.error(getApiErrorMessage(error, "Đã có lỗi xảy ra khi thay đổi trạng thái."));
    },
  });
}
