import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/lib/api-client";
import { MedicalDisease, MedicalAllergyType } from "../types/medical-record.types";
import { ApiResponse } from "@/types/api.types";

export const medicalDictionariesKeys = {
  all: ["medical-dictionaries"] as const,
  diseases: () => [...medicalDictionariesKeys.all, "diseases"] as const,
  allergyTypes: () => [...medicalDictionariesKeys.all, "allergy-types"] as const,
};

export const useDiseases = () => {
  return useQuery({
    queryKey: medicalDictionariesKeys.diseases(),
    queryFn: async () => {
      const response = await apiClient.get<ApiResponse<MedicalDisease[]>>(
        "/api/v1/medical-dictionaries/diseases"
      );
      return response.data.data;
    },
    staleTime: 24 * 60 * 60 * 1000, // 24h
  });
};

export const useAllergyTypes = () => {
  return useQuery({
    queryKey: medicalDictionariesKeys.allergyTypes(),
    queryFn: async () => {
      const response = await apiClient.get<ApiResponse<MedicalAllergyType[]>>(
        "/api/v1/medical-dictionaries/allergy-types"
      );
      return response.data.data;
    },
    staleTime: 24 * 60 * 60 * 1000, // 24h
  });
};
