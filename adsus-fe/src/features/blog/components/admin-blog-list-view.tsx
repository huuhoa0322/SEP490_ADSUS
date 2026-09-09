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
  AdminBlogPostListItemResponse,
  BlogStatus,
} from "../types/blog.types";
import { PaginationNumbered } from "@/components/ui/pagination-numbered";

const STATUS_LABELS: Record<BlogStatus, string> = {
  DRAFT: "Bản nháp",
  PUBLISHED: "Đã xuất bản",
};

const STATUS_STYLES: Record<BlogStatus, string> = {
  DRAFT: "bg-[var(--status-warning)]/12 text-[var(--status-warning)]",
  PUBLISHED: "bg-[var(--status-good)]/12 text-[var(--status-good)]",
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
    <div className="mx-auto w-full max-w-screen-2xl px-6 py-8">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-heading text-[32px] font-bold tracking-[-0.02em] text-foreground">
            Bài viết y khoa
          </h1>
          <p className="mt-1.5 text-[15px] text-muted-foreground">
            Tạo, biên tập và duyệt đăng các bài viết truyền thông y tế.
          </p>
        </div>
        <Link
          href="/admin/blog/new"
          className="flex h-12 items-center gap-2 rounded-full bg-accent px-6 font-heading text-sm font-600 uppercase tracking-wider text-accent-foreground shadow-lg shadow-accent/25 transition-all hover:bg-accent/90"
        >
          <Plus className="size-4" />
          Tạo bài viết mới
        </Link>
      </div>

      {/* Filters */}
      <div className="mt-8 flex items-center gap-2">
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
        <div role="alert" className="mt-6 flex items-start gap-2.5 rounded-2xl border border-destructive/25 bg-destructive/5 px-4 py-3 text-sm text-destructive">
          {getApiErrorMessage(error, "Không tải được danh sách bài viết.")}
        </div>
      )}

      {/* Loading */}
      {isLoading && !data && (
        <div className="flex min-h-64 items-center justify-center">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        </div>
      )}

      {/* Table */}
      {data && (
        <div className="mt-6 overflow-hidden overflow-x-auto rounded-3xl border border-border bg-background">
          <table className="w-full table-fixed text-sm">
            <colgroup>
              <col className="w-[36%]" />
              <col className="w-[14%]" />
              <col className="w-[22%]" />
              <col className="w-[14%]" />
              <col className="w-[14%]" />
            </colgroup>
            <thead>
              <tr className="border-b border-border bg-secondary/40">
                <th className="px-5 py-3.5 text-left font-600 text-muted-foreground">Tiêu đề</th>
                <th className="px-5 py-3.5 text-left font-600 text-muted-foreground">Trạng thái</th>
                <th className="px-5 py-3.5 text-left font-600 text-muted-foreground">Tác giả</th>
                <th className="px-5 py-3.5 text-left font-600 text-muted-foreground">Ngày tạo</th>
                <th className="px-5 py-3.5 text-center font-600 text-muted-foreground">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((post) => (
                <Row key={post.id} post={post} onAction={(mode) => setModalPostId(`${post.id}:${mode}`)} />
              ))}
            </tbody>
          </table>

          {data.items.length === 0 && (
            <div className="py-16 text-center">
              <FileText className="mx-auto mb-3 h-12 w-12 text-muted-foreground/40" />
              <p className="text-muted-foreground">Chưa có bài viết nào.</p>
            </div>
          )}
        </div>
      )}

      {/* Pagination */}
      {data && data.totalPages > 1 && (
        <PaginationNumbered
          currentPage={page}
          totalPages={data.totalPages}
          setPage={setPage}
          className="mt-6 justify-center"
        />
      )}

      {/* Modal */}
      {modalPostId && (
        <BlogPostModal postId={modalPostId.split(":")[0]} initialMode={(modalPostId.split(":")[1] as "view" | "edit") ?? "view"} onClose={() => setModalPostId(null)} />
      )}
    </div>
  );
}

function FilterButton({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button onClick={onClick} className={`h-9 rounded-full px-4 text-sm font-medium transition-colors border ${active ? "border-accent bg-accent text-accent-foreground shadow-sm" : "border-border text-muted-foreground hover:bg-secondary"}`}>
      {children}
    </button>
  );
}

function Row({ post, onAction }: { post: AdminBlogPostListItemResponse; onAction: (mode: "view" | "edit") => void }) {
  return (
    <tr className="border-b border-border last:border-0 hover:bg-secondary/20">
      <td className="overflow-hidden px-5 py-4">
        <div className="truncate font-600 text-foreground">{post.title}</div>
        <div className="truncate text-xs text-muted-foreground">
          {post.status === "PUBLISHED" && post.publishedAt
            ? `Xuất bản: ${new Date(post.publishedAt).toLocaleDateString("vi-VN")}`
            : `Tạo: ${new Date(post.createdAt).toLocaleDateString("vi-VN")}`}
        </div>
      </td>
      <td className="px-5 py-4">
        <span className={`inline-block rounded-full px-3 py-1 text-xs font-600 ${STATUS_STYLES[post.status]}`}>
          {STATUS_LABELS[post.status]}
        </span>
      </td>
      <td className="overflow-hidden px-5 py-4 text-sm text-muted-foreground">
        <div className="truncate">{post.authorName}</div>
      </td>
      <td className="px-5 py-4 font-mono text-xs text-muted-foreground">
        {new Date(post.createdAt).toLocaleDateString("vi-VN")}
      </td>
      <td className="px-5 py-4">
        <div className="flex items-center justify-center gap-1">
          <button onClick={() => onAction("view")} title="Xem chi tiết" className="flex size-9 items-center justify-center rounded-full text-muted-foreground hover:bg-secondary hover:text-foreground">
            <Eye className="size-4" />
          </button>
          {post.status === "DRAFT" && (
            <button onClick={() => onAction("edit")} title="Chỉnh sửa" className="flex size-9 items-center justify-center rounded-full text-primary hover:bg-primary/10">
              <Edit className="size-4" />
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
      // eslint-disable-next-line react-hooks/set-state-in-effect -- syncs the form once from the async-fetched edit target, not a render-time derivation
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
    <div role="presentation" className="fixed inset-0 z-50 flex items-center justify-center bg-foreground/40 p-4 backdrop-blur-sm" onClick={onClose}>
      <div role="presentation" className="relative flex max-h-[90vh] w-full max-w-3xl flex-col overflow-hidden rounded-3xl bg-background shadow-2xl" onClick={(e) => e.stopPropagation()}>
        {/* Modal Header */}
        <div className="flex shrink-0 items-center justify-between border-b border-border px-6 py-4">
          <div className="flex items-center gap-3">
            <h2 className="font-heading text-lg font-bold text-foreground">
              {mode === "edit" ? "Chỉnh sửa bài viết" : "Chi tiết bài viết"}
            </h2>
            {post && (
              <span className={`rounded-full px-3 py-1 text-xs font-600 ${STATUS_STYLES[post.status]}`}>
                {STATUS_LABELS[post.status]}
              </span>
            )}
          </div>
          <button onClick={onClose} aria-label="Đóng" className="rounded-full p-2 text-muted-foreground transition-colors hover:bg-secondary">
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Modal Body */}
        <div className="overflow-y-auto p-6">
          {isLoading && (
            <div className="flex min-h-48 items-center justify-center">
              <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
            </div>
          )}

          {post && (
            <div className="space-y-4">
              {/* Author info */}
              <div className="flex items-center gap-4 text-sm text-muted-foreground">
                <span className="font-medium text-foreground">{post.authorName}</span>
                <span>·</span>
                <span className="font-mono text-xs">{new Date(post.createdAt).toLocaleString("vi-VN")}</span>
              </div>

              {/* Title */}
              {mode === "edit" ? (
                <input
                  type="text"
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                  className="w-full rounded-xl border border-border bg-background px-4 py-2.5 text-xl font-bold outline-none focus:border-accent"
                  placeholder="Tiêu đề bài viết"
                />
              ) : (
                <h1 className="font-heading text-2xl font-bold text-foreground">{post.title}</h1>
              )}

              {/* Content */}
              {mode === "edit" ? (
                <textarea
                  value={content}
                  onChange={(e) => setContent(e.target.value)}
                  rows={12}
                  className="w-full rounded-xl border border-border bg-background px-4 py-3 font-mono text-sm outline-none focus:border-accent"
                  placeholder="Nội dung bài viết (Markdown)"
                />
              ) : (
                <div className="max-h-96 overflow-y-auto whitespace-pre-wrap rounded-xl border border-border bg-secondary/30 p-4 text-sm leading-relaxed text-foreground">
                  {post.content}
                </div>
              )}

              {/* Error / Success */}
              {updateMutation.isError && (
                <div className="rounded-xl border border-destructive/25 bg-destructive/5 p-3 text-sm text-destructive">
                  {getApiErrorMessage(updateMutation.error, "Không lưu được bài viết.")}
                </div>
              )}
              {publishMutation.isError && (
                <div className="rounded-xl border border-destructive/25 bg-destructive/5 p-3 text-sm text-destructive">
                  {getApiErrorMessage(publishMutation.error, "Không xuất bản được bài viết.")}
                </div>
              )}
            </div>
          )}
        </div>

        {/* Modal Footer */}
        {post && (
          <div className="flex shrink-0 items-center justify-end gap-3 border-t border-border px-6 py-4">
            {mode === "edit" ? (
              <>
                <button
                  onClick={() => {
                    setTitle(post.title);
                    setContent(post.content);
                    setMode("view");
                  }}
                  className="rounded-full border border-border px-5 py-2.5 text-sm font-600 text-muted-foreground transition-colors hover:bg-secondary"
                >
                  Hủy
                </button>
                <button
                  onClick={handleSave}
                  disabled={updateMutation.isPending}
                  className="rounded-full bg-accent px-5 py-2.5 text-sm font-600 text-accent-foreground transition-colors hover:bg-accent/90 disabled:opacity-50"
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
                    className="rounded-full border border-accent px-5 py-2.5 text-sm font-600 text-accent transition-colors hover:bg-accent/10 disabled:opacity-50"
                  >
                    {publishMutation.isPending ? "Đang xuất bản..." : "Xuất bản"}
                  </button>
                )}
                {post.status === "DRAFT" && (
                  <button
                    onClick={() => setMode("edit")}
                    className="rounded-full bg-accent px-5 py-2.5 text-sm font-600 text-accent-foreground transition-colors hover:bg-accent/90"
                  >
                    Chỉnh sửa
                  </button>
                )}
                <button onClick={onClose} className="rounded-full border border-border px-5 py-2.5 text-sm font-600 text-muted-foreground transition-colors hover:bg-secondary">
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
