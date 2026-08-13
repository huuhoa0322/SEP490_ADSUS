"use client";

import { Edit, Eye, FileText, Loader2, Plus, X } from "lucide-react";
import Link from "next/link";
import { useEffect, useState } from "react";

import { getApiErrorMessage } from "@/lib/api-client";

import {
  useAdminBlogPost,
  useAdminBlogPosts,
  usePublishBlogPost,
  useUpdateBlogPost,
} from "../hooks/use-admin-blog";
import type {
  AdminBlogPostDetailResponse,
  AdminBlogPostListItemResponse,
  BlogStatus,
} from "../types/blog.types";

const STATUS_LABELS: Record<BlogStatus, string> = {
  DRAFT: "Bản nháp",
  PUBLISHED: "Đã xuất bản",
};

const STATUS_STYLES: Record<BlogStatus, string> = {
  DRAFT: "bg-amber-100 text-amber-800",
  PUBLISHED: "bg-green-100 text-green-800",
};

/**
 * Admin Blog List View - requires ADMIN role.
 * SCR-27 - Quản lý Blog Y khoa
 * View / Edit dùng Modal popup thay vì chuyển trang.
 */
export function AdminBlogListView() {
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState<BlogStatus | undefined>(undefined);
  const [modalPostId, setModalPostId] = useState<string | null>(null);

  const { data, isLoading, isError, error } = useAdminBlogPosts({
    page,
    pageSize: 10,
    status: statusFilter,
  });

  return (
    <div className="min-h-screen bg-[var(--muted)]">
      {/* Header */}
      <div className="bg-white border-b border-border">
        <div className="mx-auto w-full max-w-screen-2xl px-6 py-6">
          <div className="flex items-center justify-between">
            <div>
              <h1 className="font-heading text-2xl font-bold text-[var(--primary)]">
                Quản lý Bài viết Y khoa
              </h1>
              <p className="mt-1 text-sm text-[var(--muted-foreground)]">
                Tạo, biên tập và duyệt đăng các bài viết truyền thông y tế
              </p>
            </div>
            <Link
              href="/admin/blog/new"
              className="inline-flex items-center gap-2 rounded-lg bg-[var(--accent)] px-4 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-[var(--accent)]/90"
            >
              <Plus className="h-4 w-4" />
              Tạo bài viết mới
            </Link>
          </div>
        </div>
      </div>

      {/* Content */}
      <div className="mx-auto w-full max-w-screen-2xl px-6 py-6">
        {/* Filters */}
        <div className="mb-6 flex items-center gap-2">
          <FilterButton active={statusFilter === undefined} onClick={() => { setStatusFilter(undefined); setPage(1); }}>
            Tất cả
          </FilterButton>
          <FilterButton active={statusFilter === "DRAFT"} onClick={() => { setStatusFilter("DRAFT"); setPage(1); }}>
            Bản nháp
          </FilterButton>
          <FilterButton active={statusFilter === "PUBLISHED"} onClick={() => { setStatusFilter("PUBLISHED"); setPage(1); }}>
            Đã xuất bản
          </FilterButton>
        </div>

        {/* Error */}
        {isError && (
          <div role="alert" className="mb-6 flex items-start gap-2.5 rounded-xl border border-destructive/25 bg-destructive/5 px-4 py-3 text-sm text-destructive">
            {getApiErrorMessage(error, "Không tải được danh sách bài viết.")}
          </div>
        )}

        {/* Loading */}
        {isLoading && !data && (
          <div className="flex min-h-64 items-center justify-center">
            <Loader2 className="h-8 w-8 animate-spin text-[var(--muted-foreground)]" />
          </div>
        )}

        {/* Table */}
        {data && (
          <div className="overflow-hidden rounded-xl border border-border bg-white">
            <table className="w-full table-fixed">
              <colgroup>
                <col className="w-[36%]" />
                <col className="w-[14%]" />
                <col className="w-[22%]" />
                <col className="w-[14%]" />
                <col className="w-[14%]" />
              </colgroup>
              <thead>
                <tr className="border-b border-border bg-[var(--muted)]">
                  <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-[var(--muted-foreground)]">Tiêu đề</th>
                  <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-[var(--muted-foreground)]">Trạng thái</th>
                  <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-[var(--muted-foreground)]">Tác giả</th>
                  <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-[var(--muted-foreground)]">Ngày tạo</th>
                  <th className="px-4 py-3 text-center text-xs font-semibold uppercase tracking-wide text-[var(--muted-foreground)]">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {data.items.map((post) => (
                  <Row key={post.id} post={post} onAction={(mode) => setModalPostId(`${post.id}:${mode}`)} />
                ))}
              </tbody>
            </table>

            {data.items.length === 0 && (
              <div className="py-16 text-center">
                <FileText className="mx-auto mb-3 h-12 w-12 text-[var(--muted-foreground)]" />
                <p className="text-[var(--muted-foreground)]">Chưa có bài viết nào.</p>
              </div>
            )}
          </div>
        )}

        {/* Pagination */}
        {data && data.totalPages > 1 && (
          <div className="mt-6 flex items-center justify-center gap-2">
            <button onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page === 1} className="rounded-lg border border-border px-4 py-2 text-sm font-medium transition-colors hover:bg-secondary disabled:cursor-not-allowed disabled:opacity-50">Trước</button>
            <span className="px-4 text-sm text-[var(--muted-foreground)]">Trang {page} / {data.totalPages}</span>
            <button onClick={() => setPage((p) => Math.min(data.totalPages, p + 1))} disabled={page === data.totalPages} className="rounded-lg border border-border px-4 py-2 text-sm font-medium transition-colors hover:bg-secondary disabled:cursor-not-allowed disabled:opacity-50">Sau</button>
          </div>
        )}
      </div>

      {/* Modal */}
      {modalPostId && (
        <BlogPostModal postId={modalPostId.split(":")[0]} initialMode={(modalPostId.split(":")[1] as "view" | "edit") ?? "view"} onClose={() => setModalPostId(null)} />
      )}
    </div>
  );
}

function FilterButton({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button onClick={onClick} className={`rounded-full px-4 py-1.5 text-sm font-medium transition-colors ${active ? "bg-[var(--accent)] text-white" : "bg-white border border-border text-[var(--muted-foreground)] hover:bg-secondary"}`}>
      {children}
    </button>
  );
}

function Row({ post, onAction }: { post: AdminBlogPostListItemResponse; onAction: (mode: "view" | "edit") => void }) {
  return (
    <tr className="hover:bg-[var(--muted)]/50">
      <td className="overflow-hidden px-4 py-4">
        <div className="truncate font-medium text-[var(--primary)]">{post.title}</div>
        <div className="truncate text-xs text-[var(--muted-foreground)]">
          {post.status === "PUBLISHED" && post.publishedAt
            ? `Xuất bản: ${new Date(post.publishedAt).toLocaleDateString("vi-VN")}`
            : `Tạo: ${new Date(post.createdAt).toLocaleDateString("vi-VN")}`}
        </div>
      </td>
      <td className="px-4 py-4">
        <span className={`inline-block rounded-full px-2.5 py-1 text-xs font-semibold ${STATUS_STYLES[post.status]}`}>
          {STATUS_LABELS[post.status]}
        </span>
      </td>
      <td className="overflow-hidden px-4 py-4 text-sm text-[var(--muted-foreground)]">
        <div className="truncate">{post.authorName}</div>
      </td>
      <td className="px-4 py-4 font-mono text-xs text-[var(--muted-foreground)]">
        {new Date(post.createdAt).toLocaleDateString("vi-VN")}
      </td>
      <td className="px-4 py-4">
        <div className="flex items-center justify-center gap-2">
          <button onClick={() => onAction("view")} className="inline-flex items-center gap-1 rounded-lg border border-[var(--accent)] px-3 py-1.5 text-xs font-medium text-[var(--accent)] transition-colors hover:bg-[var(--accent)]/10">
            <Eye className="h-3.5 w-3.5" />
            Xem
          </button>
          {post.status === "DRAFT" && (
            <button onClick={() => onAction("edit")} className="inline-flex items-center gap-1 rounded-lg border border-[var(--accent)] px-3 py-1.5 text-xs font-medium text-[var(--accent)] transition-colors hover:bg-[var(--accent)]/10">
              <Edit className="h-3.5 w-3.5" />
              Sửa
            </button>
          )}
        </div>
      </td>
    </tr>
  );
}

/**
 * Modal hiển thị chi tiết / chỉnh sửa bài viết.
 * Mode "view" → chỉ đọc, có nút "Xuất bản" nếu là Draft.
 * Mode "edit" → form chỉnh sửa title + content.
 */
function BlogPostModal({ postId, initialMode, onClose }: { postId: string; initialMode: "view" | "edit"; onClose: () => void }) {
  const { data: post, isLoading } = useAdminBlogPost(postId);
  const updateMutation = useUpdateBlogPost();
  const publishMutation = usePublishBlogPost();

  const [mode, setMode] = useState<"view" | "edit">(initialMode);
  const [title, setTitle] = useState("");
  const [content, setContent] = useState("");

  useEffect(() => {
    if (post) {
      setTitle(post.title);
      setContent(post.content);
    }
  }, [post]);

  // ESC đóng modal
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [onClose]);

  const handleSave = async () => {
    if (!post) return;
    await updateMutation.mutateAsync({ id: post.id, payload: { title, content } });
    setMode("view");
  };

  const handlePublish = async () => {
    if (!post) return;
    await publishMutation.mutateAsync(post.id);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4" onClick={onClose}>
      <div className="relative max-h-[90vh] w-full max-w-3xl overflow-y-auto rounded-xl bg-white shadow-2xl" onClick={(e) => e.stopPropagation()}>
        {/* Modal Header */}
        <div className="sticky top-0 z-10 flex items-center justify-between border-b border-border bg-white px-6 py-4">
          <div className="flex items-center gap-3">
            <h2 className="font-heading text-lg font-bold text-[var(--primary)]">
              {mode === "edit" ? "Chỉnh sửa bài viết" : "Chi tiết bài viết"}
            </h2>
            {post && (
              <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${STATUS_STYLES[post.status]}`}>
                {STATUS_LABELS[post.status]}
              </span>
            )}
          </div>
          <button onClick={onClose} className="rounded-lg p-2 text-[var(--muted-foreground)] transition-colors hover:bg-secondary">
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Modal Body */}
        <div className="p-6">
          {isLoading && (
            <div className="flex min-h-48 items-center justify-center">
              <Loader2 className="h-8 w-8 animate-spin text-[var(--muted-foreground)]" />
            </div>
          )}

          {post && (
            <div className="space-y-4">
              {/* Author info */}
              <div className="flex items-center gap-4 text-sm text-[var(--muted-foreground)]">
                <span className="font-medium">{post.authorName}</span>
                <span>·</span>
                <span className="font-mono text-xs">{new Date(post.createdAt).toLocaleString("vi-VN")}</span>
              </div>

              {/* Title */}
              {mode === "edit" ? (
                <input
                  type="text"
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                  className="w-full rounded-lg border border-border px-4 py-2.5 text-xl font-bold focus:border-[var(--accent)] focus:outline-none"
                  placeholder="Tiêu đề bài viết"
                />
              ) : (
                <h1 className="font-heading text-2xl font-bold text-[var(--primary)]">{post.title}</h1>
              )}

              {/* Content */}
              {mode === "edit" ? (
                <textarea
                  value={content}
                  onChange={(e) => setContent(e.target.value)}
                  rows={12}
                  className="w-full rounded-lg border border-border px-4 py-3 font-mono text-sm focus:border-[var(--accent)] focus:outline-none"
                  placeholder="Nội dung bài viết (Markdown)"
                />
              ) : (
                <div className="max-h-96 overflow-y-auto whitespace-pre-wrap rounded-lg border border-border bg-[var(--muted)] p-4 text-sm leading-relaxed">
                  {post.content}
                </div>
              )}

              {/* Error / Success */}
              {updateMutation.isError && (
                <div className="rounded-lg border border-destructive/25 bg-destructive/5 p-3 text-sm text-destructive">
                  {getApiErrorMessage(updateMutation.error, "Không lưu được bài viết.")}
                </div>
              )}
              {publishMutation.isError && (
                <div className="rounded-lg border border-destructive/25 bg-destructive/5 p-3 text-sm text-destructive">
                  {getApiErrorMessage(publishMutation.error, "Không xuất bản được bài viết.")}
                </div>
              )}
            </div>
          )}
        </div>

        {/* Modal Footer */}
        {post && (
          <div className="sticky bottom-0 flex items-center justify-end gap-3 border-t border-border bg-white px-6 py-4">
            {mode === "edit" ? (
              <>
                <button
                  onClick={() => {
                    setTitle(post.title);
                    setContent(post.content);
                    setMode("view");
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
              </>
            ) : (
              <>
                {post.status === "DRAFT" && (
                  <button
                    onClick={handlePublish}
                    disabled={publishMutation.isPending}
                    className="rounded-lg border border-[var(--accent)] px-4 py-2 text-sm font-medium text-[var(--accent)] transition-colors hover:bg-[var(--accent)]/10 disabled:opacity-50"
                  >
                    {publishMutation.isPending ? "Đang xuất bản..." : "Xuất bản"}
                  </button>
                )}
                {post.status === "DRAFT" && (
                  <button
                    onClick={() => setMode("edit")}
                    className="rounded-lg bg-[var(--accent)] px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-[var(--accent)]/90"
                  >
                    Chỉnh sửa
                  </button>
                )}
                <button onClick={onClose} className="rounded-lg border border-border px-4 py-2 text-sm font-medium transition-colors hover:bg-secondary">
                  Đóng
                </button>
              </>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

// Use AdminBlogPostDetailResponse indirectly via useAdminBlogPost's return type
type _Detail = AdminBlogPostDetailResponse;