"use client";

import { ChevronLeft, ChevronRight, Loader2, Plus, X } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import toast from "react-hot-toast";

import { getApiErrorMessage } from "@/lib/api-client";

import {
  useCloseScheduleSlot,
  useCreateScheduleSlot,
  useReopenScheduleSlot,
  useScheduleSlots,
} from "../hooks/use-schedule-slot";
import type {
  CreateScheduleSlotRequest,
  ScheduleSlotResponse,
  SlotStatus,
} from "../types/schedule-slot.types";

const STATUS_LABELS: Record<SlotStatus, string> = {
  OPEN: "Đang mở",
  BOOKED: "Đã đặt",
  CLOSED: "Đã đóng",
};

const STATUS_STYLES: Record<SlotStatus, string> = {
  OPEN: "bg-green-100 text-green-800",
  BOOKED: "bg-blue-100 text-blue-800",
  CLOSED: "bg-slate-200 text-slate-700",
};

const WEEKDAY_LABELS_VI = ["T2", "T3", "T4", "T5", "T6", "T7", "CN"];

/**
 * SCR-20 — Manage Schedule Slots (UC-15, Web).
 * Hiện 1 tuần (7 ngày T2-CN) tại 1 thời điểm.
 * Bấm Next/Prev để chuyển tuần.
 * Hệ thống tự sinh ca mặc định T2-CN (8h-12h, 13h-17h) cho 3 tuần.
 */
export function ScheduleSlotManagementView() {
  const todayIso = useMemo(() => isoDate(new Date()), []);

  // weekAnchor = T2 của tuần hiện tại
  const [weekAnchor, setWeekAnchor] = useState<Date>(() => mondayOfWeek(new Date()));
  const [showCreate, setShowCreate] = useState(false);
  const [defaultDate, setDefaultDate] = useState<string>(todayIso);
  const [confirmAction, setConfirmAction] = useState<{
    type: "close" | "forceClose" | "reopen";
    slot: ScheduleSlotResponse;
  } | null>(null);

  // Tính range: 1 tuần = 7 ngày từ weekAnchor (T2)
  const { fromDate, toDate, rangeLabel } = useMemo(() => {
    const start = mondayOfWeek(weekAnchor);
    const end = sundayOfWeek(weekAnchor);
    return {
      fromDate: isoDate(start),
      toDate: isoDate(end),
      rangeLabel: `Tuần ${isoDate(start)} → ${isoDate(end)}`,
    };
  }, [weekAnchor]);

  const listQuery = useScheduleSlots({ fromDate, toDate, pageSize: 200 });
  const createMutation = useCreateScheduleSlot();
  const closeMutation = useCloseScheduleSlot();
  const reopenMutation = useReopenScheduleSlot();

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

  // Prev/Next: chuyển 1 tuần
  const goPrev = () => setWeekAnchor(addDays(weekAnchor, -7));
  const goNext = () => setWeekAnchor(addDays(weekAnchor, 7));

  return (
    <div className="space-y-6">
      <header className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Quản lý lịch khám</h1>
          <p className="text-sm text-slate-500">
            Hiện 1 tuần (T2-CN). Hệ thống tự sinh ca mặc định cho 3 tuần tới.
          </p>
        </div>
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
      </header>

      <div className="flex items-center justify-between rounded-md border border-slate-200 bg-white p-3">
        <button
          type="button"
          onClick={goPrev}
          className="rounded-md border border-slate-300 p-1 hover:bg-slate-50"
          title="Tuần trước"
        >
          <ChevronLeft className="h-4 w-4" />
        </button>
        <h2 className="text-lg font-semibold">{rangeLabel}</h2>
        <button
          type="button"
          onClick={goNext}
          className="rounded-md border border-slate-300 p-1 hover:bg-slate-50"
          title="Tuần sau"
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
      ) : (
        <WeekView
          weekStart={mondayOfWeek(weekAnchor)}
          todayIso={todayIso}
          slotsByDay={slotsByDay}
          onAddClick={(dateIso) => {
            setDefaultDate(dateIso);
            setShowCreate(true);
          }}
          onClose={(s, force) => {
            if (!force) {
              setConfirmAction({ type: "close", slot: s });
              return;
            }
            setConfirmAction({ type: "forceClose", slot: s });
          }}
          onReopen={(s) => setConfirmAction({ type: "reopen", slot: s })}
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
              await listQuery.refetch();
              toast.success("Đã tạo khung giờ mới.");
            } catch (err) {
              toast.error(getApiErrorMessage(err, "Không tạo được khung giờ."));
            }
          }}
          submitting={createMutation.isPending}
        />
      )}

      {confirmAction && (
        <ConfirmModal
          title={
            confirmAction.type === "close"
              ? "Xác nhận đóng ca"
              : confirmAction.type === "forceClose"
              ? "Xác nhận đóng ca (có booking)"
              : "Xác nhận mở lại ca"
          }
          message={
            confirmAction.type === "close"
              ? confirmAction.slot.activeAppointmentsCount > 0
                ? `Ca khám ${confirmAction.slot.startTime.slice(0, 5)}–${confirmAction.slot.endTime.slice(0, 5)} ngày ${confirmAction.slot.slotDate} có ${confirmAction.slot.activeAppointmentsCount} lịch hẹn đang đặt. Bạn có chắc muốn đóng?`
                : `Bạn có chắc muốn đóng ca khám ${confirmAction.slot.startTime.slice(0, 5)}–${confirmAction.slot.endTime.slice(0, 5)} ngày ${confirmAction.slot.slotDate}?`
              : confirmAction.type === "forceClose"
              ? `Khung giờ này có ${confirmAction.slot.activeAppointmentsCount} lịch hẹn đang BOOKED. Các booking hiện tại vẫn giữ nguyên, nhưng bệnh nhân không đặt thêm được.`
              : `Mở lại ca khám ${confirmAction.slot.startTime.slice(0, 5)}–${confirmAction.slot.endTime.slice(0, 5)} ngày ${confirmAction.slot.slotDate}?`
          }
          variant={confirmAction.type === "reopen" ? "info" : confirmAction.type === "forceClose" ? "warning" : "danger"}
          confirmLabel={
            confirmAction.type === "close" ? "Đóng ca"
            : confirmAction.type === "forceClose" ? "Đóng ca"
            : "Mở lại"
          }
          onConfirm={async () => {
            try {
              if (confirmAction.type === "close") {
                await closeMutation.mutateAsync({ id: confirmAction.slot.slotId, force: false });
              } else if (confirmAction.type === "forceClose") {
                await closeMutation.mutateAsync({ id: confirmAction.slot.slotId, force: true });
              } else {
                await reopenMutation.mutateAsync(confirmAction.slot.slotId);
              }
              setConfirmAction(null);
              await listQuery.refetch();
              toast.success(
                confirmAction.type === "reopen"
                  ? `Đã mở lại ca khám ${confirmAction.slot.startTime.slice(0, 5)}–${confirmAction.slot.endTime.slice(0, 5)}.`
                  : `Đã đóng ca khám ${confirmAction.slot.startTime.slice(0, 5)}–${confirmAction.slot.endTime.slice(0, 5)}.`
              );
            } catch (err) {
              setConfirmAction(null);
              toast.error(getApiErrorMessage(err, "Thao tác thất bại."));
            }
          }}
          onCancel={() => setConfirmAction(null)}
        />
      )}
    </div>
  );
}

/**
 * Hiện 1 tuần = 7 cột ngày (T2-CN)
 */
function WeekView({
  weekStart,
  todayIso,
  slotsByDay,
  onAddClick,
  onClose,
  onReopen,
}: {
  weekStart: Date;
  todayIso: string;
  slotsByDay: Map<string, ScheduleSlotResponse[]>;
  onAddClick: (dateIso: string) => void;
  onClose: (s: ScheduleSlotResponse, force: boolean) => void | Promise<void>;
  onReopen: (s: ScheduleSlotResponse) => void | Promise<void>;
}) {
  return (
    <div className="grid grid-cols-7 gap-3">
      {Array.from({ length: 7 }).map((_, i) => {
        const date = addDays(weekStart, i);
        const dateIso = isoDate(date);
        const daySlots = slotsByDay.get(dateIso) ?? [];
        const isPast = dateIso < todayIso;
        return (
          <DayColumn
            key={dateIso}
            dateIso={dateIso}
            weekdayLabel={WEEKDAY_LABELS_VI[i % 7]}
            slots={daySlots}
            isPast={isPast}
            onAddClick={onAddClick}
            onClose={onClose}
            onReopen={onReopen}
          />
        );
      })}
    </div>
  );
}

function DayColumn({
  dateIso,
  weekdayLabel,
  slots,
  isPast,
  onAddClick,
  onClose,
  onReopen,
}: {
  dateIso: string;
  weekdayLabel: string;
  slots: ScheduleSlotResponse[];
  isPast: boolean;
  onAddClick: (dateIso: string) => void;
  onClose: (s: ScheduleSlotResponse, force: boolean) => void | Promise<void>;
  onReopen: (s: ScheduleSlotResponse) => void | Promise<void>;
}) {
  // Highlight T7, CN
  const isWeekend = weekdayLabel === "T7" || weekdayLabel === "CN";
  const dayNum = Number(dateIso.slice(8, 10));

  return (
    <div className={`min-h-[280px] rounded border p-2 ${isWeekend ? "border-amber-200 bg-amber-50/30" : "border-slate-200 bg-white"}`}>
      <div className="mb-1 text-center">
        <div className={`text-xs font-semibold ${isWeekend ? "text-amber-600" : "text-slate-500"}`}>
          {weekdayLabel}
        </div>
        <div className={`text-base ${isPast ? "text-slate-400" : "text-slate-700"}`}>
          {dayNum}
        </div>
      </div>
      <div className="space-y-1">
        {slots.length === 0 && (
          <div className={`rounded border border-dashed p-2 text-center text-xs ${isWeekend ? "border-amber-200 text-amber-400" : "border-slate-200 text-slate-400"}`}>
            {isPast ? "Qua" : "—"}
          </div>
        )}
        {slots.map((s) => (
          <SlotCard
            key={s.slotId}
            slot={s}
            onClose={() => void onClose(s, false)}
            onReopen={() => void onReopen(s)}
          />
        ))}
      </div>
      {!isPast && (
        <button
          type="button"
          onClick={() => onAddClick(dateIso)}
          className="mt-1 w-full rounded border border-dashed border-slate-300 p-1 text-xs text-slate-400 hover:border-blue-400 hover:text-blue-500"
        >
          + Thêm
        </button>
      )}
    </div>
  );
}

function SlotCard({
  slot,
  onClose,
  onReopen,
}: {
  slot: ScheduleSlotResponse;
  onClose: () => void;
  onReopen: () => void;
}) {
  return (
    <div className={`rounded border p-2 text-xs ${slot.status === "CLOSED" ? "border-slate-300 bg-slate-50" : "border-slate-200 bg-white"}`}>
      <div className="flex items-start justify-between gap-0.5">
        <span className="font-mono text-sm font-medium text-slate-700">
          {slot.startTime.slice(0, 5)}–{slot.endTime.slice(0, 5)}
        </span>
        <span className={`shrink-0 rounded-full px-2 py-1 text-[10px] ${STATUS_STYLES[slot.status]}`}>
          {STATUS_LABELS[slot.status]}
        </span>
      </div>

      {/* Hiển thị tên bệnh nhân đã book */}
      {slot.bookedAppointments && slot.bookedAppointments.length > 0 && (
        <div className="mt-0.5 space-y-0.5">
          {slot.bookedAppointments.map((apt) => (
            <div key={apt.appointmentId} className="text-blue-700 truncate text-xs" title={apt.reason ?? undefined}>
              👤 {apt.patientFullName}
            </div>
          ))}
        </div>
      )}

      <div className="mt-1 flex gap-0.5">
        {slot.status === "BOOKED" && (
          <span className="flex-1 rounded border border-blue-200 bg-blue-50 px-2 py-1 text-center text-xs text-blue-600">
            Đã đặt
          </span>
        )}
        {slot.status === "OPEN" && (
          <button
            type="button"
            onClick={onClose}
            className="flex-1 rounded border border-red-200 bg-red-50 px-2 py-1 text-xs text-red-600 hover:bg-red-100"
            title="Đóng"
          >
            Đóng ca
          </button>
        )}
        {slot.status === "CLOSED" && (
          <button
            type="button"
            onClick={onReopen}
            className="flex-1 rounded border border-green-200 bg-green-50 px-2 py-1 text-xs text-green-600 hover:bg-green-100"
          >
            Mở lại
          </button>
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
          Range &gt; 15 phút, ngày + giờ &gt; hiện tại, không trùng.
        </p>
        <ModalActions submitting={submitting} onClose={onClose} submitLabel="Tạo khung giờ" />
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
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-semibold">{title}</h2>
          <button
            type="button"
            onClick={onClose}
            className="rounded p-1 hover:bg-slate-100"
          >
            ✕
          </button>
        </div>
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

function ConfirmModal({
  title,
  message,
  confirmLabel = "Xác nhận",
  cancelLabel = "Hủy",
  variant = "danger", // "danger" | "warning" | "info"
  onConfirm,
  onCancel,
}: {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: "danger" | "warning" | "info";
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const variantStyles = {
    danger: {
      icon: "text-red-500",
      iconBg: "bg-red-100",
      confirmBtn: "bg-red-600 hover:bg-red-700",
    },
    warning: {
      icon: "text-amber-500",
      iconBg: "bg-amber-100",
      confirmBtn: "bg-amber-600 hover:bg-amber-700",
    },
    info: {
      icon: "text-blue-500",
      iconBg: "bg-blue-100",
      confirmBtn: "bg-blue-600 hover:bg-blue-700",
    },
  };
  const styles = variantStyles[variant];

  return (
    <ModalShell title="" onClose={onCancel}>
      <div className="flex items-start gap-4">
        <div className={`shrink-0 rounded-full p-3 ${styles.iconBg}`}>
          {variant === "danger" && (
            <svg className={`h-6 w-6 ${styles.icon}`} fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
          )}
          {variant === "warning" && (
            <svg className={`h-6 w-6 ${styles.icon}`} fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          )}
          {variant === "info" && (
            <svg className={`h-6 w-6 ${styles.icon}`} fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          )}
        </div>
        <div className="flex-1">
          <h3 className="text-lg font-semibold text-slate-900">{title}</h3>
          <p className="mt-2 text-sm text-slate-600">{message}</p>
        </div>
      </div>
      <div className="mt-6 flex justify-end gap-3">
        <button
          type="button"
          onClick={onCancel}
          className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
        >
          {cancelLabel}
        </button>
        <button
          type="button"
          onClick={onConfirm}
          className={`rounded-md px-4 py-2 text-sm font-medium text-white ${styles.confirmBtn}`}
        >
          {confirmLabel}
        </button>
      </div>
    </ModalShell>
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

function mondayOfWeek(d: Date): Date {
  const dow = d.getDay();
  const offset = dow === 0 ? -6 : 1 - dow;
  return addDays(d, offset);
}

function sundayOfWeek(d: Date): Date {
  // d là T2, cộng thêm 6 ngày để ra CN
  return addDays(d, 6);
}
