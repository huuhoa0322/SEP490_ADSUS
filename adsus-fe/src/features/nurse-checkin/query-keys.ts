import type { CheckinQueueParams } from "./types/checkin.types";

export const nurseCheckinQueryKeys = {
  all: ["nurse-checkin"] as const,
  queue: (params?: string | CheckinQueueParams) => [
    ...nurseCheckinQueryKeys.all,
    "queue",
    typeof params === "string" ? { date: params } : (params ?? {}),
  ],
  doctors: () => [...nurseCheckinQueryKeys.all, "doctors"] as const,
  availableSlots: (params?: { doctorId?: string; fromDate?: string; toDate?: string }) => [
    ...nurseCheckinQueryKeys.all,
    "available-slots",
    params ?? {},
  ],
};

