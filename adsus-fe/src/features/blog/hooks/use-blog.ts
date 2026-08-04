"use client";

import { useQuery } from "@tanstack/react-query";

import {
  getPublicBlogPost,
  getPublicBlogPosts,
} from "../api/blog.api";
import type { BlogPostListParams } from "../types/blog.types";

/**
 * Hook for fetching public blog posts list (no authentication required)
 */
export function usePublicBlogPosts(params: BlogPostListParams = {}) {
  return useQuery({
    queryKey: ["blog", "public", "posts", params],
    queryFn: () => getPublicBlogPosts(params),
  });
}

/**
 * Hook for fetching a single public blog post detail (no authentication required)
 */
export function usePublicBlogPost(id: string | null) {
  return useQuery({
    queryKey: ["blog", "public", "post", id],
    queryFn: () => getPublicBlogPost(id!),
    enabled: Boolean(id),
  });
}
