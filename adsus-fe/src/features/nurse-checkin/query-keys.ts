export const nurseCheckinQueryKeys = {
  all: ["nurse-checkin"] as const,
  queue: (date?: string) => [...nurseCheckinQueryKeys.all, "queue", date ?? "today"],
};
