"use client";

import { ChevronLeft, ChevronRight, Loader2, Plus, X } from "lucide-react";
import { useEffect, useMemo, useState } from "react";

import { getApiErrorMessage } from "@/lib/api-client";

import {
  useCloseScheduleSlot,
  useCreateScheduleSlot,
  useEnsureDefaultSlots,
  useScheduleSlots,
  useUpdateScheduleSlot,
} from "../hooks/use-schedule-slot";
import type {
  CreateScheduleSlotRequest,
  ScheduleSlotResponse,
  SlotStatus,
  UpdateScheduleSlotRequest,
} from "../types/schedule-slot.types";

const STATUS_LABELS: Record<SlotStatus, string> = {
  OPEN: "Đang mở",
  CLOSED: "Đã đóng",
};

const STATUS_STYLES: Record<SlotStatus, string> = {
  OPEN: "bg-green-100 text-green-800",
  CLOSED: "bg-slate-200 text-slate-700",
};

const WEEKDAY_LABELS_VI = ["T2", "T3", "T4", "T5", "T6", "T7", "CN"];

type ViewMode = "week" | "month";

/**
 * SCR-20 — Manage Schedule Slots (UC-15, Web).
 * Doctor tự quản lý lịch của chính mình. Hệ thống tự sinh ca mặc định T2-T6 (8h-12h, 13h-17h).
 * Toggle Week / Month view. T7-CN doctor tự thêm.
 */
export function ScheduleSlotManagementView() {
  const todayIso = useMemo(() => isoDate(new Date()), []);
  const today = useMemo(() => new Date(), []);

  const [viewMode, setViewMode] = useState<ViewMode>("week");
  // Anchor: với week = T2 của tuần đang xem; với month = ngày 1 của tháng đang xem.
  const [anchor, setAnchor] = useState<Date>(() =>
    viewMode === "week" ? mondayOfWeek(today) : startOfMonth(today),
  );
  // Khi đổi viewMode thì reset anchor phù hợp.
  useEffect(() => {
    setAnchor(viewMode === "week" ? mondayOfWeek(today) : startOfMonth(today));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [viewMode]);

  const [showCreate, setShowCreate] = useState(false);
  const [defaultDate, setDefaultDate] = useState<string>(todayIso);
  const [editingSlot, setEditingSlot] = useState<ScheduleSlotResponse | null>(null);

  // Tính range hiển thị theo viewMode.
  const { fromDate, toDate, rangeLabel } = useMemo(() => {
    if (viewMode === "week") {
      const start = mondayOfWeek(anchor);
      const end = sundayOfWeek(anchor);
      return {
        fromDate: isoDate(start),
        toDate: isoDate(end),
        rangeLabel: `Tuần ${isoDate(start)} → ${isoDate(end)}`,
      };
    }
    const start = mondayOfWeek(startOfMonth(anchor));
    const end = sundayOfWeek(endOfMonth(anchor));
    const m = anchor.getMonth() + 1;
    return {
      fromDate: isoDate(start),
      toDate: isoDate(end),
      rangeLabel: `Tháng ${m}/${anchor.getFullYear()}`,
    };
  }, [anchor, viewMode]);

  const listQuery = useScheduleSlots({ fromDate, toDate });
  const createMutation = useCreateScheduleSlot();
  const updateMutation = useUpdateScheduleSlot();
  const closeMutation = useCloseScheduleSlot();
  const ensureDefaultMutation = useEnsureDefaultSlots();

  // Auto-ensure default slots của tuần hiện tại khi load trang lần đầu.
  const ensuredWeekRef = useMemo(() => isoDate(mondayOfWeek(today)), [today]);
  useEffect(() => {
    if (!listQuery.isLoading && !listQuery.isError && (listQuery.data?.length ?? 0) === 0) {
      ensureDefaultMutation.mutate(ensuredWeekRef);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [listQuery.isLoading, listQuery.data?.length, ensuredWeekRef]);

  // Tự ensure-default tất cả tuần trong range đang xem.
  const [ensuringMonth, setEnsuringMonth] = useState(false);
  async function ensureMonth() {
    setEnsuringMonth(true);
    try {
      let cursor = mondayOfWeek(anchor);
      const end = viewMode === "week" ? addDays(cursor, 7) : endOfMonth(anchor);
      let safety = 0;
      while (cursor <= end && safety < 8) {
        await ensureDefaultMutation.mutateAsync(isoDate(cursor));
        cursor = addDays(cursor, 7);
        safety++;
      }
    } finally {
      setEnsuringMonth(false);
    }
  }

  const slots = listQuery.data ?? [];

  const slotsByDay = useMemo(() => {
    const map = new Map<string, ScheduleSlotResponse[]>();
    for (const s of slots) {
      const list = map.get(s.slotDate) ?? [];
      list.push(s);
      map.set(s.slotDate, list);
    }
    for (const [k, v] of map) {
      v.sort((a, b) => a.startTime.localeCompare(b.startTime));
      map.set(k, v);
    }
    return map;
  }, [slots]);

  const goPrev = () => {
    setAnchor(viewMode === "week" ? addDays(anchor, -7) : addMonths(anchor, -1));
  };
  const goNext = () => {
    setAnchor(viewMode === "week" ? addDays(anchor, 7) : addMonths(anchor, 1));
  };

  return (
    <div className="space-y-6">
      <header className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Quản lý lịch khám</h1>
          <p className="text-sm text-slate-500">
            Hệ thống tự sinh ca mặc định T2-T6 (8h-12h, 13h-17h). T7-CN doctor tự thêm.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <ViewToggle mode={viewMode} onChange={setViewMode} />
          <button
            type="button"
            onClick={() => ensureDefaultMutation.mutate(ensuredWeekRef)}
            disabled={ensureDefaultMutation.isPending}
            className="inline-flex items-center gap-2 rounded-md border border-slate-300 px-3 py-2 text-sm hover:bg-slate-50 disabled:opacity-50"
          >
            {ensureDefaultMutation.isPending && <Loader2 className="h-4 w-4 animate-spin" />}
            Khôi phục tuần này
          </button>
          <button
            type="button"
            onClick={ensureMonth}
            disabled={ensuringMonth}
            className="inline-flex items-center gap-2 rounded-md border border-slate-300 px-3 py-2 text-sm hover:bg-slate-50 disabled:opacity-50"
          >
            {ensuringMonth && <Loader2 className="h-4 w-4 animate-spin" />}
            Khôi phục {viewMode === "week" ? "tuần" : "tháng"}
          </button>
          <button
            type="button"
            onClick={() => {
              setDefaultDate(todayIso);
              setShowCreate(true);
            }}
            className="inline-flex items-center gap-2 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
          >
            <Plus className="h-4 w-4" /> Thêm khung giờ
          </button>
        </div>
      </header>

      <div className="flex items-center justify-between rounded-md border border-slate-200 bg-white p-3">
        <button
          type="button"
          onClick={goPrev}
          className="rounded-md border border-slate-300 p-1 hover:bg-slate-50"
          title={viewMode === "week" ? "Tuần trước" : "Tháng trước"}
        >
          <ChevronLeft className="h-4 w-4" />
        </button>
        <h2 className="text-lg font-semibold">{rangeLabel}</h2>
        <button
          type="button"
          onClick={goNext}
          className="rounded-md border border-slate-300 p-1 hover:bg-slate-50"
          title={viewMode === "week" ? "Tuần sau" : "Tháng sau"}
        >
          <ChevronRight className="h-4 w-4" />
        </button>
      </div>

      {listQuery.isLoading ? (
        <div className="flex items-center justify-center gap-2 rounded-md border border-slate-200 bg-white p-8 text-slate-500">
          <Loader2 className="h-4 w-4 animate-spin" /> Đang tải…
        </div>
      ) : listQuery.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          {getApiErrorMessage(listQuery.error, "Không tải được danh sách khung giờ.")}
        </div>
      ) : viewMode === "week" ? (
        <WeekView
          weekStart={mondayOfWeek(anchor)}
          todayIso={todayIso}
          slotsByDay={slotsByDay}
          onAddClick={(dateIso) => {
            setDefaultDate(dateIso);
            setShowCreate(true);
          }}
          onEdit={(s) => setEditingSlot(s)}
          onClose={async (s, force) => {
            if (!force) {
              const msg =
                s.activeAppointmentsCount > 0
                  ? `Khung giờ này có ${s.activeAppointmentsCount} lịch hẹn đang BOOKED. ` +
                    "Bạn có chắc muốn đóng? Sau khi đóng, không thể mở lại và bệnh nhân không đặt thêm được."
                  : `Bạn có chắc muốn đóng khung giờ ${s.startTime.slice(0, 5)}–${s.endTime.slice(0, 5)} ` +
                    `ngày ${s.slotDate}? Sau khi đóng, không thể mở lại.`;
              if (!confirm(msg)) return;
            }
            try {
              await closeMutation.mutateAsync({ id: s.slotId, force });
            } catch (err) {
              const apiErr = err as {
                response?: { data?: { data?: { affectedBookingsCount?: number } } };
              };
              const bookings = apiErr?.response?.data?.data?.affectedBookingsCount;
              if (bookings && bookings > 0) {
                const ok = confirm(
                  `Khung giờ này có ${bookings} lịch hẹn đang BOOKED. Bạn có chắc muốn đóng? ` +
                    `(Các booking hiện tại vẫn giữ nguyên, nhưng bệnh nhân không đặt thêm được.)`,
                );
                if (ok) {
                  await closeMutation.mutateAsync({ id: s.slotId, force: true });
                }
              } else {
                alert(getApiErrorMessage(err, "Không đóng được khung giờ."));
              }
            }
          }}
        />
      ) : (
        <MonthView
          monthStart={startOfMonth(anchor)}
          gridStart={mondayOfWeek(startOfMonth(anchor))}
          gridEnd={sundayOfWeek(endOfMonth(anchor))}
          todayIso={todayIso}
          slotsByDay={slotsByDay}
          onAddClick={(dateIso) => {
            setDefaultDate(dateIso);
            setShowCreate(true);
          }}
          onEdit={(s) => setEditingSlot(s)}
          onClose={async (s, force) => {
            if (!force) {
              const msg =
                s.activeAppointmentsCount > 0
                  ? `Khung giờ này có ${s.activeAppointmentsCount} lịch hẹn đang BOOKED. ` +
                    "Bạn có chắc muốn đóng? Sau khi đóng, không thể mở lại và bệnh nhân không đặt thêm được."
                  : `Bạn có chắc muốn đóng khung giờ ${s.startTime.slice(0, 5)}–${s.endTime.slice(0, 5)} ` +
                    `ngày ${s.slotDate}? Sau khi đóng, không thể mở lại.`;
              if (!confirm(msg)) return;
            }
            try {
              await closeMutation.mutateAsync({ id: s.slotId, force });
            } catch (err) {
              const apiErr = err as {
                response?: { data?: { data?: { affectedBookingsCount?: number } } };
              };
              const bookings = apiErr?.response?.data?.data?.affectedBookingsCount;
              if (bookings && bookings > 0) {
                const ok = confirm(
                  `Khung giờ này có ${bookings} lịch hẹn đang BOOKED. Bạn có chắc muốn đóng? ` +
                    `(Các booking hiện tại vẫn giữ nguyên, nhưng bệnh nhân không đặt thêm được.)`,
                );
                if (ok) {
                  await closeMutation.mutateAsync({ id: s.slotId, force: true });
                }
              } else {
                alert(getApiErrorMessage(err, "Không đóng được khung giờ."));
              }
            }
          }}
        />
      )}

      {showCreate && (
        <CreateSlotModal
          defaultDate={defaultDate}
          onClose={() => setShowCreate(false)}
          onSubmit={async (payload) => {
            try {
              await createMutation.mutateAsync(payload);
              setShowCreate(false);
            } catch (err) {
              alert(getApiErrorMessage(err, "Không tạo được khung giờ."));
            }
          }}
          submitting={createMutation.isPending}
        />
      )}

      {editingSlot && (
        <EditSlotModal
          slot={editingSlot}
          onClose={() => setEditingSlot(null)}
          onSubmit={async (payload) => {
            try {
              await updateMutation.mutateAsync({ id: editingSlot.slotId, payload });
              setEditingSlot(null);
            } catch (err) {
              alert(getApiErrorMessage(err, "Không cập nhật được khung giờ."));
            }
          }}
          submitting={updateMutation.isPending}
        />
      )}
    </div>
  );
}

function ViewToggle({
  mode,
  onChange,
}: {
  mode: ViewMode;
  onChange: (m: ViewMode) => void;
}) {
  return (
    <div className="inline-flex overflow-hidden rounded-md border border-slate-300">
      <button
        type="button"
        onClick={() => onChange("week")}
        className={`px-3 py-2 text-sm ${
          mode === "week"
            ? "bg-blue-600 text-white"
            : "bg-white text-slate-700 hover:bg-slate-50"
        }`}
      >
        Tuần
      </button>
      <button
        type="button"
        onClick={() => onChange("month")}
        className={`border-l border-slate-300 px-3 py-2 text-sm ${
          mode === "month"
            ? "bg-blue-600 text-white"
            : "bg-white text-slate-700 hover:bg-slate-50"
        }`}
      >
        Tháng
      </button>
    </div>
  );
}

function WeekView({
  weekStart,
  todayIso,
  slotsByDay,
  onAddClick,
  onEdit,
  onClose,
}: {
  weekStart: Date;
  todayIso: string;
  slotsByDay: Map<string, ScheduleSlotResponse[]>;
  onAddClick: (dateIso: string) => void;
  onEdit: (s: ScheduleSlotResponse) => void;
  onClose: (s: ScheduleSlotResponse, force: boolean) => void | Promise<void>;
}) {
  return (
    <div className="grid grid-cols-7 gap-2">
      {Array.from({ length: 7 }).map((_, i) => {
        const date = addDays(weekStart, i);
        const dateIso = isoDate(date);
        const daySlots = slotsByDay.get(dateIso) ?? [];
        const isPast = dateIso < todayIso;
        return (
          <DayColumn
            key={dateIso}
            dateIso={dateIso}
            weekdayLabel={WEEKDAY_LABELS_VI[i]}
            slots={daySlots}
            isPast={isPast}
            onAddClick={onAddClick}
            onEdit={onEdit}
            onClose={onClose}
          />
        );
      })}
    </div>
  );
}

function MonthView({
  monthStart,
  gridStart,
  gridEnd,
  todayIso,
  slotsByDay,
  onAddClick,
  onEdit,
  onClose,
}: {
  monthStart: Date;
  gridStart: Date;
  gridEnd: Date;
  todayIso: string;
  slotsByDay: Map<string, ScheduleSlotResponse[]>;
  onAddClick: (dateIso: string) => void;
  onEdit: (s: ScheduleSlotResponse) => void;
  onClose: (s: ScheduleSlotResponse, force: boolean) => void | Promise<void>;
}) {
  const days = useMemo(() => {
    const list: Date[] = [];
    for (let d = new Date(gridStart); d <= gridEnd; d = addDays(d, 1)) {
      list.push(new Date(d));
    }
    return list;
  }, [gridStart, gridEnd]);

  return (
    <div className="rounded-md border border-slate-200 bg-white">
      <div className="grid grid-cols-7 border-b border-slate-200 bg-slate-50">
        {WEEKDAY_LABELS_VI.map((label) => (
          <div key={label} className="px-2 py-2 text-center text-xs font-semibold text-slate-600">
            {label}
          </div>
        ))}
      </div>
      <div className="grid grid-cols-7">
        {days.map((day) => {
          const dateIso = isoDate(day);
          const isCurrentMonth = day.getMonth() === monthStart.getMonth();
          const isPast = dateIso < todayIso;
          const isToday = dateIso === todayIso;
          const daySlots = slotsByDay.get(dateIso) ?? [];
          return (
            <DayCell
              key={dateIso}
              dateIso={dateIso}
              isCurrentMonth={isCurrentMonth}
              isPast={isPast}
              isToday={isToday}
              slots={daySlots}
              onAddClick={() => onAddClick(dateIso)}
              onEdit={onEdit}
              onClose={onClose}
            />
          );
        })}
      </div>
    </div>
  );
}

function DayColumn({
  dateIso,
  weekdayLabel,
  slots,
  isPast,
  onAddClick,
  onEdit,
  onClose,
}: {
  dateIso: string;
  weekdayLabel: string;
  slots: ScheduleSlotResponse[];
  isPast: boolean;
  onAddClick: (dateIso: string) => void;
  onEdit: (s: ScheduleSlotResponse) => void;
  onClose: (s: ScheduleSlotResponse, force: boolean) => void | Promise<void>;
}) {
  return (
    <div className="min-h-[280px] rounded-md border border-slate-200 bg-white p-2">
      <div className="mb-2 flex items-center justify-between">
        <div>
          <div className="text-xs font-semibold uppercase text-slate-500">{weekdayLabel}</div>
          <div className="text-sm text-slate-700">{dateIso}</div>
        </div>
        {!isPast && (
          <button
            type="button"
            onClick={() => onAddClick(dateIso)}
            className="rounded border border-slate-200 p-0.5 text-slate-400 hover:bg-slate-50 hover:text-slate-600"
            title="Thêm khung giờ"
          >
            <Plus className="h-3 w-3" />
          </button>
        )}
      </div>
      <div className="space-y-2">
        {slots.length === 0 && (
          <div className="rounded border border-dashed border-slate-200 p-3 text-center text-xs text-slate-400">
            {isPast ? "Đã qua" : "Trống"}
          </div>
        )}
        {slots.map((s) => (
          <SlotCard
            key={s.slotId}
            slot={s}
            onEdit={() => onEdit(s)}
            onClose={() => void onClose(s, false)}
          />
        ))}
      </div>
    </div>
  );
}

function DayCell({
  dateIso,
  isCurrentMonth,
  isPast,
  isToday,
  slots,
  onAddClick,
  onEdit,
  onClose,
}: {
  dateIso: string;
  isCurrentMonth: boolean;
  isPast: boolean;
  isToday: boolean;
  slots: ScheduleSlotResponse[];
  onAddClick: () => void;
  onEdit: (s: ScheduleSlotResponse) => void;
  onClose: (s: ScheduleSlotResponse, force: boolean) => void | Promise<void>;
}) {
  const dayNum = Number(dateIso.slice(8, 10));
  return (
    <div
      className={`min-h-[110px] border-b border-r border-slate-100 p-1.5 text-xs ${
        isCurrentMonth ? "bg-white" : "bg-slate-50/50"
      }`}
    >
      <div className="mb-1 flex items-center justify-between">
        <span
          className={`flex h-6 w-6 items-center justify-center rounded-full text-[11px] ${
            isToday
              ? "bg-blue-600 font-bold text-white"
              : isCurrentMonth
              ? "text-slate-700"
              : "text-slate-400"
          }`}
        >
          {dayNum}
        </span>
        {!isPast && isCurrentMonth && (
          <button
            type="button"
            onClick={onAddClick}
            className="rounded border border-slate-200 p-0.5 text-slate-400 hover:bg-slate-50 hover:text-slate-600"
            title="Thêm khung giờ"
          >
            <Plus className="h-3 w-3" />
          </button>
        )}
      </div>
      <div className="space-y-1">
        {slots.map((s) => (
          <SlotChip
            key={s.slotId}
            slot={s}
            onEdit={() => onEdit(s)}
            onClose={() => void onClose(s, false)}
          />
        ))}
      </div>
    </div>
  );
}

function SlotCard({
  slot,
  onEdit,
  onClose,
}: {
  slot: ScheduleSlotResponse;
  onEdit: () => void;
  onClose: () => void;
}) {
  return (
    <div className="rounded border border-slate-200 p-2 text-xs">
      <div className="flex items-start justify-between gap-2">
        <span className="font-mono text-slate-700">
          {slot.startTime.slice(0, 5)}–{slot.endTime.slice(0, 5)}
        </span>
        <span className={`shrink-0 rounded-full px-2 py-0.5 text-[10px] ${STATUS_STYLES[slot.status]}`}>
          {STATUS_LABELS[slot.status]}
        </span>
      </div>
      {slot.activeAppointmentsCount > 0 && (
        <div className="mt-1 text-blue-600">Booking: {slot.activeAppointmentsCount}</div>
      )}
      <div className="mt-2 flex gap-1">
        {slot.status === "OPEN" && (
          <>
            <button
              type="button"
              onClick={onEdit}
              className="flex-1 rounded border border-slate-300 px-2 py-0.5 hover:bg-slate-50"
            >
              Sửa
            </button>
            <button
              type="button"
              onClick={onClose}
              className="rounded border border-slate-300 px-2 py-0.5 hover:bg-slate-50"
            >
              <X className="h-3 w-3" />
            </button>
          </>
        )}
      </div>
    </div>
  );
}

function SlotChip({
  slot,
  onEdit,
  onClose,
}: {
  slot: ScheduleSlotResponse;
  onEdit: () => void;
  onClose: () => void;
}) {
  return (
    <div className="group rounded border border-slate-200 bg-white px-1 py-0.5 hover:bg-slate-50">
      <div className="flex items-center justify-between gap-1">
        <span className="font-mono text-[10px] text-slate-700">
          {slot.startTime.slice(0, 5)}–{slot.endTime.slice(0, 5)}
        </span>
        <span className={`rounded-full px-1 text-[9px] ${STATUS_STYLES[slot.status]}`}>
          {slot.status === "OPEN" ? "" : "✕"}
        </span>
      </div>
      {slot.activeAppointmentsCount > 0 && (
        <div className="text-[9px] text-blue-600">📅 {slot.activeAppointmentsCount}</div>
      )}
      <div className="hidden gap-0.5 group-hover:flex">
        {slot.status === "OPEN" && (
          <>
            <button
              type="button"
              onClick={onEdit}
              className="flex-1 rounded bg-slate-100 px-1 text-[9px] hover:bg-slate-200"
            >
              Sửa
            </button>
            <button
              type="button"
              onClick={onClose}
              className="rounded bg-slate-100 px-1 hover:bg-slate-200"
            >
              <X className="h-2.5 w-2.5" />
            </button>
          </>
        )}
      </div>
    </div>
  );
}

function CreateSlotModal({
  defaultDate,
  onClose,
  onSubmit,
  submitting,
}: {
  defaultDate: string;
  onClose: () => void;
  onSubmit: (payload: CreateScheduleSlotRequest) => void | Promise<void>;
  submitting: boolean;
}) {
  const [visitDate, setVisitDate] = useState(defaultDate);
  const [startTime, setStartTime] = useState("08:00:00");
  const [endTime, setEndTime] = useState("09:00:00");

  return (
    <ModalShell title="Thêm khung giờ mới" onClose={onClose}>
      <form
        className="space-y-3"
        onSubmit={(e) => {
          e.preventDefault();
          void onSubmit({ visitDate, startTime, endTime });
        }}
      >
        <label className="block text-sm">
          <span className="text-slate-600">Ngày khám</span>
          <input
            required
            type="date"
            value={visitDate}
            onChange={(e) => setVisitDate(e.target.value)}
            className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2"
          />
        </label>
        <TimeInputs
          startTime={startTime}
          endTime={endTime}
          onStartChange={setStartTime}
          onEndChange={setEndTime}
        />
        <p className="text-xs text-slate-500">
          BR-01: range &gt; 15 phút, VisitDate + StartTime &gt; now (UTC), không overlap.
        </p>
        <ModalActions submitting={submitting} onClose={onClose} submitLabel="Tạo khung giờ" />
      </form>
    </ModalShell>
  );
}

function EditSlotModal({
  slot,
  onClose,
  onSubmit,
  submitting,
}: {
  slot: ScheduleSlotResponse;
  onClose: () => void;
  onSubmit: (payload: UpdateScheduleSlotRequest) => void | Promise<void>;
  submitting: boolean;
}) {
  const [startTime, setStartTime] = useState(slot.startTime);
  const [endTime, setEndTime] = useState(slot.endTime);

  return (
    <ModalShell title={`Sửa khung giờ ${slot.slotDate}`} onClose={onClose}>
      <form
        className="space-y-3"
        onSubmit={(e) => {
          e.preventDefault();
          void onSubmit({ startTime, endTime });
        }}
      >
        <TimeInputs
          startTime={startTime}
          endTime={endTime}
          onStartChange={setStartTime}
          onEndChange={setEndTime}
        />
        <p className="text-xs text-slate-500">
          Dùng để tách ca (vd 8h-12h → 8h-10h + 10h-12h). Booking vẫn giữ nguyên.
        </p>
        <ModalActions submitting={submitting} onClose={onClose} submitLabel="Lưu" />
      </form>
    </ModalShell>
  );
}

function ModalShell({
  title,
  onClose,
  children,
}: {
  title: string;
  onClose: () => void;
  children: React.ReactNode;
}) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl">
        <h2 className="mb-4 text-lg font-semibold">{title}</h2>
        {children}
      </div>
    </div>
  );
}

function ModalActions({
  submitting,
  onClose,
  submitLabel,
}: {
  submitting: boolean;
  onClose: () => void;
  submitLabel: string;
}) {
  return (
    <div className="mt-4 flex justify-end gap-2">
      <button
        type="button"
        onClick={onClose}
        className="rounded-md border border-slate-300 px-4 py-2 text-sm"
      >
        Hủy
      </button>
      <button
        type="submit"
        disabled={submitting}
        className="inline-flex items-center gap-2 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
      >
        {submitting && <Loader2 className="h-4 w-4 animate-spin" />}
        {submitLabel}
      </button>
    </div>
  );
}

function TimeInputs({
  startTime,
  endTime,
  onStartChange,
  onEndChange,
}: {
  startTime: string;
  endTime: string;
  onStartChange: (v: string) => void;
  onEndChange: (v: string) => void;
}) {
  return (
    <div className="grid grid-cols-2 gap-3">
      <label className="block text-sm">
        <span className="text-slate-600">Bắt đầu (HH:mm:ss, 24h)</span>
        <input
          required
          type="text"
          pattern="^\d{2}:\d{2}:\d{2}$"
          placeholder="08:00:00"
          value={startTime}
          onChange={(e) => onStartChange(e.target.value)}
          className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 font-mono"
        />
      </label>
      <label className="block text-sm">
        <span className="text-slate-600">Kết thúc (HH:mm:ss, 24h)</span>
        <input
          required
          type="text"
          pattern="^\d{2}:\d{2}:\d{2}$"
          placeholder="09:00:00"
          value={endTime}
          onChange={(e) => onEndChange(e.target.value)}
          className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 font-mono"
        />
      </label>
    </div>
  );
}

function isoDate(d: Date): string {
  const yyyy = d.getFullYear();
  const mm = String(d.getMonth() + 1).padStart(2, "0");
  const dd = String(d.getDate()).padStart(2, "0");
  return `${yyyy}-${mm}-${dd}`;
}

function addDays(d: Date, n: number): Date {
  const next = new Date(d);
  next.setDate(d.getDate() + n);
  return next;
}

function addMonths(d: Date, n: number): Date {
  const next = new Date(d);
  next.setMonth(d.getMonth() + n);
  return next;
}

function startOfMonth(d: Date): Date {
  return new Date(d.getFullYear(), d.getMonth(), 1);
}

function endOfMonth(d: Date): Date {
  return new Date(d.getFullYear(), d.getMonth() + 1, 0);
}

function mondayOfWeek(d: Date): Date {
  const dow = d.getDay();
  const offset = dow === 0 ? -6 : 1 - dow;
  return addDays(d, offset);
}

function sundayOfWeek(d: Date): Date {
  const dow = d.getDay();
  const offset = dow === 0 ? 0 : 7 - dow;
  return addDays(d, offset);
}