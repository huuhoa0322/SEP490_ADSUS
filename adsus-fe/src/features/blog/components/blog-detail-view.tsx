"use client";

import { ArrowLeft, Calendar } from "lucide-react";
import Link from "next/link";
import { usePublicBlogPost } from "../hooks/use-blog";
import { getApiErrorMessage } from "@/lib/api-client";

/**
 * Blog detail view - PUBLIC, no authentication required.
 * SCR-26 - Chi tiết bài viết Blog Sức khỏe
 */
export function BlogDetailView({ id }: { id: string }) {
  const { data: post, isLoading, isError, error } = usePublicBlogPost(id);

  return (
    <div className="min-h-screen bg-[var(--muted)]">
      {/* Header with back button */}
      <div className="bg-white border-b border-border">
        <div className="mx-auto max-w-4xl px-6 py-4">
          <Link
            href="/blog"
            className="inline-flex items-center gap-2 text-sm text-[var(--muted-foreground)] transition-colors hover:text-[var(--primary)]"
          >
            <ArrowLeft className="h-4 w-4" />
            Quay lại danh sách
          </Link>
        </div>
      </div>

      {/* Content */}
      <div className="mx-auto max-w-4xl px-6 py-8">
        {isError && (
          <div
            role="alert"
            className="mb-6 flex items-start gap-2.5 rounded-xl border border-destructive/25 bg-destructive/5 px-4 py-3 text-sm text-destructive"
          >
            {getApiErrorMessage(error, "Không tải được bài viết.")}
          </div>
        )}

        {isLoading && !post && (
          <div className="flex min-h-64 items-center justify-center">
            <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary/20 border-t-primary" />
          </div>
        )}

        {post && (
          <article className="rounded-xl border border-border bg-white">
            {/* Article Header */}
            <div className="border-b border-border p-8">
              <h1 className="font-heading text-3xl font-bold leading-tight text-[var(--primary)]">
                {post.title}
              </h1>
              <div className="mt-4 flex items-center gap-4 text-sm text-[var(--muted-foreground)]">
                <span className="font-medium text-[var(--primary)]">
                  {post.authorName}
                </span>
                <span className="flex items-center gap-1">
                  <Calendar className="h-4 w-4" />
                  <span className="font-mono text-xs">
                    {post.publishedAt
                      ? new Date(post.publishedAt).toLocaleDateString("vi-VN", {
                          day: "2-digit",
                          month: "2-digit",
                          year: "numeric",
                        })
                      : new Date(post.createdAt).toLocaleDateString("vi-VN", {
                          day: "2-digit",
                          month: "2-digit",
                          year: "numeric",
                        })}
                  </span>
                </span>
              </div>
            </div>

            {/* Article Content */}
            <div className="p-8">
              <div className="prose prose-lg max-w-none text-[var(--foreground)]">
                {post.content.split("\n").map((paragraph, index) => (
                  <p key={index} className="mb-4 leading-relaxed">
                    {paragraph}
                  </p>
                ))}
              </div>
            </div>

            {/* Disclaimer */}
            <div className="border-t border-border p-6 bg-[var(--muted)]">
              <p className="text-sm text-[var(--muted-foreground)]">
                <strong>Lưu ý:</strong> Thông tin trong bài viết này chỉ mang tính chất tham khảo
                và không thay thế cho lời khuyên y tế chuyên môn. Vui lòng tham khảo bác sĩ
                của bạn để được tư vấn cụ thể.
              </p>
            </div>
          </article>
        )}
      </div>
    </div>
  );
}
