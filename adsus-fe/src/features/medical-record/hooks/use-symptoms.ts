import { useQuery } from "@tanstack/react-query";

import { getSymptomCategories } from "../api/symptoms.api";
import { medicalRecordQueryKeys } from "./query-keys";

export function useSymptomCategories() {
  return useQuery({
    queryKey: medicalRecordQueryKeys.symptoms(),
    queryFn: () => getSymptomCategories(),
  });
}
