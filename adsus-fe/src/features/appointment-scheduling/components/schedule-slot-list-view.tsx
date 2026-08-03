"use client";

import { useState } from "react";

import { useScheduleSlotList } from "../hooks/use-schedule-slots";
import type { SlotStatus } from "../types/schedule-slot.types";

type Filter = SlotStatus | "ALL";

const FILTER_LABELS: Record<Filter, string> = {
  ALL: "Tất cả",
  OPEN: "Mở",
  CLOSED: "Đã đóng",
};

/**
 * Module 8 UC-15 — list view cho Doctor/Nurse.
 * Filter theo status + slotDate, có nút "Tạo khung giờ".
 */
export function ScheduleSlotListView() {
  const [filterStatus, setFilterStatus] = useState<Filter>("ALL");
  const [filterDate, setFilterDate] = useState("");

  const { data, isLoading, isError } = useScheduleSlotList({
    status: filterStatus === "ALL" ? undefined : filterStatus,
    slotDate: filterDate || undefined,
    page: 1,
    pageSize: 20,
  });

  if (isLoading) return <p className="text-muted-foreground">Đang tải...</p>;
  if (isError) return <p className="text-destructive">Không tải được danh sách.</p>;
  if (!data || data.items.length === 0)
    return (
      <div className="space-y-4">
        <Toolbar filterStatus={filterStatus} setFilterStatus={setFilterStatus}
          filterDate={filterDate} setFilterDate={setFilterDate} />
        <div className="rounded-lg border border-dashed p-8 text-center">
          <p className="text-muted-foreground">Chưa có khung giờ nào.</p>
        </div>
      </div>
    );

  return (
    <div className="space-y-4">
      <Toolbar filterStatus={filterStatus} setFilterStatus={setFilterStatus}
        filterDate={filterDate} setFilterDate={setFilterDate} />

      <ul className="space-y-2">
        {data.items.map((s) => (
          <li
            key={s.slotId}
            className="flex items-center justify-between rounded-lg border bg-card p-3"
          >
            <div>
              <p className="font-medium">
                {s.doctorName} · {s.slotDate} · {s.startTime.slice(0, 5)}-{s.endTime.slice(0, 5)}
              </p>
              <p className="text-xs text-muted-foreground">
                {s.status === "OPEN" ? (
                  <span className="text-[#1cba9f]">Đang mở</span>
                ) : (
                  <span className="text-muted-foreground">Đã đóng</span>
                )}
              </p>
            </div>
            <a
              href={`/schedule-slots/${s.slotId}`}
              className="text-sm text-[#4488be] hover:underline"
            >
              Xem →
            </a>
          </li>
        ))}
      </ul>
    </div>
  );
}

function Toolbar({
  filterStatus,
  setFilterStatus,
  filterDate,
  setFilterDate,
}: {
  filterStatus: Filter;
  setFilterStatus: (v: Filter) => void;
  filterDate: string;
  setFilterDate: (v: string) => void;
}) {
  return (
    <div className="flex flex-wrap items-center gap-3">
      <div className="flex gap-2">
        {(Object.keys(FILTER_LABELS) as Filter[]).map((s) => (
          <button
            key={s}
            type="button"
            onClick={() => setFilterStatus(s)}
            className={`rounded-full px-3 py-1 text-sm ${
              filterStatus === s
                ? "bg-[#223a66] text-white"
                : "bg-muted text-muted-foreground hover:bg-muted/70"
            }`}
          >
            {FILTER_LABELS[s]}
          </button>
        ))}
      </div>
      <input
        type="date"
        value={filterDate}
        onChange={(e) => setFilterDate(e.target.value)}
        className="rounded-md border bg-background px-3 py-1 text-sm"
      />
      <a
        href="/schedule-slots/new"
        className="ml-auto rounded-full bg-[#1cba9f] px-4 py-1.5 text-sm font-medium text-white hover:bg-[#1cba9f]/90"
      >
        + Tạo khung giờ
      </a>
    </div>
  );
}