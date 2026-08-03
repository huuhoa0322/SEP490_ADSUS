"use client";

import { ArrowLeft, Send } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";

import { getApiErrorMessage } from "@/lib/api-client";

import { useCreateBlogPost } from "../hooks/use-admin-blog";

/**
 * Admin Blog Create View - requires ADMIN role.
 * SCR-27 - Tạo bài viết mới (bản Draft)
 */
export function AdminBlogCreateView() {
  const router = useRouter();
  const createMutation = useCreateBlogPost();

  const [title, setTitle] = useState("");
  const [content, setContent] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!title.trim() || !content.trim()) {
      return;
    }

    try {
      const result = await createMutation.mutateAsync({
        title: title.trim(),
        content: content.trim(),
      });
      router.push(`/admin/blog/${result.id}`);
    } catch (e) {
      // Error handled by mutation
    }
  };

  return (
    <div className="min-h-screen bg-[var(--muted)]">
      {/* Header */}
      <div className="bg-white border-b border-border">
        <div className="mx-auto max-w-4xl px-6 py-4">
          <Link
            href="/admin/blog"
            className="inline-flex items-center gap-2 text-sm text-[var(--muted-foreground)] transition-colors hover:text-[var(--primary)]"
          >
            <ArrowLeft className="h-4 w-4" />
            Quay lại danh sách
          </Link>
        </div>
      </div>

      {/* Content */}
      <div className="mx-auto max-w-4xl px-6 py-8">
        <div className="mb-6">
          <h1 className="font-heading text-2xl font-bold text-[var(--primary)]">
            Tạo bài viết mới
          </h1>
          <p className="mt-1 text-sm text-[var(--muted-foreground)]">
            Bài viết sẽ được tạo ở trạng thái bản nháp. Bạn có thể chỉnh sửa và xuất bản sau.
          </p>
        </div>

        <form onSubmit={handleSubmit} className="space-y-6">
          {/* Title */}
          <div className="rounded-xl border border-border bg-white p-6">
            <label className="block">
              <span className="text-sm font-medium text-[var(--primary)]">Tiêu đề bài viết</span>
              <input
                type="text"
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                required
                className="mt-1 block w-full rounded-lg border border-border px-4 py-2.5 focus:border-[var(--accent)] focus:outline-none"
                placeholder="Nhập tiêu đề bài viết..."
              />
            </label>
          </div>

          {/* Content */}
          <div className="rounded-xl border border-border bg-white p-6">
            <label className="block">
              <span className="text-sm font-medium text-[var(--primary)]">Nội dung bài viết</span>
              <textarea
                value={content}
                onChange={(e) => setContent(e.target.value)}
                required
                rows={15}
                className="mt-1 block w-full rounded-lg border border-border px-4 py-3 font-mono text-sm focus:border-[var(--accent)] focus:outline-none"
                placeholder="Nhập nội dung bài viết (Markdown)..."
              />
            </label>
          </div>

          {/* Error */}
          {createMutation.isError && (
            <div className="rounded-lg border border-destructive/25 bg-destructive/5 p-4 text-sm text-destructive">
              {getApiErrorMessage(createMutation.error, "Không tạo được bài viết.")}
            </div>
          )}

          {/* Actions */}
          <div className="flex items-center justify-end gap-3">
            <Link
              href="/admin/blog"
              className="rounded-lg border border-border px-4 py-2 text-sm font-medium transition-colors hover:bg-secondary"
            >
              Hủy
            </Link>
            <button
              type="submit"
              disabled={createMutation.isPending || !title.trim() || !content.trim()}
              className="inline-flex items-center gap-2 rounded-lg bg-[var(--accent)] px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-[var(--accent)]/90 disabled:opacity-50"
            >
              <Send className="h-4 w-4" />
              {createMutation.isPending ? "Đang tạo..." : "Tạo bài viết"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
