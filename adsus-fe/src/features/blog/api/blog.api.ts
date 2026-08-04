import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";

import type {
  AdminBlogPostDetailResponse,
  AdminBlogPostListItemResponse,
  AdminBlogPostListParams,
  BlogPostDetailResponse,
  BlogPostListItemResponse,
  BlogPostListParams,
  CreateBlogPostRequest,
  PagedResult,
  UpdateBlogPostRequest,
} from "../types/blog.types";

/**
 * Blog PUBLIC API - không cần authentication (AllowAnonymous)
 */

/**
 * GET /api/v1/blog-posts - Danh sách bài viết đã xuất bản
 */
export async function getPublicBlogPosts(
  params: BlogPostListParams = {},
): Promise<PagedResult<BlogPostListItemResponse>> {
  const { page = 1, pageSize = 10 } = params;
  const { data } = await apiClient.get<
    ApiResponse<PagedResult<BlogPostListItemResponse>>
  >("/api/v1/blog-posts", {
    params: { page, pageSize },
  });

  if (!data.data) {
    throw new Error(data.message || "Không tải được danh sách bài viết.");
  }

  return data.data;
}

/**
 * GET /api/v1/blog-posts/:id - Chi tiết bài viết
 */
export async function getPublicBlogPost(id: string): Promise<BlogPostDetailResponse> {
  const { data } = await apiClient.get<ApiResponse<BlogPostDetailResponse>>(
    `/api/v1/blog-posts/${id}`,
  );

  if (!data.data) {
    throw new Error(data.message || "Không tải được bài viết.");
  }

  return data.data;
}

/**
 * Blog ADMIN API - cần authentication (Authorize Roles = ADMIN)
 */

/**
 * GET /api/v1/admin/blog-posts - Danh sách tất cả bài viết (Admin)
 */
export async function getAdminBlogPosts(
  params: AdminBlogPostListParams = {},
): Promise<PagedResult<AdminBlogPostListItemResponse>> {
  const { page = 1, pageSize = 10, status } = params;
  // Filter undefined values để tránh gửi ?status=undefined lên server
  const cleanParams: Record<string, string | number> = { page, pageSize };
  if (status) cleanParams.status = status;

  const { data } = await apiClient.get<
    ApiResponse<PagedResult<AdminBlogPostListItemResponse>>
  >("/api/v1/admin/blog-posts", {
    params: cleanParams,
  });

  if (!data.data) {
    throw new Error(data.message || "Không tải được danh sách bài viết.");
  }

  // DEBUG: log response để kiểm tra cấu trúc
  if (typeof window !== "undefined") {
    console.log("[Blog Admin API] response:", JSON.stringify(data.data, null, 2));
  }

  // Normalize: Backend có thể trả enum là số (0/1) hoặc string ("DRAFT"/"PUBLISHED").
  // Đảm bảo Frontend luôn nhận string.
  const items = data.data.items.map((item) => ({
    ...item,
    status: normalizeStatus(item.status),
  }));

  return { ...data.data, items };
}

function normalizeStatus(status: unknown): "DRAFT" | "PUBLISHED" {
  if (typeof status === "string") {
    const upper = status.toUpperCase();
    if (upper === "DRAFT" || upper === "PUBLISHED") return upper;
  }
  // Backend mặc định ASP.NET serialize enum thành số:
  //   0 = Draft (giá trị đầu tiên trong enum BlogPostStatus)
  //   1 = Published
  if (typeof status === "number") {
    return status === 0 ? "DRAFT" : "PUBLISHED";
  }
  return "DRAFT"; // fallback an toàn
}

/**
 * GET /api/v1/admin/blog-posts/:id - Chi tiết bài viết (Admin)
 */
export async function getAdminBlogPost(
  id: string,
): Promise<AdminBlogPostDetailResponse> {
  const { data } = await apiClient.get<ApiResponse<AdminBlogPostDetailResponse>>(
    `/api/v1/admin/blog-posts/${id}`,
  );

  if (!data.data) {
    throw new Error(data.message || "Không tải được bài viết.");
  }

  return {
    ...data.data,
    status: normalizeStatus(data.data.status),
  };
}

/**
 * POST /api/v1/admin/blog-posts - Tạo bài viết mới (Draft)
 */
export async function createBlogPost(
  payload: CreateBlogPostRequest,
): Promise<AdminBlogPostDetailResponse> {
  const { data } = await apiClient.post<ApiResponse<AdminBlogPostDetailResponse>>(
    "/api/v1/admin/blog-posts",
    payload,
  );

  if (!data.data) {
    throw new Error(data.message || "Không tạo được bài viết.");
  }

  return data.data;
}

/**
 * PUT /api/v1/admin/blog-posts/:id - Cập nhật bài viết
 */
export async function updateBlogPost(
  id: string,
  payload: UpdateBlogPostRequest,
): Promise<AdminBlogPostDetailResponse> {
  const { data } = await apiClient.put<ApiResponse<AdminBlogPostDetailResponse>>(
    `/api/v1/admin/blog-posts/${id}`,
    payload,
  );

  if (!data.data) {
    throw new Error(data.message || "Không cập nhật được bài viết.");
  }

  return data.data;
}

/**
 * POST /api/v1/admin/blog-posts/:id/publish - Xuất bản bài viết
 */
export async function publishBlogPost(id: string): Promise<AdminBlogPostDetailResponse> {
  const { data } = await apiClient.post<ApiResponse<AdminBlogPostDetailResponse>>(
    `/api/v1/admin/blog-posts/${id}/publish`,
  );

  if (!data.data) {
    throw new Error(data.message || "Không xuất bản được bài viết.");
  }

  return data.data;
}
