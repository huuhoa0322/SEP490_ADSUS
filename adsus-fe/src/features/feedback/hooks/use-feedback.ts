import { useQuery } from "@tanstack/react-query";
import { getAdminFeedbacks } from "../api/feedback.api";
import type { FeedbackListQuery } from "../types/feedback.types";

export const feedbackQueryKeys = {
  all: ["admin-feedbacks"] as const,
  lists: () => [...feedbackQueryKeys.all, "list"] as const,
  list: (query: FeedbackListQuery) => [...feedbackQueryKeys.lists(), query] as const,
};

export function useAdminFeedbacks(query: FeedbackListQuery) {
  return useQuery({
    queryKey: feedbackQueryKeys.list(query),
    queryFn: () => getAdminFeedbacks(query),
  });
}
