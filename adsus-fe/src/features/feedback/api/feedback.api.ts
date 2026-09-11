import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";
import type {
  AdminFeedbackItem,
  FeedbackListQuery,
  PagedResult,
} from "../types/feedback.types";

const BASE = "/api/v1/admin/feedbacks";

export async function getAdminFeedbacks(
  query: FeedbackListQuery = {}
): Promise<PagedResult<AdminFeedbackItem>> {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? 15;
  const trimmedSearch = query.search?.trim();

  const { data } = await apiClient.get<ApiResponse<PagedResult<AdminFeedbackItem>>>(BASE, {
    params: {
      page,
      pageSize,
      search: trimmedSearch || undefined,
      minRating: query.minRating && query.minRating > 0 ? query.minRating : undefined,
    },
  });

  if (!data.data) {
    throw new Error(data.message || "Không tải được danh sách phản hồi.");
  }

  const raw = data.data;
  const totalItems =
    raw.totalItems ??
    (raw as unknown as { totalCount?: number }).totalCount ??
    raw.items?.length ??
    0;

  return {
    items: raw.items ?? [],
    page: raw.page ?? page,
    pageSize: raw.pageSize ?? pageSize,
    totalItems,
    totalPages:
      raw.totalPages ?? (totalItems === 0 ? 0 : Math.ceil(totalItems / (raw.pageSize ?? pageSize))),
  };
}
