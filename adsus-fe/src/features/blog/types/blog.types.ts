/**
 * Types cho Blog Module (Module 10 Engagement).
 * Backend API: /api/v1/blog-posts (public), /api/v1/admin/blog-posts (admin)
 */

// Backend trả về AuthorName là string, không phải nested object
export interface BlogPostListItemResponse {
  id: string;
  title: string;
  content?: string;
  authorName: string;
  publishedAt: string | null;
  createdAt: string;
  updatedAt?: string;
}

export interface BlogPostDetailResponse {
  id: string;
  title: string;
  content: string;
  authorName: string;
  publishedAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface BlogPostListParams {
  page?: number;
  pageSize?: number;
}

export interface CreateBlogPostRequest {
  title: string;
  content: string;
}

export interface UpdateBlogPostRequest {
  title: string;
  content: string;
}

export interface AdminBlogPostListItemResponse {
  id: string;
  title: string;
  status: BlogStatus;
  publishedAt: string | null;
  createdAt: string;
  authorName: string;
}

export interface AdminBlogPostDetailResponse {
  id: string;
  title: string;
  content: string;
  status: BlogStatus;
  publishedAt: string | null;
  createdAt: string;
  authorName: string;
}

export type BlogStatus = "DRAFT" | "PUBLISHED";

export interface AdminBlogPostListParams extends BlogPostListParams {
  status?: "DRAFT" | "PUBLISHED";
}
