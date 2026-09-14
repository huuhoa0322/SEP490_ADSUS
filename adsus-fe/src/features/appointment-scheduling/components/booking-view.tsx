"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Loader2 } from "lucide-react";
import toast from "react-hot-toast";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Textarea } from "@/components/ui/textarea";
import { getApiErrorMessage } from "@/lib/api-client";
import { useAuthStore } from "@/store/auth-store";
import { useBookAppointment } from "../hooks/use-booking";
import { useBookingForm } from "../hooks/use-booking-form";
import { useRelatives } from "../hooks/use-relatives";
import type { BookAppointmentRequest, SymptomInput } from "../types/booking.types";

import { DoctorFilterSection } from "./doctor-filter-section";
import { WeekSelector } from "./week-selector";
import { DateSelector } from "./date-selector";
import { SlotGrid } from "./slot-grid";
import { SymptomSection } from "./symptom-section";
import { RelativeSection } from "./relative-section";

interface BookingViewProps {
  /** Nếu true: guest bấm "Đặt lịch" sẽ gọi onGuestBookAttempt thay vì submit thật. */
  requireAuth?: boolean;
  onGuestBookAttempt?: () => void;
  onSwitchToAddRelative?: () => void;
}

/** Toast dialog thành công sau khi đặt lịch */
function BookingSuccessDialog({
  open,
  onOpenChange,
  onFinish,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onFinish: () => void;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <div className="flex flex-col items-center py-6 text-center">
          <div className="mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-emerald-100 dark:bg-emerald-950/50">
            <svg
              className="h-7 w-7 text-emerald-600 dark:text-emerald-400"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M5 13l4 4L19 7" />
            </svg>
          </div>
          <h3 className="text-lg font-semibold text-foreground font-heading">
            Đặt lịch thành công!
          </h3>
          <p className="mt-2 text-sm text-muted-foreground">
            Lịch hẹn của bạn đã được ghi nhận vào hệ thống. Vui lòng đến phòng khám đúng giờ và mang theo giấy tờ tùy thân.
          </p>
          <p className="mt-2 text-xs text-muted-foreground">
            Để theo dõi lịch hẹn và nhận thông báo nhắc lịch, hãy kiểm tra mục hồ sơ hoặc ứng dụng di động ADSUS.
          </p>
          <Button className="mt-5 w-full font-semibold" onClick={onFinish}>
            Quay về trang chủ
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}

export function BookingView({
  requireAuth,
  onGuestBookAttempt,
  onSwitchToAddRelative,
}: BookingViewProps = {}) {
  const router = useRouter();
  const [isSuccessOpen, setIsSuccessOpen] = useState(false);
  const [isConfirmResetOpen, setIsConfirmResetOpen] = useState(false);
  const [pendingChange, setPendingChange] = useState<(() => void) | null>(null);

  const form = useBookingForm();
  const bookMutation = useBookAppointment();
  const { data: relatives = [], isLoading: isLoadingRelatives } = useRelatives(
    !form.isBookingForSelf
  );

  const hasEnteredData = Boolean(form.reason.trim()) || form.symptoms.length > 0;

  const handleChangeIsBookingForSelf = (forSelf: boolean) => {
    if (forSelf === form.isBookingForSelf) return;

    if (hasEnteredData) {
      setPendingChange(() => () => {
        form.setIsBookingForSelf(forSelf);
      });
      setIsConfirmResetOpen(true);
    } else {
      form.setIsBookingForSelf(forSelf);
    }
  };

  const handleSelectRelative = (relativeId: string | null) => {
    if (relativeId === form.selectedRelativeId) return;

    if (hasEnteredData) {
      setPendingChange(() => () => {
        form.setSelectedRelativeId(relativeId);
      });
      setIsConfirmResetOpen(true);
    } else {
      form.setSelectedRelativeId(relativeId);
    }
  };

  const handleConfirmReset = () => {
    form.setReason("");
    form.setSymptoms([]);
    if (pendingChange) {
      pendingChange();
      setPendingChange(null);
    }
    setIsConfirmResetOpen(false);
  };

  const handleCancelReset = () => {
    setPendingChange(null);
    setIsConfirmResetOpen(false);
  };

  const handleSubmit = async () => {
    if (!form.selectedSlotId) return;

    if (requireAuth) {
      onGuestBookAttempt?.();
      return;
    }

    // Role check safety
    const role = useAuthStore.getState().user?.role;
    if (role !== "PATIENT") {
      toast.error("Bạn cần đăng nhập với tài khoản bệnh nhân để đặt lịch.");
      return;
    }

    if (!form.isBookingForSelf && !form.selectedRelativeId) {
      toast.error("Vui lòng chọn người thân trước khi xác nhận đặt lịch.");
      return;
    }

    // Lọc bỏ các block triệu chứng rỗng (không có categoryId, hoặc không có symptomId và không có ghi chú)
    const validSymptoms: SymptomInput[] = form.symptoms
      .filter(
        (s) =>
          Boolean(s.categoryId?.trim()) &&
          (Boolean(s.symptomId?.trim()) || Boolean(s.otherNote?.trim()))
      )
      .map((s) => ({
        categoryId: s.categoryId.trim(),
        symptomId: s.symptomId?.trim() || null,
        otherNote: s.otherNote?.trim() || null,
      }));

    const request: BookAppointmentRequest = {
      scheduleSlotId: form.selectedSlotId,
      reason: form.reason.trim() || undefined,
      symptoms: validSymptoms.length > 0 ? validSymptoms : undefined,
      relationshipId: form.isBookingForSelf ? undefined : (form.selectedRelativeId ?? undefined),
    };

    try {
      await bookMutation.mutateAsync(request);
      setIsSuccessOpen(true);
      form.resetForm();
    } catch (err) {
      toast.error(getApiErrorMessage(err, "Đặt lịch thất bại. Vui lòng thử lại."));
    }
  };

  const handleSuccessFinish = () => {
    setIsSuccessOpen(false);
    router.push("/");
  };

  return (
    <div className="space-y-8">
      {/* 1. Header */}
      <div>
        <h1 className="font-heading text-2xl sm:text-3xl font-bold text-foreground">
          Đặt lịch khám trực tuyến
        </h1>
        <p className="mt-1.5 text-sm text-muted-foreground">
          Chọn bác sĩ, tuần và thời gian phù hợp với nhu cầu của bạn. Có thể đặt trước tối đa 30 ngày.
        </p>
      </div>

      {/* 2. Relative Booking Section (Bước 1: Chọn người khám) */}
      <RelativeSection
        isBookingForSelf={form.isBookingForSelf}
        onChangeIsBookingForSelf={handleChangeIsBookingForSelf}
        relatives={relatives}
        isLoadingRelatives={isLoadingRelatives}
        selectedRelativeId={form.selectedRelativeId}
        onSelectRelative={handleSelectRelative}
        onSwitchToAddRelative={() => onSwitchToAddRelative?.()}
      />

      {/* 3. Doctor Filter Section */}
      <DoctorFilterSection
        selectedGender={form.selectedDoctorGender}
        onSelectGender={form.selectDoctorGender}
        doctors={form.filteredDoctorOptions}
        selectedDoctorId={form.selectedDoctorId}
        onSelectDoctor={form.selectDoctor}
      />

      {/* 4. Week Selector */}
      <WeekSelector
        selectedWeekIndex={form.selectedWeekIndex}
        onSelectWeek={form.selectWeek}
      />

      {/* 5. Date Selector */}
      <DateSelector
        displayDates={form.displayDates}
        selectedDate={form.selectedDate}
        onSelectDate={form.selectDate}
        selectedWeekIndex={form.selectedWeekIndex}
      />

      {/* 6. Slot Grid */}
      <SlotGrid
        selectedDoctorId={form.selectedDoctorId}
        selectedDate={form.selectedDate}
        slots={form.visibleSlots}
        selectedSlotId={form.selectedSlotId}
        onSelectSlot={form.selectSlot}
        isLoading={form.isLoadingSlots}
      />

      {/* 7. Reason Section */}
      <div className="space-y-2">
        <label
          htmlFor="booking-reason"
          className="text-xs font-bold tracking-wider text-muted-foreground uppercase"
        >
          Lý do khám (tùy chọn)
        </label>
        <Textarea
          id="booking-reason"
          value={form.reason}
          onChange={(e) => form.setReason(e.target.value)}
          placeholder="Ví dụ: Đau hạ vị 2 ngày nay, rong kinh nhẹ, kiểm tra thai định kỳ..."
          rows={3}
          className="resize-none"
        />
      </div>

      {/* 8. Symptom Section */}
      <SymptomSection
        symptoms={form.symptoms}
        onChange={form.setSymptoms}
        isExpanded={form.isSymptomSectionExpanded}
        onToggle={() => form.setIsSymptomSectionExpanded((prev) => !prev)}
      />

      {/* 9. Confirm Button */}
      <div className="pt-2">
        <Button
          type="button"
          onClick={handleSubmit}
          disabled={!form.selectedSlotId || bookMutation.isPending}
          className="w-full h-12 text-base font-semibold shadow-sm"
        >
          {bookMutation.isPending ? (
            <>
              <Loader2 className="mr-2 h-5 w-5 animate-spin" />
              Đang đặt lịch...
            </>
          ) : (
            "Xác nhận đặt lịch khám"
          )}
        </Button>
      </div>

      {/* Dialog xác nhận đổi người khám */}
      <Dialog
        open={isConfirmResetOpen}
        onOpenChange={(open) => {
          if (!open) handleCancelReset();
          else setIsConfirmResetOpen(true);
        }}
      >
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Xác nhận thay đổi người khám</DialogTitle>
            <DialogDescription className="pt-2 text-sm text-muted-foreground">
              Thay đổi người khám sẽ làm mới lý do khám và triệu chứng để đảm bảo hồ sơ y tế chính xác. Bạn có muốn tiếp tục?
            </DialogDescription>
          </DialogHeader>
          <DialogFooter className="flex flex-col-reverse sm:flex-row gap-2 pt-4">
            <Button
              type="button"
              variant="outline"
              onClick={handleCancelReset}
            >
              Hủy
            </Button>
            <Button
              type="button"
              variant="default"
              onClick={handleConfirmReset}
            >
              Đồng ý
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Dialog thông báo thành công */}
      <BookingSuccessDialog
        open={isSuccessOpen}
        onOpenChange={setIsSuccessOpen}
        onFinish={handleSuccessFinish}
      />
    </div>
  );
}
