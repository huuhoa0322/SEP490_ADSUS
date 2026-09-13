"use client";

import { useState, useMemo } from "react";
import { useRouter } from "next/navigation";
import {
  addDays,
  addMonths,
  format,
  isBefore,
  isSameDay,
  startOfDay,
  startOfMonth,
  subMonths,
} from "date-fns";
import { vi } from "date-fns/locale";
import { Calendar, ChevronLeft, ChevronRight, Clock, User } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { Textarea } from "@/components/ui/textarea";
import { Skeleton } from "@/components/ui/skeleton";
import { getApiErrorMessage } from "@/lib/api-client";
import { useAuthStore } from "@/store/auth-store";
import { useBookAppointment, useOpenSlots } from "../hooks/use-booking";
import type { BookAppointmentRequest, OpenSlotResponse } from "../types/booking.types";
import toast from "react-hot-toast";

const TODAY = startOfDay(new Date());
const TWO_WEEKS_LATER = addDays(TODAY, 14);

interface BookingViewProps {
  /** Nếu true: guest bấm "Đặt lịch" sẽ gọi onGuestBookAttempt thay vì submit thật. */
  requireAuth?: boolean;
  onGuestBookAttempt?: () => void;
}

interface DayGroup {
  date: Date;
  slots: OpenSlotResponse[];
}

function groupByDate(slots: OpenSlotResponse[]): DayGroup[] {
  const map = new Map<string, OpenSlotResponse[]>();
  for (const slot of slots) {
    const key = slot.slotDate;
    if (!map.has(key)) map.set(key, []);
    map.get(key)!.push(slot);
  }
  return Array.from(map.entries())
    .map(([slotDate, daySlots]) => ({
      date: new Date(slotDate + "T00:00:00"),
      slots: daySlots.sort((a, b) => a.startTime.localeCompare(b.startTime)),
    }))
    .sort((a, b) => a.date.getTime() - b.date.getTime());
}

function buildCalendarDays(month: Date): (Date | null)[] {
  const first = startOfMonth(month);
  const startDay = (first.getDay() + 1) % 7; // 0=Mon in our calendar
  const days: (Date | null)[] = [];
  for (let i = 0; i < startDay; i++) days.push(null);
  const lastDay = new Date(month.getFullYear(), month.getMonth() + 1, 0).getDate();
  for (let d = 1; d <= lastDay; d++) {
    days.push(new Date(month.getFullYear(), month.getMonth(), d));
  }
  return days;
}

/** Calendar mini view — chọn ngày → hiện slot bên phải */
function SlotCalendar({
  groups,
  selectedDate,
  onSelectDate,
}: {
  groups: DayGroup[];
  selectedDate: Date | null;
  onSelectDate: (date: Date) => void;
}) {
  const [month, setMonth] = useState(startOfMonth(TODAY));
  const days = useMemo(() => buildCalendarDays(month), [month]);

  const hasSlots = (date: Date) =>
    groups.some((g) => isSameDay(g.date, date));

  const isPast = (date: Date) => isBefore(date, TODAY);
  const isToday = (date: Date) => isSameDay(date, TODAY);
  const isSelected = (date: Date) => selectedDate && isSameDay(date, selectedDate);
  const isBeyondLimit = (date: Date) => isBefore(TWO_WEEKS_LATER, date);

  return (
    <Card>
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between">
          <Button
            variant="ghost"
            size="icon"
            onClick={() => setMonth(subMonths(month, 1))}
            disabled={isBefore(startOfMonth(month), startOfMonth(TODAY))}
          >
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <span className="font-semibold">
            {format(month, "MMMM yyyy", { locale: vi })}
          </span>
          <Button
            variant="ghost"
            size="icon"
            onClick={() => setMonth(addMonths(month, 1))}
            disabled={isBefore(TWO_WEEKS_LATER, startOfMonth(month))}
          >
            <ChevronRight className="h-4 w-4" />
          </Button>
        </div>
        <div className="mt-3 grid grid-cols-7 gap-1 text-center text-xs font-medium text-muted-foreground">
          {["T2", "T3", "T4", "T5", "T6", "T7", "CN"].map((d) => (
            <div key={d}>{d}</div>
          ))}
        </div>
      </CardHeader>
      <CardContent className="pt-0">
        <div className="grid grid-cols-7 gap-1">
          {days.map((day, idx) => {
            if (!day) return <div key={`empty-${idx}`} />;
            const past = isPast(day);
            const beyond = isBeyondLimit(day);
            const disabled = past || beyond;
            const selected = isSelected(day);
            const dot = hasSlots(day);

            return (
              <button
                key={day.toISOString()}
                disabled={disabled}
                onClick={() => !disabled && onSelectDate(day)}
                className={`
                  relative flex h-9 w-9 items-center justify-center rounded-full text-sm
                  transition-colors
                  ${disabled ? "text-muted-foreground/40 cursor-default" : "hover:bg-lp-teal-tint cursor-pointer"}
                  ${selected ? "bg-lp-teal text-white hover:bg-lp-teal-h" : ""}
                  ${isToday(day) && !selected ? "ring-1 ring-lp-teal" : ""}
                `}
              >
                {format(day, "d")}
                {dot && !selected && (
                  <span className="absolute bottom-1 left-1/2 -translate-x-1/2 h-1 w-1 rounded-full bg-lp-teal" />
                )}
              </button>
            );
          })}
        </div>
      </CardContent>
    </Card>
  );
}

/** Danh sách slot của 1 ngày */
function SlotList({
  slots,
  onSelectSlot,
  isLoading,
}: {
  slots: OpenSlotResponse[];
  onSelectSlot: (slot: OpenSlotResponse) => void;
  isLoading: boolean;
}) {
  if (isLoading) {
    return (
      <div className="space-y-3">
        {[1, 2, 3].map((i) => (
          <Skeleton key={i} className="h-20 w-full" />
        ))}
      </div>
    );
  }

  if (slots.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center rounded-lg border border-dashed py-12 text-center text-muted-foreground">
        <Calendar className="mb-3 h-10 w-10 text-muted-foreground/50" />
        <p className="text-sm">Không có khung giờ trống nào cho ngày này.</p>
        <p className="mt-1 text-xs">Vui lòng chọn ngày khác.</p>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      {slots.map((slot) => (
        <button
          key={slot.slotId}
          onClick={() => onSelectSlot(slot)}
          className="w-full rounded-lg border border-lp-border bg-white p-4 text-left transition-all hover:border-lp-teal hover:shadow-sm hover:ring-1 hover:ring-lp-teal/30"
        >
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-lp-teal-tint">
              <Clock className="h-5 w-5 text-lp-teal" />
            </div>
            <div className="flex-1 min-w-0">
              <p className="font-semibold text-lp-text">
                {slot.startTime.slice(0, 5)} – {slot.endTime.slice(0, 5)}
              </p>
              <div className="mt-0.5 flex items-center gap-1.5 text-sm text-muted-foreground">
                <User className="h-3.5 w-3.5 shrink-0" />
                <span className="truncate">BS. {slot.doctorName}</span>
              </div>
            </div>
            <span className="shrink-0 rounded-full bg-lp-teal-tint px-2.5 py-1 text-xs font-medium text-lp-teal">
              Còn trống
            </span>
          </div>
        </button>
      ))}
    </div>
  );
}

/** Form xác nhận đặt lịch */
function BookingConfirmDialog({
  slot,
  open,
  onOpenChange,
  onConfirm,
  isPending,
}: {
  slot: OpenSlotResponse | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (reason: string) => void;
  isPending: boolean;
}) {
  const [reason, setReason] = useState("");

  const handleConfirm = () => {
    onConfirm(reason);
  };

  const handleOpenChange = (val: boolean) => {
    if (!val) setReason("");
    onOpenChange(val);
  };

  if (!slot) return null;

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Xác nhận đặt lịch khám</DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-3">
          {/* Slot info */}
          <div className="rounded-lg bg-lp-canvas p-4 space-y-2">
            <div className="flex items-center gap-2 text-sm">
              <Calendar className="h-4 w-4 text-muted-foreground" />
              <span className="font-medium">
                {format(new Date(slot.slotDate + "T00:00:00"), "EEEE, dd/MM/yyyy", { locale: vi })}
              </span>
            </div>
            <div className="flex items-center gap-2 text-sm">
              <Clock className="h-4 w-4 text-muted-foreground" />
              <span className="font-medium">{slot.startTime.slice(0, 5)} – {slot.endTime.slice(0, 5)}</span>
            </div>
            <div className="flex items-center gap-2 text-sm">
              <User className="h-4 w-4 text-muted-foreground" />
              <span className="font-medium">BS. {slot.doctorName}</span>
            </div>
          </div>

          {/* Reason input */}
          <div className="space-y-2">
            <label className="text-sm font-medium text-foreground">
              Lý do khám <span className="text-muted-foreground font-normal">(tùy chọn)</span>
            </label>
            <Textarea
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="VD: Khám thai định kỳ, siêu âm 4D..."
              rows={3}
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => handleOpenChange(false)}>
            Hủy
          </Button>
          <Button onClick={handleConfirm} disabled={isPending}>
            {isPending ? "Đang đặt lịch..." : "Xác nhận đặt lịch"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** Toast thành công sau khi đặt lịch */
function BookingSuccessDialog({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <div className="flex flex-col items-center py-6 text-center">
          <div className="mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-green-100">
            <svg className="h-7 w-7 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M5 13l4 4L19 7" />
            </svg>
          </div>
          <h3 className="text-lg font-semibold text-foreground">Đặt lịch thành công!</h3>
          <p className="mt-2 text-sm text-muted-foreground">
            Lịch hẹn của bạn đã được ghi nhận. Vui lòng đến phòng khám đúng giờ và mang theo
            giấy tờ tùy thân.
          </p>
          <p className="mt-2 text-xs text-muted-foreground">
            Để xem chi tiết và nhận thông báo về lịch hẹn, hãy truy cập app ADSUS trên mobile.
          </p>
          <Button className="mt-5 w-full" onClick={() => onOpenChange(false)}>
            Quay về trang chủ
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}

export function BookingView({ requireAuth, onGuestBookAttempt }: BookingViewProps = {}) {
  const router = useRouter();
  const [selectedDate, setSelectedDate] = useState<Date | null>(TODAY);
  const [selectedSlot, setSelectedSlot] = useState<OpenSlotResponse | null>(null);
  const [isConfirmOpen, setIsConfirmOpen] = useState(false);
  const [isSuccessOpen, setIsSuccessOpen] = useState(false);

  const fromDate = format(TODAY, "yyyy-MM-dd");
  const toDate = format(TWO_WEEKS_LATER, "yyyy-MM-dd");

  const { data: allSlots, isLoading } = useOpenSlots({ fromDate, toDate });
  const bookMutation = useBookAppointment();

  const groups = useMemo(() => groupByDate(allSlots ?? []), [allSlots]);

  // Auto-select first available date
  const handleDateSelect = (date: Date) => {
    setSelectedDate(date);
    setSelectedSlot(null);
  };

  const currentGroup = useMemo(
    () => groups.find((g) => selectedDate && isSameDay(g.date, selectedDate)),
    [groups, selectedDate],
  );

  const handleSlotSelect = (slot: OpenSlotResponse) => {
    setSelectedSlot(slot);
    setIsConfirmOpen(true);
  };

  const handleConfirm = async (reason: string) => {
    if (!selectedSlot) return;

    if (requireAuth) {
      onGuestBookAttempt?.();
      return;
    }

    // Safety: double-check Patient role (wrapper đã check, nhưng thêm ở đây phòng ngừa)
    const role = useAuthStore.getState().user?.role;
    if (role !== "PATIENT") {
      toast.error("Bạn cần đăng nhập với tài khoản bệnh nhân để đặt lịch.");
      return;
    }

    const request: BookAppointmentRequest = {
      scheduleSlotId: selectedSlot.slotId,
      reason: reason || undefined,
    };

    try {
      await bookMutation.mutateAsync(request);
      setIsConfirmOpen(false);
      setIsSuccessOpen(true);
    } catch (err) {
      toast.error(getApiErrorMessage(err, "Đặt lịch thất bại. Vui lòng thử lại."));
    }
  };

  const handleSuccessClose = () => {
    setIsSuccessOpen(false);
    setSelectedSlot(null);
    setSelectedDate(TODAY);
    router.push("/");
  };

  return (
    <div className="mx-auto w-full max-w-screen-xl px-6 py-8">
      <header className="mb-8">
        <h1 className="font-heading text-3xl font-semibold text-lp-navy">Đặt lịch khám</h1>
        <p className="mt-2 text-muted-foreground">
          Chọn ngày và khung giờ phù hợp với bạn. Có thể đặt trước tối đa 2 tuần.
        </p>
      </header>

      <div className="grid gap-8 lg:grid-cols-[320px_1fr]">
        {/* Cột trái: lịch */}
        <div className="space-y-4">
          <SlotCalendar
            groups={groups}
            selectedDate={selectedDate}
            onSelectDate={handleDateSelect}
          />

          {/* Legend */}
          <div className="flex items-center gap-4 text-xs text-muted-foreground">
            <span className="flex items-center gap-1.5">
              <span className="h-2 w-2 rounded-full bg-lp-teal" /> Có lịch trống
            </span>
            <span className="flex items-center gap-1.5">
              <span className="h-4 w-4 rounded border border-lp-teal" /> Hôm nay
            </span>
          </div>
        </div>

        {/* Cột phải: danh sách slot */}
        <div>
          <div className="mb-4 flex items-center justify-between">
            <h2 className="text-lg font-semibold text-foreground">
              {selectedDate
                ? format(selectedDate, "EEEE, dd/MM/yyyy", { locale: vi })
                : "Chọn ngày"}
            </h2>
            {currentGroup && (
              <span className="rounded-full bg-lp-teal-tint px-3 py-1 text-xs font-medium text-lp-teal">
                {currentGroup.slots.length} khung giờ
              </span>
            )}
          </div>

          {selectedDate ? (
            <SlotList
              slots={currentGroup?.slots ?? []}
              onSelectSlot={handleSlotSelect}
              isLoading={isLoading}
            />
          ) : (
            <div className="flex flex-col items-center justify-center rounded-lg border border-dashed py-16 text-center text-muted-foreground">
              <Calendar className="mb-3 h-10 w-10 text-muted-foreground/50" />
              <p>Vui lòng chọn một ngày bên trái để xem khung giờ trống.</p>
            </div>
          )}
        </div>
      </div>

      <BookingConfirmDialog
        slot={selectedSlot}
        open={isConfirmOpen}
        onOpenChange={setIsConfirmOpen}
        onConfirm={handleConfirm}
        isPending={bookMutation.isPending}
      />

      <BookingSuccessDialog
        open={isSuccessOpen}
        onOpenChange={handleSuccessClose}
      />
    </div>
  );
}
