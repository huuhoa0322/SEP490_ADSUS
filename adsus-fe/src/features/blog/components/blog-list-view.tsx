"use client";

import { FileText } from "lucide-react";
import Link from "next/link";
import { useState } from "react";

import { getApiErrorMessage } from "@/lib/api-client";

import { usePublicBlogPosts } from "../hooks/use-blog";
import type { BlogPostListItemResponse } from "../types/blog.types";
import { PaginationNumbered } from "@/components/ui/pagination-numbered";

/**
 * Blog list view - PUBLIC, no authentication required.
 * SCR-26 - Blog Sức khỏe Patient
 */
export function BlogListView() {
  const [page, setPage] = useState(1);
  const pageSize = 10;

  const { data, isLoading, isError, error } = usePublicBlogPosts({ page, pageSize });

  return (
    <div className="min-h-screen bg-muted">
      {/* Hero Header */}
      <div className="relative overflow-hidden border-b border-border bg-background">
        <div className="absolute inset-x-0 bottom-0 h-[3px] bg-gradient-to-r from-primary to-accent" />
        <div className="mx-auto max-w-4xl px-6 py-14">
          <span className="inline-flex items-center gap-1.5 rounded-full bg-accent/10 px-3 py-1 text-xs font-700 uppercase tracking-wider text-accent">
            <span className="size-1.5 rounded-full bg-accent" />
            Kiến thức y khoa
          </span>
          <h1 className="mt-4 font-heading text-4xl font-bold tracking-tight text-foreground">
            Blog Sức khỏe
          </h1>
          <p className="mt-2.5 text-[15px] text-muted-foreground">
            Bài viết y tế được kiểm duyệt bởi bác sĩ chuyên khoa
          </p>
        </div>
      </div>

      {/* Content */}
      <div className="mx-auto max-w-4xl px-6 py-10">
        {isError && (
          <div
            role="alert"
            className="mb-6 flex items-start gap-2.5 rounded-2xl border border-destructive/25 bg-destructive/5 px-4 py-3 text-sm text-destructive"
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
              <div className="rounded-2xl border-2 border-dashed border-border bg-background py-16 text-center">
                <FileText className="mx-auto mb-3 h-12 w-12 text-muted-foreground/40" />
                <p className="text-muted-foreground">
                  Chưa có bài viết nào được xuất bản.
                </p>
              </div>
            )}

            {/* Pagination */}
            {data.totalPages > 1 && (
              <PaginationNumbered
                currentPage={page}
                totalPages={data.totalPages}
                setPage={setPage}
                className="mt-8 justify-center"
              />
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
      className="group block rounded-2xl border border-border bg-background p-6 transition-all hover:border-accent hover:shadow-md"
    >
      <div className="mb-3">
        <span className="inline-block rounded-full bg-accent/10 px-3 py-1 text-xs font-700 uppercase tracking-wider text-accent">
          Bài viết nổi bật
        </span>
      </div>
      <h2 className="font-heading text-2xl font-bold leading-snug text-foreground group-hover:text-accent">
        {post.title}
      </h2>
      <p className="mt-2 line-clamp-2 text-muted-foreground">
        {post.content}
      </p>
      <div className="mt-4 flex items-center gap-2 text-sm text-muted-foreground">
        <span className="font-medium text-foreground">{post.authorName}</span>
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
      className="group flex gap-4 rounded-2xl border border-border bg-background p-4 transition-all hover:border-accent hover:shadow-sm"
    >
      {/* Thumbnail placeholder */}
      <div className="flex h-24 w-24 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-accent/10 to-accent/5 text-3xl text-accent">
        📖
      </div>

      <div className="flex flex-1 flex-col justify-between">
        <div>
          <h3 className="font-heading text-base font-semibold leading-snug text-foreground group-hover:text-accent">
            {post.title}
          </h3>
          <p className="mt-1 line-clamp-2 text-sm text-muted-foreground">
            {post.content}
          </p>
        </div>
        <div className="mt-2 flex items-center gap-2 text-xs text-muted-foreground">
          <span className="font-medium text-foreground">{post.authorName}</span>
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
