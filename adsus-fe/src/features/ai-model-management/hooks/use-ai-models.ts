import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  activateAiModel,
  getActiveAiModel,
  getAiModelById,
  getAiModels,
  registerAiModel,
  updateAiModel,
  calculateMap50,
} from "../api/ai-models.api";

import type { AiModelListQuery } from "../types/ai-model.types";

export const queryKeys = {
  all: ["ai-models"] as const,
  lists: () => [...queryKeys.all, "list"] as const,
  list: (query: AiModelListQuery) => [...queryKeys.lists(), query] as const,
  details: () => [...queryKeys.all, "detail"] as const,
  detail: (id: string) => [...queryKeys.details(), id] as const,
  active: () => [...queryKeys.all, "active"] as const,
};

export function useAiModelList(query: AiModelListQuery) {
  return useQuery({
    queryKey: queryKeys.list(query),
    queryFn: () => getAiModels(query),
  });
}

export function useActiveAiModel() {
  return useQuery({
    queryKey: queryKeys.active(),
    queryFn: getActiveAiModel,
  });
}

export function useAiModelDetail(id?: string) {
  return useQuery({
    queryKey: queryKeys.detail(id!),
    queryFn: () => getAiModelById(id!),
    enabled: !!id,
  });
}

export function useRegisterAiModel() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: registerAiModel,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.lists() });
    },
  });
}

export function useUpdateAiModel() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: Parameters<typeof updateAiModel>[1] }) =>
      updateAiModel(id, payload),
    onSuccess: (_, { id }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.detail(id) });
      queryClient.invalidateQueries({ queryKey: queryKeys.lists() });
    },
  });
}

export function useActivateAiModel() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: activateAiModel,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.lists() });
      queryClient.invalidateQueries({ queryKey: queryKeys.details() });
      queryClient.invalidateQueries({ queryKey: queryKeys.active() });
      queryClient.invalidateQueries({ queryKey: ["dashboard"] });
    },
  });
}

export function useCalculateMap50() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: calculateMap50,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.lists() });
      queryClient.invalidateQueries({ queryKey: queryKeys.details() });
      queryClient.invalidateQueries({ queryKey: queryKeys.active() });
      queryClient.invalidateQueries({ queryKey: ["dashboard"] });
    },
  });
}
