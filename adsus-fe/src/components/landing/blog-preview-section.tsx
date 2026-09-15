"use client";

import Link from "next/link";

import { usePublicBlogPosts } from "@/features/blog/hooks/use-blog";
import type { BlogPostListItemResponse } from "@/features/blog/types/blog.types";

function formatDate(value: string | null, fallback: string): string {
  if (!value) return new Date(fallback).toLocaleDateString("vi-VN");
  const d = new Date(value);
  return Number.isNaN(d.getTime())
    ? new Date(fallback).toLocaleDateString("vi-VN")
    : d.toLocaleDateString("vi-VN");
}

function excerptFrom(content: string | undefined, max = 120): string {
  if (!content) return "";
  const stripped = content.replace(/\s+/g, " ").trim();
  return stripped.length > max ? `${stripped.slice(0, max)}…` : stripped;
}

export function BlogPreviewSection() {
  const { data, isLoading, isError } = usePublicBlogPosts({ page: 1, pageSize: 3 });
  const posts = data?.items ?? [];

  return (
    <section
      className="px-4 py-16 sm:px-6 lg:px-8"
      style={{ backgroundColor: "var(--lp-surface)" }}
    >
      <div className="mx-auto max-w-6xl">
        {/* Header */}
        <div className="mb-10 flex items-end justify-between gap-4">
          <div>
            <span
              className="mb-2 inline-block text-xs font-semibold uppercase tracking-widest"
              style={{ color: "var(--lp-teal)" }}
            >
              Kiến thức sức khỏe
            </span>
            <h2
              className="text-2xl sm:text-3xl"
              style={{
                fontFamily: "var(--lp-font-serif)",
                fontWeight: 600,
                color: "var(--lp-navy)",
              }}
            >
              Bài viết mới nhất
            </h2>
          </div>
          <Link
            href="/blog"
            className="shrink-0 text-sm font-medium transition-opacity hover:opacity-70"
            style={{ color: "var(--lp-teal)" }}
          >
            Xem tất cả →
          </Link>
        </div>

        {/* Posts grid */}
        {isError && (
          <div
            role="alert"
            className="rounded-xl border px-4 py-6 text-center text-sm"
            style={{
              borderColor: "var(--lp-border)",
              color: "var(--lp-muted)",
              backgroundColor: "var(--lp-surface)",
            }}
          >
            Không tải được bài viết. Vui lòng thử lại sau.
          </div>
        )}

        {isLoading && posts.length === 0 && (
          <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
            {Array.from({ length: 3 }).map((_, i) => (
              <div
                key={i}
                className="h-48 animate-pulse rounded-xl border"
                style={{
                  backgroundColor: "var(--lp-surface)",
                  borderColor: "var(--lp-border)",
                }}
              />
            ))}
          </div>
        )}

        {!isLoading && posts.length === 0 && !isError && (
          <div
            className="rounded-xl border px-4 py-10 text-center text-sm"
            style={{
              borderColor: "var(--lp-border)",
              color: "var(--lp-muted)",
              backgroundColor: "var(--lp-surface)",
            }}
          >
            Chưa có bài viết nào được xuất bản.
          </div>
        )}

        {posts.length > 0 && (
          <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
            {posts.map((post) => (
              <BlogPreviewCard key={post.id} post={post} />
            ))}
          </div>
        )}
      </div>
    </section>
  );
}

function BlogPreviewCard({ post }: { post: BlogPostListItemResponse }) {
  return (
    <Link
      href={`/blog/${post.id}`}
      className="group flex flex-col rounded-xl border p-5 transition-shadow hover:shadow-md"
      style={{
        backgroundColor: "var(--lp-surface)",
        borderColor: "var(--lp-border)",
      }}
    >
      {/* Tag */}
      <span
        className="mb-3 inline-block self-start rounded-full px-2.5 py-0.5 text-xs font-semibold"
        style={{
          backgroundColor: "var(--lp-teal-tint)",
          color: "var(--lp-teal)",
        }}
      >
        Sức khỏe
      </span>

      {/* Title */}
      <h3
        className="mb-2 text-base font-semibold leading-snug transition-colors group-hover:opacity-80"
        style={{ color: "var(--lp-navy)", fontFamily: "var(--lp-font-serif)" }}
      >
        {post.title}
      </h3>

      {/* Excerpt */}
      <p
        className="mb-4 flex-1 text-sm leading-relaxed"
        style={{ color: "var(--lp-muted)" }}
      >
        {excerptFrom(post.content) || "Đang cập nhật nội dung..."}
      </p>

      {/* Meta */}
      <div className="flex items-center gap-2 text-xs">
        <span className="font-medium" style={{ color: "var(--lp-navy)" }}>
          {post.authorName}
        </span>
        <span style={{ color: "var(--lp-muted)" }}>·</span>
        <time
          style={{ color: "var(--lp-muted)", fontFamily: "var(--lp-font-mono)" }}
        >
          {formatDate(post.publishedAt, post.createdAt)}
        </time>
      </div>
    </Link>
  );
}
