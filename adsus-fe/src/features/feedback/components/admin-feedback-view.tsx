"use client";

import {
  AlertCircle,
  ExternalLink,
  Loader2,
  RefreshCw,
  Search,
  Star,
} from "lucide-react";
import Link from "next/link";
import { useEffect, useState } from "react";

import { PaginationNumbered } from "@/components/ui/pagination-numbered";
import { getApiErrorMessage } from "@/lib/api-client";
import { cn } from "@/lib/utils";

import { useAdminFeedbacks } from "../hooks/use-feedback";
import type { AdminFeedbackItem } from "../types/feedback.types";

const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";

function normalizeRating(raw: number): number {
  if (Number.isNaN(raw)) return 1;
  const rounded = Math.round(raw);
  return Math.min(5, Math.max(1, rounded));
}

function formatDateTime(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return "—";
  return date.toLocaleString("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function StarRating({ rating }: { rating: number }) {
  const normalized = normalizeRating(rating);

  return (
    <div className="flex items-center gap-1.5" aria-label={`${normalized} trên 5 sao`}>
      <div className="flex items-center">
        {[1, 2, 3, 4, 5].map((star) => (
          <Star
            key={star}
            aria-hidden="true"
            className={cn(
              "size-4",
              star <= normalized
                ? "fill-amber-400 text-amber-400"
                : "fill-muted/20 text-muted-foreground/30"
            )}
          />
        ))}
      </div>
      <span className="text-xs font-semibold text-muted-foreground">
        ({normalized}/5)
      </span>
    </div>
  );
}

function Th({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <th
      className={cn(
        "px-5 py-4 font-heading text-xs font-semibold uppercase tracking-wider text-muted-foreground",
        className
      )}
    >
      {children}
    </th>
  );
}

export function AdminFeedbackView() {
  const [searchInput, setSearchInput] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [ratingFilter, setRatingFilter] = useState<number | undefined>(undefined);
  const [page, setPage] = useState(1);
  const pageSize = 15;

  // Realtime search with 300ms debounce
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(searchInput);
      setPage(1);
    }, 300);
    return () => clearTimeout(timer);
  }, [searchInput]);

  const { data, isLoading, isError, error, refetch, isFetching } = useAdminFeedbacks({
    page,
    pageSize,
    search: debouncedSearch,
    minRating: ratingFilter,
  });

  const items = data?.items ?? [];
  const totalItems = data?.totalItems ?? (data as unknown as { totalCount?: number })?.totalCount ?? items.length;
  const totalPages = data?.totalPages ?? (totalItems === 0 ? 0 : Math.ceil(totalItems / pageSize));

  return (
    <div className="mx-auto w-full max-w-screen-2xl px-6 py-8">
      {/* Page Header */}
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-heading text-[32px] font-bold tracking-[-0.02em] text-foreground">
            Quản lý phản hồi dịch vụ
          </h1>
          <p className="mt-1.5 text-[15px] text-muted-foreground">
            Theo dõi, đánh giá chất lượng phục vụ và ý kiến đóng góp từ bệnh nhân.
          </p>
        </div>

        <button
          type="button"
          onClick={() => refetch()}
          disabled={isFetching}
          className="flex h-11 items-center gap-2 rounded-full border border-border bg-background px-5 text-sm font-medium text-foreground transition-colors hover:bg-secondary disabled:opacity-50"
        >
          <RefreshCw className={cn("size-4", isFetching && "animate-spin")} />
          Làm mới
        </button>
      </div>

      {/* Toolbar: Search & Rating Filter */}
      <div className="mt-8 flex flex-wrap gap-3">
        <div className="relative min-w-64 flex-1">
          <Search
            aria-hidden="true"
            className="pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
          />
          <input
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            placeholder="Tìm theo tên bệnh nhân, bác sĩ hoặc mã ca khám..."
            aria-label="Tìm kiếm phản hồi"
            className="h-12 w-full rounded-full border border-border bg-background pl-11 pr-4 text-[15px] outline-none transition-colors focus:border-accent"
          />
        </div>

        <select
          value={ratingFilter ?? ""}
          onChange={(e) => {
            const val = e.target.value ? Number(e.target.value) : undefined;
            setRatingFilter(val);
            setPage(1);
          }}
          aria-label="Lọc theo đánh giá"
          className="h-12 rounded-full border border-border bg-background px-5 text-[15px] outline-none focus:border-accent"
        >
          <option value="">Tất cả đánh giá</option>
          <option value="5">5 sao</option>
          <option value="4">4 sao</option>
          <option value="3">3 sao</option>
          <option value="2">2 sao</option>
          <option value="1">1 sao</option>
        </select>
      </div>

      {/* Error state */}
      {isError && (
        <div
          role="alert"
          className="mt-5 flex items-start gap-2.5 rounded-2xl border border-destructive/25 bg-destructive/5 px-4 py-3 text-sm text-destructive"
        >
          <AlertCircle aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
          <div className="flex-1">
            <span>
              {getApiErrorMessage(
                error,
                error instanceof Error && error.message
                  ? error.message
                  : "Không thể tải danh sách phản hồi."
              )}
            </span>
            <button
              type="button"
              onClick={() => refetch()}
              className="ml-3 underline font-medium hover:opacity-80"
            >
              Thử lại
            </button>
          </div>
        </div>
      )}

      {/* Card table container */}
      <div className="mt-6 overflow-x-auto rounded-3xl border border-border bg-background shadow-sm">
        <table className="w-full min-w-4xl border-collapse text-left text-sm">
          <thead>
            <tr className="border-b border-border bg-secondary/40 [&>th:first-child]:pl-6 [&>td:first-child]:pl-6">
              <Th>Mã ca khám / Chi tiết</Th>
              <Th>Bác sĩ phụ trách</Th>
              <Th>Bệnh nhân</Th>
              <Th>Đánh giá</Th>
              <Th>Nội dung</Th>
              <Th>Thời gian gửi</Th>
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr>
                <td colSpan={6} className="px-5 py-14 text-center text-muted-foreground">
                  <Loader2 className="mx-auto size-5 animate-spin" />
                  <span className="mt-2 block text-xs">Đang tải dữ liệu phản hồi...</span>
                </td>
              </tr>
            )}

            {!isLoading && items.length === 0 && (
              <tr>
                <td colSpan={6} className="px-5 py-14 text-center text-muted-foreground">
                  Không tìm thấy phản hồi nào phù hợp.
                </td>
              </tr>
            )}

            {!isLoading &&
              items.map((item: AdminFeedbackItem) => {
                const hasValidCase =
                  Boolean(item.caseId) &&
                  item.caseId !== EMPTY_GUID &&
                  item.caseId.trim() !== "";

                return (
                  <tr
                    key={item.id}
                    className="border-b border-border last:border-0 hover:bg-muted/30 transition-colors [&>th:first-child]:pl-6 [&>td:first-child]:pl-6"
                  >
                    {/* Mã ca khám / Chi tiết */}
                    <td className="px-5 py-4">
                      {hasValidCase ? (
                        <Link
                          href={`/cases/${item.caseId}`}
                          className="inline-flex items-center gap-1 font-mono text-xs font-semibold text-accent hover:underline"
                          title="Xem chi tiết ca khám"
                        >
                          <span>
                            {item.caseId.length > 12
                              ? `${item.caseId.substring(0, 8)}...`
                              : item.caseId}
                          </span>
                          <ExternalLink className="size-3" />
                        </Link>
                      ) : (
                        <span className="text-muted-foreground italic text-xs">
                          Phản hồi chung
                        </span>
                      )}
                    </td>

                    {/* Bác sĩ phụ trách */}
                    <td className="px-5 py-4 font-medium text-foreground">
                      {item.doctorId ? (
                        <Link
                          href={`/admin/users/${item.doctorId}`}
                          className="hover:text-accent hover:underline"
                          title="Xem thông tin tài khoản bác sĩ"
                        >
                          {item.doctorName || "Bác sĩ"}
                        </Link>
                      ) : (
                        <span className="text-muted-foreground italic">
                          {item.doctorName || "Không xác định"}
                        </span>
                      )}
                    </td>

                    {/* Bệnh nhân */}
                    <td className="px-5 py-4">
                      {item.patientProfileId ? (
                        <Link
                          href={`/patients/${item.patientProfileId}`}
                          className="font-medium text-foreground hover:text-accent hover:underline"
                          title="Xem hồ sơ bệnh nhân"
                        >
                          {item.patientName || "Bệnh nhân"}
                        </Link>
                      ) : (
                        <span className="font-medium text-foreground">
                          {item.patientName || "Khách vãng lai"}
                        </span>
                      )}
                      <div className="text-xs text-muted-foreground tabular-nums">
                        {item.patientPhone || "Chưa có SĐT"}
                      </div>
                    </td>

                    {/* Đánh giá */}
                    <td className="px-5 py-4">
                      <StarRating rating={item.rating} />
                    </td>

                    {/* Nội dung */}
                    <td className="px-5 py-4">
                      {item.content ? (
                        <div
                          className="max-w-md text-sm text-foreground/90 break-words line-clamp-3"
                          title={item.content}
                        >
                          {item.content}
                        </div>
                      ) : (
                        <span className="italic text-muted-foreground text-xs">
                          Không có nhận xét
                        </span>
                      )}
                    </td>

                    {/* Thời gian gửi */}
                    <td className="px-5 py-4 text-muted-foreground text-xs whitespace-nowrap">
                      {formatDateTime(item.submittedAt)}
                    </td>
                  </tr>
                );
              })}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      {data && (
        <div className="mt-5 flex flex-wrap items-center justify-between gap-4 text-sm text-muted-foreground">
          <span>
            Đang xem {items.length} / {totalItems} kết quả
          </span>
          {totalPages > 1 && (
            <PaginationNumbered
              currentPage={page}
              totalPages={totalPages}
              setPage={setPage}
            />
          )}
        </div>
      )}
    </div>
  );
}
