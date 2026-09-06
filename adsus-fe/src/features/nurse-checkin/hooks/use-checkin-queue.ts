"use client";

import { useQuery } from "@tanstack/react-query";
import { getCheckinQueue } from "../api/checkin.api";
import { nurseCheckinQueryKeys } from "../query-keys";

export function useCheckinQueue(date?: string) {
  return useQuery({
    queryKey: nurseCheckinQueryKeys.queue(date),
    queryFn: () => getCheckinQueue(date),
    refetchInterval: 30000, // Refetch every 30 seconds
  });
}
