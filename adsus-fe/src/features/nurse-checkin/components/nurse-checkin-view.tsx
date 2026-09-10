"use client";

import { useState, useEffect } from "react";
import { format } from "date-fns";
import { vi } from "date-fns/locale";
import { Search, RefreshCw, Check, Clock } from "lucide-react";
import { DatePicker } from "@/components/ui/date-picker";
import { PaginationNumbered } from "@/components/ui/pagination-numbered";
import { cn } from "@/lib/utils";
import { useCheckinQueue } from "../hooks/use-checkin-queue";
import { useCheckin } from "../hooks/use-checkin";
import { CheckinQueueTable } from "./checkin-queue-table";
import { AppointmentDetailModal } from "./appointment-detail-modal";
import type { CheckinQueueItem } from "../types/checkin.types";

function getTodayStr(): string {
  return format(new Date(), "yyyy-MM-dd");
}

function getPastDateStr(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() - (days - 1));
  return format(d, "yyyy-MM-dd");
}

export function NurseCheckinView() {
  const todayStr = getTodayStr();
  const [fromDate, setFromDate] = useState(todayStr);
  const [toDate, setToDate] = useState(todayStr);
  const [status, setStatus] = useState("ALL");
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [page, setPage] = useState(1);
  const [selectedItem, setSelectedItem] = useState<CheckinQueueItem | null>(null);
  const pageSize = 15;

  // Realtime search with 300ms debounce
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(search);
      setPage(1);
    }, 300);
    return () => clearTimeout(timer);
  }, [search]);

  const { data, isLoading, refetch, isRefetching } = useCheckinQueue({
    fromDate,
    toDate,
    status: status === "ALL" ? "ALL" : status,
    search: debouncedSearch.trim() || undefined,
    page,
    pageSize,
  });

  const { mutate: checkin, isPending: isCheckingIn } = useCheckin();

  const queue = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages =
    data?.totalPages ?? (totalCount === 0 ? 0 : Math.ceil(totalCount / pageSize));

  const checkedIn = queue.filter(
    (item) => item.status?.toUpperCase() === "APPROVED"
  );
  const pending = queue.filter(
    (item) => item.status?.toUpperCase() === "BOOKED"
  );

  // Zero-division safe progress calculation
  const progressPercent =
    totalCount === 0
      ? 0
      : Math.min(100, Math.max(0, Math.round((checkedIn.length / totalCount) * 100)));

  const handleFromDateChange = (val: string | Date | null | undefined) => {
    if (!val) return;
    const newFrom = typeof val === "string" ? val : format(val, "yyyy-MM-dd");
    if (toDate && newFrom > toDate) {
      setFromDate(toDate);
      setToDate(newFrom);
    } else {
      setFromDate(newFrom);
    }
    setPage(1);
  };

  const handleToDateChange = (val: string | Date | null | undefined) => {
    if (!val) return;
    const newTo = typeof val === "string" ? val : format(val, "yyyy-MM-dd");
    if (fromDate && newTo < fromDate) {
      setToDate(fromDate);
      setFromDate(newTo);
    } else {
      setToDate(newTo);
    }
    setPage(1);
  };

  const handlePreset = (preset: "today" | "7days" | "30days") => {
    const today = getTodayStr();
    if (preset === "today") {
      setFromDate(today);
      setToDate(today);
    } else if (preset === "7days") {
      setFromDate(getPastDateStr(7));
      setToDate(today);
    } else if (preset === "30days") {
      setFromDate(getPastDateStr(30));
      setToDate(today);
    }
    setPage(1);
  };

  const isTodayPreset = fromDate === todayStr && toDate === todayStr;
  const is7DaysPreset = fromDate === getPastDateStr(7) && toDate === todayStr;
  const is30DaysPreset = fromDate === getPastDateStr(30) && toDate === todayStr;

  const handleCheckin = (item: CheckinQueueItem) => {
    checkin({ appointmentId: item.appointmentId, caseId: item.caseId });
  };

  return (
    <div className="mx-auto w-full max-w-screen-2xl px-6 py-10">
      <header className="mb-6 flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="font-heading text-2xl font-bold text-foreground">Nurse Check-In</h1>
          <p className="text-muted-foreground capitalize">
            {format(new Date(), "EEEE, dd/MM/yyyy", { locale: vi })}
          </p>
        </div>
        <button
          onClick={() => refetch()}
          disabled={isRefetching}
          className="flex items-center gap-2 rounded-md border border-border bg-background px-3 py-2 text-sm text-muted-foreground hover:text-foreground disabled:opacity-50"
        >
          <RefreshCw className={`h-4 w-4 ${isRefetching ? "animate-spin" : ""}`} />
          Làm mới
        </button>
      </header>

      {/* Filter bar: Presets, Date Range Pickers, Status Filter */}
      <div className="mb-6 flex flex-wrap items-center gap-3">
        {/* Date Presets */}
        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={() => handlePreset("today")}
            className={cn(
              "rounded-full border px-4 py-2 text-sm font-medium transition-colors",
              isTodayPreset
                ? "border-accent bg-accent/10 text-accent"
                : "border-border text-foreground hover:bg-secondary"
            )}
          >
            Hôm nay
          </button>
          <button
            type="button"
            onClick={() => handlePreset("7days")}
            className={cn(
              "rounded-full border px-4 py-2 text-sm font-medium transition-colors",
              is7DaysPreset
                ? "border-accent bg-accent/10 text-accent"
                : "border-border text-foreground hover:bg-secondary"
            )}
          >
            7 ngày qua
          </button>
          <button
            type="button"
            onClick={() => handlePreset("30days")}
            className={cn(
              "rounded-full border px-4 py-2 text-sm font-medium transition-colors",
              is30DaysPreset
                ? "border-accent bg-accent/10 text-accent"
                : "border-border text-foreground hover:bg-secondary"
            )}
          >
            30 ngày qua
          </button>
        </div>

        {/* Date range pickers */}
        <div className="flex items-center gap-2">
          <DatePicker
            value={fromDate}
            maxDate={toDate ? new Date(toDate) : undefined}
            onChange={handleFromDateChange}
            className="w-[160px] rounded-full border border-border bg-background px-4 py-2 text-sm outline-none focus:border-accent"
          />
          <span className="text-muted-foreground">→</span>
          <DatePicker
            value={toDate}
            minDate={fromDate ? new Date(fromDate) : undefined}
            onChange={handleToDateChange}
            className="w-[160px] rounded-full border border-border bg-background px-4 py-2 text-sm outline-none focus:border-accent"
          />
        </div>

        {/* Status category dropdown filter */}
        <select
          value={status}
          onChange={(e) => {
            setStatus(e.target.value);
            setPage(1);
          }}
          className="h-10 rounded-full border border-border bg-background px-4 text-sm outline-none focus:border-accent"
        >
          <option value="ALL">Tất cả trạng thái</option>
          <option value="BOOKED">Đang chờ check-in</option>
          <option value="APPROVED">Đã check-in</option>
          <option value="COMPLETED">Đã hoàn thành</option>
          <option value="CANCELLED">Đã huỷ / Vắng mặt</option>
        </select>
      </div>

      {/* Search */}
      <div className="mb-6">
        <div className="relative">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <input
            type="text"
            placeholder="Tìm kiếm bệnh nhân (tên, số điện thoại), bác sĩ..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full rounded-full border border-input bg-background py-2 pl-10 pr-4 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
          />
        </div>
      </div>

      {/* Stats */}
      <div className="mb-6 flex gap-4">
        <div className="flex items-center gap-3 rounded-lg border border-accent/20 bg-accent/8 p-4">
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-accent/15 text-accent">
            <Check className="h-5 w-5" />
          </div>
          <div>
            <p className="text-sm font-medium text-accent">Đã check-in</p>
            <p className="text-2xl font-bold text-accent tabular-nums">{checkedIn.length}</p>
          </div>
        </div>
        <div className="flex items-center gap-3 rounded-lg border border-[var(--status-warning)]/20 bg-[var(--status-warning)]/8 p-4">
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-[var(--status-warning)]/15 text-[var(--status-warning)]">
            <Clock className="h-5 w-5" />
          </div>
          <div>
            <p className="text-sm font-medium text-[var(--status-warning)]">Đang chờ</p>
            <p className="text-2xl font-bold text-[var(--status-warning)] tabular-nums">{pending.length}</p>
          </div>
        </div>
      </div>

      {/* Progress bar */}
      <div className="mb-6">
        <div className="mb-1 flex items-center justify-between text-sm text-muted-foreground">
          <span>Tiến độ check-in</span>
          <span className="tabular-nums">
            {checkedIn.length} / {totalCount} bệnh nhân ({progressPercent}%)
          </span>
        </div>
        <div className="h-2 overflow-hidden rounded-full bg-muted">
          <div
            className="h-full rounded-full bg-accent transition-all duration-300"
            style={{ width: `${progressPercent}%` }}
          />
        </div>
      </div>

      {/* Queue Table */}
      <CheckinQueueTable
        queue={queue}
        isLoading={isLoading}
        onCheckin={handleCheckin}
        checkingInId={isCheckingIn ? "checking" : null}
        page={page}
        pageSize={pageSize}
        onDetail={setSelectedItem}
      />

      {/* Appointment Detail & Reschedule Modal */}
      {selectedItem && (
        <AppointmentDetailModal
          isOpen={Boolean(selectedItem)}
          item={selectedItem}
          onClose={() => setSelectedItem(null)}
          onCheckin={(item) => {
            handleCheckin(item);
            setSelectedItem(null);
          }}
        />
      )}

      {/* Pagination */}
      <div className="mt-5 flex items-center justify-between text-sm text-muted-foreground">
        <span>
          Đang xem {queue.length} / {totalCount} kết quả
        </span>
        <PaginationNumbered
          currentPage={page}
          totalPages={totalPages}
          setPage={setPage}
        />
      </div>
    </div>
  );
}

