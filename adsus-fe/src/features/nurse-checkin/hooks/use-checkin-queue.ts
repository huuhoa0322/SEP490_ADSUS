"use client";

import { useQuery } from "@tanstack/react-query";
import { getCheckinQueue } from "../api/checkin.api";
import { nurseCheckinQueryKeys } from "../query-keys";
import type { CheckinQueueParams } from "../types/checkin.types";

export function useCheckinQueue(params?: string | CheckinQueueParams) {
  return useQuery({
    queryKey: nurseCheckinQueryKeys.queue(params),
    queryFn: () => getCheckinQueue(params),
    refetchInterval: 30000, // Refetch every 30 seconds
    placeholderData: (prev) => prev,
  });
}

