"use client";

import { useState, useEffect } from "react";
import { format } from "date-fns";
import { vi } from "date-fns/locale";
import { Search, RefreshCw, Check, Clock, UserX, XCircle } from "lucide-react";
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

  const rawQueue = data?.items ?? [];
  // Màn hình mặc định ("ALL") không hiện các ca hủy (Cancelled), chỉ hiện khi chọn category 'Đã huỷ'
  const queue = status === "ALL"
    ? rawQueue.filter((item) => (item.status || "").toUpperCase() !== "CANCELLED")
    : rawQueue;

  const totalCount = data?.totalCount ?? 0;
  const totalPages =
    data?.totalPages ?? (totalCount === 0 ? 0 : Math.ceil(totalCount / pageSize));

  // Period-wide counts directly from backend across the selected date range
  const bookedCount = data?.bookedCount ?? 0;
  const checkedInCount = data?.checkedInCount ?? 0;
  const noShowCount = data?.noShowCount ?? 0;
  const cancelledCount = data?.cancelledCount ?? 0;

  // Zero-division safe progress calculation across active appointments (booked + checked-in)
  const activeCount = bookedCount + checkedInCount;
  const progressPercent =
    activeCount === 0
      ? 0
      : Math.min(100, Math.max(0, Math.round((checkedInCount / activeCount) * 100)));

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

        {/* Status category dropdown filter: Booked, Approved/Checked-in, NoShow, and Cancelled */}
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
          <option value="NOSHOW">Vắng mặt</option>
          <option value="CANCELLED">Đã huỷ</option>
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

      {/* Stats cards: Clear, vibrant colors matching action column in table */}
      <div className="mb-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {/* Card 1: Đang chờ check-in - matches Check-in button */}
        <div className="flex items-center gap-3.5 rounded-xl border border-[#2E37A4]/25 bg-[#2E37A4]/5 p-4 shadow-xs">
          <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-[#2E37A4] text-white shadow-xs">
            <Clock className="h-5 w-5" />
          </div>
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-[#2E37A4]">
              Đang chờ check-in
            </p>
            <p className="text-2xl font-bold text-[#2E37A4] tabular-nums">
              {bookedCount}
            </p>
          </div>
        </div>

        {/* Card 2: Đã check-in - matches Đã check-in badge */}
        <div className="flex items-center gap-3.5 rounded-xl border border-emerald-600/25 bg-emerald-50/80 p-4 shadow-xs dark:bg-emerald-950/20">
          <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-emerald-600 text-white shadow-xs">
            <Check className="h-5 w-5" />
          </div>
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-emerald-700 dark:text-emerald-400">
              Đã check-in
            </p>
            <p className="text-2xl font-bold text-emerald-700 tabular-nums dark:text-emerald-400">
              {checkedInCount}
            </p>
          </div>
        </div>

        {/* Card 3: Vắng mặt */}
        <div className="flex items-center gap-3.5 rounded-xl border border-amber-600/25 bg-amber-50/80 p-4 shadow-xs dark:bg-amber-950/20">
          <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-amber-600 text-white shadow-xs">
            <UserX className="h-5 w-5" />
          </div>
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-amber-700 dark:text-amber-400">
              Vắng mặt
            </p>
            <p className="text-2xl font-bold text-amber-700 tabular-nums dark:text-amber-400">
              {noShowCount}
            </p>
          </div>
        </div>

        {/* Card 4: Đã huỷ - matches Đã huỷ badge */}
        <div className="flex items-center gap-3.5 rounded-xl border border-rose-600/25 bg-rose-50/80 p-4 shadow-xs dark:bg-rose-950/20">
          <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-rose-600 text-white shadow-xs">
            <XCircle className="h-5 w-5" />
          </div>
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-rose-700 dark:text-rose-400">
              Đã huỷ
            </p>
            <p className="text-2xl font-bold text-rose-700 tabular-nums dark:text-rose-400">
              {cancelledCount}
            </p>
          </div>
        </div>
      </div>

      {/* Progress bar */}
      <div className="mb-6">
        <div className="mb-1 flex items-center justify-between text-sm text-muted-foreground">
          <span>Tiến độ check-in (toàn mốc thời gian)</span>
          <span className="tabular-nums">
            {checkedInCount} / {activeCount} bệnh nhân ({progressPercent}%)
          </span>
        </div>
        <div className="h-2 overflow-hidden rounded-full bg-muted">
          <div
            className="h-full rounded-full bg-emerald-600 transition-all duration-300"
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

