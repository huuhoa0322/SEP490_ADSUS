"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  createBlogPost,
  getAdminBlogPost,
  getAdminBlogPosts,
  publishBlogPost,
  updateBlogPost,
} from "../api/blog.api";
import type {
  AdminBlogPostListParams,
  CreateBlogPostRequest,
  UpdateBlogPostRequest,
} from "../types/blog.types";

/**
 * Hook for fetching admin blog posts list (requires ADMIN role)
 */
export function useAdminBlogPosts(params: AdminBlogPostListParams = {}) {
  return useQuery({
    queryKey: ["blog", "admin", "posts", params],
    queryFn: () => getAdminBlogPosts(params),
  });
}

/**
 * Hook for fetching a single admin blog post detail (requires ADMIN role)
 */
export function useAdminBlogPost(id: string | null) {
  return useQuery({
    queryKey: ["blog", "admin", "post", id],
    queryFn: () => getAdminBlogPost(id!),
    enabled: Boolean(id),
  });
}

/**
 * Hook for creating a new blog post (requires ADMIN role)
 */
export function useCreateBlogPost() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: CreateBlogPostRequest) => createBlogPost(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["blog", "admin"] });
    },
  });
}

/**
 * Hook for updating a blog post (requires ADMIN role)
 */
export function useUpdateBlogPost() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: UpdateBlogPostRequest }) =>
      updateBlogPost(id, payload),
    onSuccess: (_, { id }) => {
      queryClient.invalidateQueries({ queryKey: ["blog", "admin"] });
      queryClient.invalidateQueries({ queryKey: ["blog", "admin", "post", id] });
    },
  });
}

/**
 * Hook for publishing a blog post (requires ADMIN role)
 */
export function usePublishBlogPost() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => publishBlogPost(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["blog", "admin"] });
    },
  });
}
