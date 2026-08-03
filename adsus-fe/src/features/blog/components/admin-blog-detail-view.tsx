"use client";

import { ArrowLeft, Calendar, CheckCircle } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";

import { getApiErrorMessage } from "@/lib/api-client";

import { useAdminBlogPost, usePublishBlogPost, useUpdateBlogPost } from "../hooks/use-admin-blog";

type BlogStatus = "DRAFT" | "PUBLISHED";

const STATUS_LABELS: Record<BlogStatus, string> = {
  DRAFT: "Bản nháp",
  PUBLISHED: "Đã xuất bản",
};

const STATUS_STYLES: Record<BlogStatus, string> = {
  DRAFT: "bg-amber-100 text-amber-800",
  PUBLISHED: "bg-green-100 text-green-800",
};

/**
 * Admin Blog Detail/Edit View - requires ADMIN role.
 * SCR-27 - Chi tiết và chỉnh sửa bài viết
 */
export function AdminBlogDetailView({ id }: { id: string }) {
  const router = useRouter();
  const { data: post, isLoading, isError, error } = useAdminBlogPost(id);
  const updateMutation = useUpdateBlogPost();
  const publishMutation = usePublishBlogPost();

  const [isEditing, setIsEditing] = useState(false);
  const [title, setTitle] = useState("");
  const [content, setContent] = useState("");

  // Initialize form when post loads
  if (post && !isEditing && (title === "" || content === "")) {
    setTitle(post.title);
    setContent(post.content);
  }

  const handleSave = async () => {
    try {
      await updateMutation.mutateAsync({
        id,
        payload: { title, content },
      });
      setIsEditing(false);
    } catch (e) {
      // Error handled by mutation
    }
  };

  const handlePublish = async () => {
    try {
      await publishMutation.mutateAsync(id);
    } catch (e) {
      // Error handled by mutation
    }
  };

  return (
    <div className="min-h-screen bg-[var(--muted)]">
      {/* Header */}
      <div className="bg-white border-b border-border">
        <div className="mx-auto max-w-4xl px-6 py-4">
          <div className="flex items-center justify-between">
            <Link
              href="/admin/blog"
              className="inline-flex items-center gap-2 text-sm text-[var(--muted-foreground)] transition-colors hover:text-[var(--primary)]"
            >
              <ArrowLeft className="h-4 w-4" />
              Quay lại danh sách
            </Link>
            {post && !isEditing && post.status === "DRAFT" && (
              <button
                onClick={() => setIsEditing(true)}
                className="inline-flex items-center gap-2 rounded-lg border border-[var(--accent)] px-4 py-2 text-sm font-medium text-[var(--accent)] transition-colors hover:bg-[var(--accent)]/10"
              >
                Chỉnh sửa
              </button>
            )}
          </div>
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
          <div className="space-y-6">
            {/* Article Info */}
            <div className="rounded-xl border border-border bg-white p-6">
              <div className="flex items-start justify-between gap-4">
                <div className="flex-1">
                  <h1 className="font-heading text-2xl font-bold text-[var(--primary)]">
                    {isEditing ? (
                      <input
                        type="text"
                        value={title}
                        onChange={(e) => setTitle(e.target.value)}
                        className="w-full rounded-lg border border-border px-4 py-2 text-2xl font-bold focus:border-[var(--accent)] focus:outline-none"
                        placeholder="Tiêu đề bài viết"
                      />
                    ) : (
                      post.title
                    )}
                  </h1>
                  <div className="mt-2 flex items-center gap-4 text-sm text-[var(--muted-foreground)]">
                    <span className="font-medium">{post.authorName}</span>
                    <span className="flex items-center gap-1">
                      <Calendar className="h-4 w-4" />
                      {new Date(post.createdAt).toLocaleDateString("vi-VN")}
                    </span>
                    <span
                      className={`rounded-full px-2.5 py-1 text-xs font-semibold ${STATUS_STYLES[post.status]}`}
                    >
                      {STATUS_LABELS[post.status]}
                    </span>
                  </div>
                </div>
                {post.status === "DRAFT" && !isEditing && (
                  <button
                    onClick={handlePublish}
                    disabled={publishMutation.isPending}
                    className="inline-flex items-center gap-2 rounded-lg bg-[var(--accent)] px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-[var(--accent)]/90 disabled:opacity-50"
                  >
                    <CheckCircle className="h-4 w-4" />
                    {publishMutation.isPending ? "Đang xuất bản..." : "Xuất bản"}
                  </button>
                )}
              </div>
            </div>

            {/* Article Content */}
            <div className="rounded-xl border border-border bg-white p-6">
              <h2 className="mb-4 font-heading text-lg font-semibold text-[var(--primary)]">
                Nội dung bài viết
              </h2>
              {isEditing ? (
                <textarea
                  value={content}
                  onChange={(e) => setContent(e.target.value)}
                  rows={12}
                  className="w-full rounded-lg border border-border px-4 py-3 font-mono text-sm focus:border-[var(--accent)] focus:outline-none"
                  placeholder="Nội dung bài viết (Markdown)"
                />
              ) : (
                <div className="whitespace-pre-wrap text-[var(--foreground)]">
                  {post.content}
                </div>
              )}
            </div>

            {/* Actions */}
            {isEditing && (
              <div className="flex items-center justify-end gap-3">
                <button
                  onClick={() => {
                    setIsEditing(false);
                    setTitle(post.title);
                    setContent(post.content);
                  }}
                  className="rounded-lg border border-border px-4 py-2 text-sm font-medium transition-colors hover:bg-secondary"
                >
                  Hủy
                </button>
                <button
                  onClick={handleSave}
                  disabled={updateMutation.isPending}
                  className="rounded-lg bg-[var(--accent)] px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-[var(--accent)]/90 disabled:opacity-50"
                >
                  {updateMutation.isPending ? "Đang lưu..." : "Lưu thay đổi"}
                </button>
              </div>
            )}

            {/* Error messages */}
            {updateMutation.isError && (
              <div className="rounded-lg border border-destructive/25 bg-destructive/5 p-4 text-sm text-destructive">
                {getApiErrorMessage(updateMutation.error, "Không lưu được bài viết.")}
              </div>
            )}

            {publishMutation.isError && (
              <div className="rounded-lg border border-destructive/25 bg-destructive/5 p-4 text-sm text-destructive">
                {getApiErrorMessage(publishMutation.error, "Không xuất bản được bài viết.")}
              </div>
            )}

            {updateMutation.isSuccess && (
              <div className="rounded-lg border border-[var(--accent)]/25 bg-[var(--accent)]/5 p-4 text-sm text-[var(--accent)]">
                Bài viết đã được lưu thành công.
              </div>
            )}

            {publishMutation.isSuccess && (
              <div className="rounded-lg border border-[var(--accent)]/25 bg-[var(--accent)]/5 p-4 text-sm text-[var(--accent)]">
                Bài viết đã được xuất bản thành công.
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
