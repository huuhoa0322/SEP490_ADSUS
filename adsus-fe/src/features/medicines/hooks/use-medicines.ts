import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createMedicine,
  deleteMedicine, activateMedicine,
  getPagedMedicines,
  getMedicineById,
  updateMedicine,
  type CreateMedicineRequest,
  type UpdateMedicineRequest,
  getMedicineUnits,
  getPackagingsByMedicineId,
  addPackaging,
  updatePackaging,
  deletePackaging,
  type CreateMedicinePackagingRequest,
  type UpdateMedicinePackagingRequest,
} from "../api/medicines-api";

export function useMedicines(page: number, pageSize: number, search?: string, inStock?: boolean) {
  return useQuery({
    queryKey: ["admin-medicines", page, pageSize, search, inStock],
    queryFn: () => getPagedMedicines(page, pageSize, search, inStock),
    staleTime: 5 * 60 * 1000,
  });
}

export function useMedicineById(id: string) {
  return useQuery({
    queryKey: ["medicine-by-id", id],
    queryFn: () => getMedicineById(id),
    enabled: !!id,
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

export function useMedicineUnits() {
  return useQuery({
    queryKey: ["medicine-units"],
    queryFn: () => getMedicineUnits(),
    staleTime: 60 * 60 * 1000,
  });
}

export function useMedicinePackagings(medicineId: string) {
  return useQuery({
    queryKey: ["medicine-packagings", medicineId],
    queryFn: () => getPackagingsByMedicineId(medicineId),
    enabled: !!medicineId,
  });
}

export function useAddPackaging() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ medicineId, request }: { medicineId: string; request: CreateMedicinePackagingRequest }) =>
      addPackaging(medicineId, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["medicine-packagings", variables.medicineId] });
    },
  });
}

export function useUpdatePackaging() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ packagingId, request }: { packagingId: string; request: UpdateMedicinePackagingRequest }) =>
      updatePackaging(packagingId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["medicine-packagings"] });
    },
  });
}

export function useDeletePackaging() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (packagingId: string) => deletePackaging(packagingId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["medicine-packagings"] });
    },
  });
}
