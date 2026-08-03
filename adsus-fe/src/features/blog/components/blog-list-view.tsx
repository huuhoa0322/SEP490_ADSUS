"use client";

import { FileText } from "lucide-react";
import Link from "next/link";
import { useState } from "react";

import { getApiErrorMessage } from "@/lib/api-client";

import { usePublicBlogPosts } from "../hooks/use-blog";
import type { BlogPostListItemResponse } from "../types/blog.types";

/**
 * Blog list view - PUBLIC, no authentication required.
 * SCR-26 - Blog Sức khỏe Patient
 */
export function BlogListView() {
  const [page, setPage] = useState(1);
  const pageSize = 10;

  const { data, isLoading, isError, error } = usePublicBlogPosts({ page, pageSize });

  return (
    <div className="min-h-screen bg-[var(--muted)]">
      {/* Hero Header */}
      <div className="bg-white border-b border-border">
        <div className="mx-auto max-w-4xl px-6 py-12">
          <h1 className="font-heading text-3xl font-bold tracking-tight text-[var(--primary)]">
            Blog Sức khỏe
          </h1>
          <p className="mt-2 text-[var(--muted-foreground)]">
            Bài viết y tế được kiểm duyệt bởi bác sĩ chuyên khoa
          </p>
        </div>
      </div>

      {/* Content */}
      <div className="mx-auto max-w-4xl px-6 py-8">
        {isError && (
          <div
            role="alert"
            className="mb-6 flex items-start gap-2.5 rounded-xl border border-destructive/25 bg-destructive/5 px-4 py-3 text-sm text-destructive"
          >
            {getApiErrorMessage(error, "Không tải được danh sách bài viết.")}
          </div>
        )}

        {isLoading && !data && (
          <div className="flex min-h-64 items-center justify-center">
            <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary/20 border-t-primary" />
          </div>
        )}

        {data && (
          <>
            {/* Hero post - first item */}
            {data.items.length > 0 && (
              <HeroPost post={data.items[0]} />
            )}

            {/* Article list */}
            <div className="mt-8 space-y-4">
              {data.items.slice(1).map((post) => (
                <ArticleCard key={post.id} post={post} />
              ))}
            </div>

            {/* Empty state */}
            {data.items.length === 0 && (
              <div className="rounded-xl border-2 border-dashed border-border bg-white py-16 text-center">
                <FileText className="mx-auto mb-3 h-12 w-12 text-[var(--muted-foreground)]" />
                <p className="text-[var(--muted-foreground)]">
                  Chưa có bài viết nào được xuất bản.
                </p>
              </div>
            )}

            {/* Pagination */}
            {data.totalPages > 1 && (
              <div className="mt-8 flex items-center justify-center gap-2">
                <button
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page === 1}
                  className="rounded-lg border border-border px-4 py-2 text-sm font-medium transition-colors hover:bg-secondary disabled:cursor-not-allowed disabled:opacity-50"
                >
                  Trước
                </button>
                <span className="px-4 text-sm text-[var(--muted-foreground)]">
                  Trang {page} / {data.totalPages}
                </span>
                <button
                  onClick={() => setPage((p) => Math.min(data.totalPages, p + 1))}
                  disabled={page === data.totalPages}
                  className="rounded-lg border border-border px-4 py-2 text-sm font-medium transition-colors hover:bg-secondary disabled:cursor-not-allowed disabled:opacity-50"
                >
                  Sau
                </button>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}

/**
 * Hero post - featured article displayed prominently
 */
function HeroPost({ post }: { post: BlogPostListItemResponse }) {
  return (
    <Link
      href={`/blog/${post.id}`}
      className="group block rounded-xl border border-border bg-white p-6 transition-all hover:border-[var(--accent)] hover:shadow-md"
    >
      <div className="mb-3">
        <span className="inline-block rounded-full bg-[var(--accent)]/10 px-3 py-1 text-xs font-bold uppercase tracking-wider text-[var(--accent)]">
          Bài viết nổi bật
        </span>
      </div>
      <h2 className="font-heading text-2xl font-bold leading-snug text-[var(--primary)] group-hover:text-[var(--accent)]">
        {post.title}
      </h2>
      <p className="mt-2 line-clamp-2 text-[var(--muted-foreground)]">
        {post.content}
      </p>
      <div className="mt-4 flex items-center gap-2 text-sm text-[var(--muted-foreground)]">
        <span className="font-medium text-[var(--primary)]">{post.authorName}</span>
        <span>·</span>
        <span className="font-mono text-xs">
          {post.publishedAt
            ? new Date(post.publishedAt).toLocaleDateString("vi-VN")
            : new Date(post.createdAt).toLocaleDateString("vi-VN")}
        </span>
      </div>
    </Link>
  );
}

/**
 * Article card - smaller preview for list
 */
function ArticleCard({ post }: { post: BlogPostListItemResponse }) {
  return (
    <Link
      href={`/blog/${post.id}`}
      className="group flex gap-4 rounded-xl border border-border bg-white p-4 transition-all hover:border-[var(--accent)] hover:shadow-sm"
    >
      {/* Thumbnail placeholder */}
      <div className="flex h-24 w-24 shrink-0 items-center justify-center rounded-lg bg-gradient-to-br from-[var(--accent)]/10 to-[var(--accent)]/5 text-3xl text-[var(--accent)]">
        📖
      </div>

      <div className="flex flex-1 flex-col justify-between">
        <div>
          <h3 className="font-heading text-base font-semibold leading-snug text-[var(--primary)] group-hover:text-[var(--accent)]">
            {post.title}
          </h3>
          <p className="mt-1 line-clamp-2 text-sm text-[var(--muted-foreground)]">
            {post.content}
          </p>
        </div>
        <div className="mt-2 flex items-center gap-2 text-xs text-[var(--muted-foreground)]">
          <span className="font-medium">{post.authorName}</span>
          <span className="font-mono">
            {post.publishedAt
              ? new Date(post.publishedAt).toLocaleDateString("vi-VN")
              : new Date(post.createdAt).toLocaleDateString("vi-VN")}
          </span>
        </div>
      </div>
    </Link>
  );
}
