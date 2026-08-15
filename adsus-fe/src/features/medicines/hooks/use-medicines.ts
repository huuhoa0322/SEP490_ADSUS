import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createMedicine,
  deleteMedicine, activateMedicine,
  getPagedMedicines,
  updateMedicine,
  type CreateMedicineRequest,
  type UpdateMedicineRequest,
} from "../api/medicines-api";

export function useMedicines(page: number, pageSize: number, search?: string) {
  return useQuery({
    queryKey: ["admin-medicines", page, pageSize, search],
    queryFn: () => getPagedMedicines(page, pageSize, search),
    staleTime: 5 * 60 * 1000,
  });
}

export function useCreateMedicine() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateMedicineRequest) => createMedicine(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-medicines"] });
      queryClient.invalidateQueries({ queryKey: ["search-medicines"] });
    },
  });
}

export function useUpdateMedicine() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateMedicineRequest }) =>
      updateMedicine(id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-medicines"] });
      queryClient.invalidateQueries({ queryKey: ["search-medicines"] });
    },
  });
}

export function useDeleteMedicine() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => deleteMedicine(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-medicines"] });
      queryClient.invalidateQueries({ queryKey: ["search-medicines"] });
    },
  });
}

export function useActivateMedicine() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => activateMedicine(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-medicines"] });
      queryClient.invalidateQueries({ queryKey: ["search-medicines"] });
    },
  });
}
