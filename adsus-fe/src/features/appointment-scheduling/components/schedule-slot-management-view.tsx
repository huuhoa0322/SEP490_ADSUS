"use client";

import { ChevronLeft, ChevronRight, Loader2, Plus, X } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import toast from "react-hot-toast";

import { getApiErrorMessage } from "@/lib/api-client";

import {
  useCloseScheduleSlot,
  useCreateOvertimeSlots,
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
  const createOvertimeMutation = useCreateOvertimeSlots();

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
        <CreateOvertimeModal
          defaultDate={defaultDate}
          onClose={() => setShowCreate(false)}
          onSubmit={async (visitDate) => {
            try {
              const res = await createOvertimeMutation.mutateAsync(visitDate);
              setShowCreate(false);
              await listQuery.refetch();
              if (res.successCount > 0) {
                toast.success(`Đã tạo thành công ${res.successCount} ca khám ngoài giờ.`);
              }
              if (res.errorCount > 0) {
                toast.error(`${res.errorCount} ca bị lỗi (có thể do trùng lặp hoặc đã qua giờ).`);
              }
            } catch (err) {
              toast.error(getApiErrorMessage(err, "Không tạo được ca khám ngoài giờ."));
            }
          }}
          submitting={createOvertimeMutation.isPending}
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
    <div className="grid min-h-[600px] grid-cols-7 gap-3">
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
            isPastDay={isPast}
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
  isPastDay,
  onAddClick,
  onClose,
  onReopen,
}: {
  dateIso: string;
  weekdayLabel: string;
  slots: ScheduleSlotResponse[];
  isPastDay: boolean;
  onAddClick: (dateIso: string) => void;
  onClose: (s: ScheduleSlotResponse, force: boolean) => void | Promise<void>;
  onReopen: (s: ScheduleSlotResponse) => void | Promise<void>;
}) {
  const isWeekend = weekdayLabel === "T7" || weekdayLabel === "CN";

  const date = new Date(dateIso);
  const dayOfMonth = date.getDate().toString().padStart(2, "0");
  const month = (date.getMonth() + 1).toString().padStart(2, "0");

  const now = new Date();
  const currentIsoDate = isoDate(now);
  const currentTimeStr = now.toTimeString().slice(0, 8);
  const isToday = dateIso === currentIsoDate;

  // Check if this day already has overtime slots (starts at or after 17:00)
  const hasOvertime = slots.some((s) => s.startTime >= "17:00:00" || s.startTime >= "17:00");

  return (
    <div className={`flex min-h-[280px] flex-col rounded border p-2 ${isWeekend ? "border-amber-200 bg-amber-50/30" : "border-slate-200 bg-white"}`}>
      {/* Header with weekday + date */}
      <div className="mb-2 text-center">
        <div className={`text-sm font-semibold ${isWeekend ? "text-amber-600" : "text-slate-500"}`}>
          {weekdayLabel} ({dayOfMonth}/{month})
        </div>
      </div>

      {/* Slots container - fixed height, scrollable if needed */}
      <div className="flex-1 space-y-1">
        {slots.length === 0 && (
          <div className={`rounded border border-dashed p-2 text-center text-xs ${isWeekend ? "border-amber-200 text-amber-400" : "border-slate-200 text-slate-400"}`}>
            {isPastDay ? "Qua" : "—"}
          </div>
        )}
        {slots.map((s) => {
          const isPastSlot = isPastDay || (isToday && s.startTime < currentTimeStr);
          return (
            <SlotCard
              key={s.slotId}
              slot={s}
              isPast={isPastSlot}
              onClose={() => void onClose(s, false)}
              onReopen={() => void onReopen(s)}
            />
          );
        })}
      </div>

      {/* Add button - always at bottom */}
      {!isPastDay && !hasOvertime && (
        <button
          type="button"
          onClick={() => onAddClick(dateIso)}
          className="mt-2 w-full rounded bg-blue-50 border border-blue-200 p-2 text-sm font-semibold text-blue-600 hover:bg-blue-100 hover:border-blue-300 transition-colors shadow-sm"
        >
          + Tăng ca
        </button>
      )}
    </div>
  );
}

function SlotCard({
  slot,
  isPast,
  onClose,
  onReopen,
}: {
  slot: ScheduleSlotResponse;
  isPast: boolean;
  onClose: () => void;
  onReopen: () => void;
}) {
  const patientName = slot.bookedAppointments?.[0]?.patientFullName;
  const hasBooking = !!patientName;

  const displayStatusLabel = isPast && slot.status === "OPEN" ? "Đã qua" : STATUS_LABELS[slot.status];
  const displayStatusStyle = isPast ? "bg-slate-100 text-slate-500" : STATUS_STYLES[slot.status];

  return (
    <div className={`flex flex-col rounded border p-2.5 text-sm ${slot.status === "CLOSED" || isPast ? "border-slate-300 bg-slate-50 opacity-80" : "border-slate-200 bg-white"}`}>
      {/* Header: Time range + Status badge */}
      <div className="flex items-start justify-between gap-1">
        <span className="font-mono text-sm font-medium text-slate-600">
          {slot.startTime.slice(0, 5)}–{slot.endTime.slice(0, 5)}
        </span>
        <span className={`shrink-0 rounded-full px-2 py-0.5 text-[11px] font-medium ${displayStatusStyle}`}>
          {displayStatusLabel}
        </span>
      </div>

      {/* Patient name - primary action for BOOKED */}
      <div className="mt-2 flex items-center justify-between gap-1">
        <span className={`flex-1 truncate text-sm ${hasBooking ? "text-blue-700 font-medium" : "text-slate-400"}`}>
          {hasBooking ? patientName : "—"}
        </span>

        {slot.status !== "CLOSED" ? (
          <button
            type="button"
            disabled={isPast}
            onClick={onClose}
            className={`shrink-0 rounded p-1 text-slate-400 hover:bg-red-50 hover:text-red-500 transition-colors ${isPast ? "invisible" : ""}`}
            title="Đóng ca"
          >
            <X className="h-4 w-4" />
          </button>
        ) : (
          <button
            type="button"
            disabled={isPast}
            onClick={onReopen}
            className={`shrink-0 rounded px-2 py-0.5 text-xs font-semibold text-green-600 hover:bg-green-100 transition-colors border border-transparent hover:border-green-200 ${isPast ? "invisible" : ""}`}
          >
            Mở lại
          </button>
        )}
      </div>
    </div>
  );
}

function CreateOvertimeModal({
  defaultDate,
  onClose,
  onSubmit,
  submitting,
}: {
  defaultDate: string;
  onClose: () => void;
  onSubmit: (visitDate: string) => void | Promise<void>;
  submitting: boolean;
}) {
  const [visitDate, setVisitDate] = useState(defaultDate);

  const overtimeSlots = [
    { startTime: "17:00:00", endTime: "17:30:00" },
    { startTime: "17:30:00", endTime: "18:00:00" },
    { startTime: "18:00:00", endTime: "18:30:00" },
    { startTime: "18:30:00", endTime: "19:00:00" },
    { startTime: "19:00:00", endTime: "19:30:00" },
    { startTime: "19:30:00", endTime: "20:00:00" },
  ];

  return (
    <ModalShell title="Đăng ký khám ngoài giờ" onClose={onClose}>
      <form
        className="space-y-4"
        onSubmit={(e) => {
          e.preventDefault();
          void onSubmit(visitDate);
        }}
      >
        <label className="block text-sm">
          <span className="text-slate-600">Ngày khám</span>
          <input
            readOnly
            type="date"
            value={visitDate}
            className="mt-1 w-full rounded-md border border-slate-300 bg-slate-50 px-3 py-2 text-slate-500 cursor-not-allowed"
          />
        </label>

        <div>
          <p className="text-sm font-medium text-slate-700">Các ca sẽ được tạo (17:00 - 20:00):</p>
          <div className="mt-2 grid grid-cols-2 gap-2">
            {overtimeSlots.map((s, idx) => (
              <div key={idx} className="rounded border border-slate-200 bg-slate-50 p-2 text-center text-xs font-mono text-slate-600">
                {s.startTime.slice(0, 5)} - {s.endTime.slice(0, 5)}
              </div>
            ))}
          </div>
        </div>

        <ModalActions submitting={submitting} onClose={onClose} submitLabel="Xác nhận tăng ca" />
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
