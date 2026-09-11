"use client";

import { useState } from "react";
import {
  AlertCircle,
  Loader2,
  RotateCcw,
  ScrollText,
  Search,
} from "lucide-react";

import { PaginationNumbered } from "@/components/ui/pagination-numbered";
import { DatePicker } from "@/components/ui/date-picker";
import { formatDateTime, ROLE_LABEL } from "@/features/user-role-management/lib/user-labels";
import { getApiErrorMessage } from "@/lib/api-client";
import type { Role } from "@/types/api.types";

import { useAuditLogs } from "../hooks/use-audit-logs";
import {
  AUDIT_CATEGORIES,
  type AuditCategory,
  type AuditLogEntry,
} from "../types/audit-logs.types";
import { AuditLogBadge } from "./audit-log-badge";

const PAGE_SIZE = 15;

export function AuditLogListView() {
  const [search, setSearch] = useState("");
  const [category, setCategory] = useState<AuditCategory>("ALL");
  const [fromDate, setFromDate] = useState<string>("");
  const [toDate, setToDate] = useState<string>("");
  const [page, setPage] = useState(1);

  const queryParams = {
    page,
    pageSize: PAGE_SIZE,
    search,
    action: category !== "ALL" ? category : undefined,
    fromDate: fromDate || undefined,
    toDate: toDate || undefined,
  };

  const { data, isLoading, isError, error, refetch, isFetching } = useAuditLogs(queryParams);

  const items = data?.items ?? [];
  const totalItems = data?.totalItems ?? 0;
  const totalPages = data?.totalPages ?? 0;

  const isFiltered = Boolean(search || category !== "ALL" || fromDate || toDate);

  const handleResetFilters = () => {
    setSearch("");
    setCategory("ALL");
    setFromDate("");
    setToDate("");
    setPage(1);
  };

  return (
    <div className="mx-auto w-full max-w-screen-2xl px-6 py-8">
      {/* ---- Header ---- */}
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="flex items-center gap-2.5 font-heading text-[32px] font-bold tracking-[-0.02em] text-foreground">
            <ScrollText className="size-8 text-accent" />
            Nhật ký hệ thống
          </h1>
          <p className="mt-1.5 text-[15px] text-muted-foreground">
            Theo dõi lịch sử thao tác, phân quyền tài khoản và các hoạt động trong hệ thống.
          </p>
        </div>

        {isFiltered && (
          <button
            type="button"
            onClick={handleResetFilters}
            className="flex h-10 items-center gap-1.5 rounded-full border border-border px-4 text-xs font-medium text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
          >
            <RotateCcw className="size-3.5" />
            Đặt lại bộ lọc
          </button>
        )}
      </div>

      {/* ---- Toolbar: Search & Filters ---- */}
      <div className="mt-8 flex flex-wrap items-center gap-3">
        {/* Realtime Debounced Search */}
        <div className="relative min-w-64 flex-1">
          <Search
            aria-hidden
            className="pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
          />
          <input
            type="text"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
            placeholder="Tìm theo người thực hiện, vai trò, hành động, chi tiết..."
            className="h-12 w-full rounded-full border border-border bg-background pl-11 pr-4 text-[15px] outline-none transition-colors focus:border-accent"
            data-testid="audit-search-input"
          />
        </div>

        {/* Category Multi-Filter Dropdown */}
        <select
          value={category}
          onChange={(e) => {
            setCategory(e.target.value as AuditCategory);
            setPage(1);
          }}
          className="h-12 rounded-full border border-border bg-background px-5 text-[15px] outline-none transition-colors focus:border-accent"
          data-testid="audit-category-select"
          aria-label="Lọc theo danh mục"
        >
          {AUDIT_CATEGORIES.map((cat) => (
            <option key={cat.value} value={cat.value}>
              {cat.label}
            </option>
          ))}
        </select>

        {/* Date Range: From Date & To Date */}
        <div className="flex items-center gap-2">
          <DatePicker
            value={fromDate}
            onChange={(val) => {
              const formatted = val ? (val instanceof Date ? val.toISOString() : String(val)) : "";
              setFromDate(formatted);
              setPage(1);
            }}
            placeholder="Từ ngày"
            className="h-12 w-[140px] rounded-full border border-border bg-background px-4 text-sm outline-none focus:border-accent"
          />
          <span className="text-muted-foreground">→</span>
          <DatePicker
            value={toDate}
            onChange={(val) => {
              const formatted = val ? (val instanceof Date ? val.toISOString() : String(val)) : "";
              setToDate(formatted);
              setPage(1);
            }}
            placeholder="Đến ngày"
            className="h-12 w-[140px] rounded-full border border-border bg-background px-4 text-sm outline-none focus:border-accent"
          />
        </div>
      </div>

      {/* ---- Error Alert ---- */}
      {isError && (
        <div
          role="alert"
          className="mt-6 flex items-center justify-between gap-3 rounded-2xl border border-destructive/20 bg-destructive/10 p-4 text-destructive"
        >
          <div className="flex items-center gap-2 text-sm font-medium">
            <AlertCircle className="size-4 shrink-0" />
            <span>{getApiErrorMessage(error, "Không thể tải danh sách nhật ký thao tác.")}</span>
          </div>
          <button
            type="button"
            onClick={() => refetch()}
            className="rounded-full bg-destructive/15 px-3 py-1 text-xs font-semibold hover:bg-destructive/25"
          >
            Thử lại
          </button>
        </div>
      )}

      {/* ---- Card Table Container ---- */}
      <div className="mt-6 overflow-x-auto rounded-3xl border border-border bg-background shadow-sm">
        <table
          className="w-full min-w-4xl border-collapse text-left text-sm"
          data-testid="audit-logs-table"
        >
          <thead>
            <tr className="border-b border-border bg-secondary/40 [&>th:first-child]:pl-6 [&>td:first-child]:pl-6">
              <th className="px-5 py-4 font-heading text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                Thời gian
              </th>
              <th className="px-5 py-4 font-heading text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                Người thực hiện
              </th>
              <th className="px-5 py-4 font-heading text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                Vai trò
              </th>
              <th className="px-5 py-4 font-heading text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                Hành động
              </th>
              <th className="px-5 py-4 font-heading text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                Chi tiết thao tác
              </th>
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr>
                <td colSpan={5} className="py-16 text-center">
                  <div className="flex items-center justify-center gap-2 text-sm text-muted-foreground">
                    <Loader2 className="size-5 animate-spin" />
                    <span>Đang tải nhật ký thao tác...</span>
                  </div>
                </td>
              </tr>
            )}

            {!isLoading && items.length === 0 && (
              <tr>
                <td colSpan={5} className="py-16 text-center text-muted-foreground">
                  <ScrollText className="mx-auto size-10 text-muted-foreground/30" />
                  <p className="mt-3 text-base font-medium text-foreground">Không có nhật ký nào</p>
                  <p className="mt-1 text-xs text-muted-foreground">
                    Không tìm thấy thao tác nào phù hợp với điều kiện tìm kiếm.
                  </p>
                </td>
              </tr>
            )}

            {!isLoading &&
              items.map((entry) => (
                <AuditLogRow key={entry.logId} entry={entry} />
              ))}
          </tbody>
        </table>
      </div>

      {/* ---- Pagination ---- */}
      <div className="mt-5 flex flex-wrap items-center justify-between gap-4 text-sm text-muted-foreground">
        <div className="flex items-center gap-2">
          <span>
            Đang xem {items.length} / {totalItems} kết quả
          </span>
          {isFetching && !isLoading && (
            <Loader2 className="size-3.5 animate-spin text-muted-foreground" />
          )}
        </div>

        {totalPages > 1 && (
          <PaginationNumbered
            currentPage={page}
            totalPages={totalPages}
            setPage={(p) => setPage(p)}
          />
        )}
      </div>
    </div>
  );
}

function AuditLogRow({ entry }: { entry: AuditLogEntry }) {
  const actorDisplay = entry.actorName || "Hệ thống";
  const roleDisplay =
    ROLE_LABEL[entry.actorRole as Role] || entry.actorRole || "—";

  return (
    <tr
      className="border-b border-border last:border-0 transition-colors hover:bg-muted/30 [&>th:first-child]:pl-6 [&>td:first-child]:pl-6"
      data-testid="audit-log-row"
    >
      {/* Thời gian */}
      <td className="px-5 py-4 whitespace-nowrap text-xs text-muted-foreground font-mono">
        {formatDateTime(entry.performedAt)}
      </td>

      {/* Người thực hiện */}
      <td className="px-5 py-4 font-medium text-foreground">
        <div className="flex flex-col">
          <span>{actorDisplay}</span>
          {entry.actorId && (
            <span className="text-[11px] text-muted-foreground font-mono truncate max-w-[140px]">
              {entry.actorId}
            </span>
          )}
        </div>
      </td>

      {/* Vai trò */}
      <td className="px-5 py-4 whitespace-nowrap">
        <span className="inline-flex rounded-full bg-secondary px-2.5 py-0.5 text-xs font-medium text-secondary-foreground">
          {roleDisplay}
        </span>
      </td>

      {/* Hành động */}
      <td className="px-5 py-4 whitespace-nowrap">
        <AuditLogBadge action={entry.action} />
      </td>

      {/* Chi tiết thao tác */}
      <td className="px-5 py-4 text-xs text-foreground/80 max-w-md">
        {entry.detail ? (
          <span className="block truncate" title={entry.detail}>
            {entry.detail}
          </span>
        ) : (
          <span className="text-muted-foreground italic">— Không có chi tiết —</span>
        )}
      </td>
    </tr>
  );
}
