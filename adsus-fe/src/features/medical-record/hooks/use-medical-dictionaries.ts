import { useQuery } from "@tanstack/react-query";

import { listAllergyTypes, listDiseases } from "../api/medical-dictionaries.api";

export const medicalDictionariesKeys = {
  all: ["medical-dictionaries"] as const,
  diseases: () => [...medicalDictionariesKeys.all, "diseases"] as const,
  allergyTypes: () => [...medicalDictionariesKeys.all, "allergy-types"] as const,
};

export const useDiseases = () => {
  return useQuery({
    queryKey: medicalDictionariesKeys.diseases(),
    queryFn: listDiseases,
    staleTime: 24 * 60 * 60 * 1000, // 24h
  });
};

export const useAllergyTypes = () => {
  return useQuery({
    queryKey: medicalDictionariesKeys.allergyTypes(),
    queryFn: listAllergyTypes,
    staleTime: 24 * 60 * 60 * 1000, // 24h
  });
};
