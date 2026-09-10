"use client";

import { useState, useMemo } from "react";
import { format } from "date-fns";
import {
  Calendar,
  Clock,
  User,
  Stethoscope,
  FileText,
  Phone,
  AlertCircle,
  Loader2,
  ArrowLeft,
  CalendarClock,
} from "lucide-react";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Checkbox } from "@/components/ui/checkbox";
import { DatePicker } from "@/components/ui/date-picker";
import type {
  CheckinQueueItem,
  DoctorSummary,
  AvailableSlot,
} from "../types/checkin.types";
import {
  useDoctorList,
  useAvailableSlots,
  useRescheduleAppointment,
} from "../hooks/use-reschedule";

export interface AppointmentDetailModalProps {
  isOpen: boolean;
  item: CheckinQueueItem | null;
  onClose: () => void;
  onCheckin?: (item: CheckinQueueItem) => void;
}

interface AppointmentDetailModalInnerProps {
  item: CheckinQueueItem;
  onClose: () => void;
  onCheckin?: (item: CheckinQueueItem) => void;
}

function AppointmentDetailModalInner({
  item,
  onClose,
  onCheckin,
}: AppointmentDetailModalInnerProps) {
  const [mode, setMode] = useState<"view" | "reschedule">("view");

  // Normalized status flags
  const normStatus = (item.status || "").toUpperCase();
  const normCaseStatus = (item.caseStatus || "").toUpperCase();

  const isBooked = normStatus === "BOOKED";
  const isApproved = normStatus === "APPROVED";
  const isCompleted = normStatus === "COMPLETED";
  const isCancelled =
    normStatus === "CANCELLED" ||
    normStatus === "NOSHOW" ||
    normStatus === "NO_SHOW";

  // Check if reschedule is allowed:
  // Scenario 1: BOOKED -> allowed
  // Scenario 2: APPROVED/COMPLETED -> allowed if case is InProgress (not Confirmed or End)
  // Scenario 3: CANCELLED/NO_SHOW -> allowed
  const isCaseEnded =
    normCaseStatus === "CONFIRMED" ||
    normCaseStatus === "END";

  const canReschedule = useMemo(() => {
    if (isBooked) return true;
    if (isApproved || isCompleted) {
      return !isCaseEnded;
    }
    if (isCancelled) return true;
    return false;
  }, [isBooked, isApproved, isCompleted, isCaseEnded, isCancelled]);

  // Queries and mutations
  const { data: doctors = [], isLoading: isLoadingDoctors } = useDoctorList();
  const rescheduleMutation = useRescheduleAppointment();

  // Helper to extract doctor id (supports backend DTO userId or doctorId)
  const getDocId = (doc: DoctorSummary): string => doc.doctorId || doc.userId || "";

  // 1. Doctor state: pre-select existing doctor if matched
  const defaultDoctorId = useMemo(() => {
    if (item.doctorId) return item.doctorId;
    if (doctors.length > 0) {
      const match = doctors.find(
        (d) => d.fullName?.trim().toLowerCase() === item.doctorName?.trim().toLowerCase()
      );
      if (match) {
        return getDocId(match);
      }
      return getDocId(doctors[0]);
    }
    return "";
  }, [item.doctorId, item.doctorName, doctors]);

  const [selectedDoctorIdOverride, setSelectedDoctorIdOverride] = useState<string | null>(null);
  const selectedDoctorId = selectedDoctorIdOverride ?? defaultDoctorId;

  // 2. Date state: max(today, appointment original date)
  const defaultDate = useMemo(() => {
    const todayStr = format(new Date(), "yyyy-MM-dd");
    if (item.slotTime) {
      try {
        const apptDateStr = format(new Date(item.slotTime), "yyyy-MM-dd");
        if (apptDateStr >= todayStr) {
          return apptDateStr;
        }
      } catch {
        return todayStr;
      }
    }
    return todayStr;
  }, [item.slotTime]);

  const [selectedDateOverride, setSelectedDateOverride] = useState<string | null>(null);
  const selectedDate = selectedDateOverride ?? defaultDate;

  // 3. Slot state
  const [selectedSlotId, setSelectedSlotId] = useState<string>("");

  // 4. Reasons state
  const [newReasonOverride, setNewReasonOverride] = useState<string | null>(null);
  const newReason = newReasonOverride ?? (item.reason || "");

  const [rescheduleReason, setRescheduleReason] = useState<string>("");
  const [validationError, setValidationError] = useState<string | null>(null);

  // 5. Smart default for autoCheckin based on the 3 scenarios:
  // Scenario 1 (BOOKED): unchecked (false)
  // Scenario 2 (COMPLETED / APPROVED): checked (true)
  // Scenario 3 (CANCELLED / NO_SHOW): unchecked (false)
  const defaultAutoCheckin = useMemo(() => {
    return normStatus === "COMPLETED" || normStatus === "APPROVED";
  }, [normStatus]);

  const [autoCheckinOverride, setAutoCheckinOverride] = useState<boolean | null>(null);
  const autoCheckin = autoCheckinOverride ?? defaultAutoCheckin;

  // Fetch available slots for the selected doctor and date
  const {
    data: availableSlots = [],
    isLoading: isLoadingSlots,
  } = useAvailableSlots({
    doctorId: selectedDoctorId || undefined,
    fromDate: selectedDate || undefined,
    toDate: selectedDate || undefined,
  });

  const slotDateTime = new Date(item.slotTime);

  const formatSlotLabel = (slot: AvailableSlot): string => {
    const start = slot.startTime ? slot.startTime.slice(0, 5) : "";
    const end = slot.endTime ? slot.endTime.slice(0, 5) : "";
    return `${start} – ${end}`;
  };

  const handleDoctorChange = (doctorId: string) => {
    setSelectedDoctorIdOverride(doctorId);
    setSelectedSlotId("");
  };

  const handleDateChange = (val: string | Date | null | undefined) => {
    if (!val) return;
    const str = typeof val === "string" ? val : format(val, "yyyy-MM-dd");
    setSelectedDateOverride(str);
    setSelectedSlotId("");
  };

  const handleSubmitReschedule = async () => {
    if (!selectedSlotId) {
      setValidationError("Vui lòng chọn khung giờ khám mới.");
      return;
    }

    if (!rescheduleReason.trim()) {
      setValidationError("Vui lòng nhập lý do đổi lịch.");
      return;
    }

    setValidationError(null);

    try {
      await rescheduleMutation.mutateAsync({
        appointmentId: item.appointmentId,
        request: {
          newScheduleSlotId: selectedSlotId,
          rescheduleReason: rescheduleReason.trim(),
          newReason: newReason.trim() || undefined,
          autoCheckin,
        },
      });
      onClose();
    } catch {
      // Error toast is handled in hook onError
    }
  };

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <div className="flex items-center justify-between gap-2 pr-6">
            <DialogTitle>
              {mode === "view" ? "Chi tiết lịch hẹn" : "Đổi lịch hẹn"}
            </DialogTitle>
            {mode === "view" && (
              <div>
                {isBooked && (
                  <Badge variant="soft-warning">Đang chờ check-in</Badge>
                )}
                {isApproved && (
                  <Badge variant="soft-primary">Đã check-in</Badge>
                )}
                {isCompleted && (
                  <Badge variant="soft-success">Đã hoàn thành</Badge>
                )}
                {isCancelled && (
                  <Badge variant="destructive">Đã huỷ / Vắng mặt</Badge>
                )}
                {!isBooked && !isApproved && !isCompleted && !isCancelled && (
                  <Badge variant="outline">{item.status}</Badge>
                )}
              </div>
            )}
          </div>
          {mode === "reschedule" && (
            <p className="text-sm text-muted-foreground">
              Bệnh nhân: <span className="font-semibold text-foreground">{item.patientFullName}</span>
            </p>
          )}
        </DialogHeader>

        {mode === "view" ? (
          /* ================= MODE 1: VIEW DETAILS ================= */
          <div className="space-y-4 py-2">
            <div className="grid grid-cols-2 gap-4 rounded-lg border border-border bg-muted/20 p-4">
              <div className="space-y-1">
                <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
                  <Calendar className="h-3.5 w-3.5" />
                  Ngày hẹn
                </span>
                <p className="font-medium text-foreground">
                  {format(slotDateTime, "dd/MM/yyyy")}
                </p>
              </div>
              <div className="space-y-1">
                <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
                  <Clock className="h-3.5 w-3.5" />
                  Khung giờ
                </span>
                <p className="font-medium text-foreground">
                  {format(slotDateTime, "HH:mm")}
                </p>
              </div>
              <div className="space-y-1">
                <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
                  <Stethoscope className="h-3.5 w-3.5" />
                  Bác sĩ phụ trách
                </span>
                <p className="font-medium text-foreground">{item.doctorName}</p>
              </div>
              <div className="space-y-1">
                <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
                  <User className="h-3.5 w-3.5" />
                  Bệnh nhân
                </span>
                <p className="font-medium text-foreground">{item.patientFullName}</p>
              </div>
              <div className="space-y-1">
                <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
                  <Phone className="h-3.5 w-3.5" />
                  Số điện thoại
                </span>
                <p className="text-foreground">{item.patientPhone || "—"}</p>
              </div>
              <div className="col-span-2 space-y-1 border-t border-border/50 pt-2">
                <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
                  <FileText className="h-3.5 w-3.5" />
                  Lý do khám
                </span>
                <p className="text-foreground">{item.reason || "—"}</p>
              </div>
            </div>

            {/* If ca khám đã kết thúc (Confirmed / End), show warning banner */}
            {(isCompleted || isApproved) && isCaseEnded && (
              <div className="flex items-center gap-2 rounded-md border border-amber-500/20 bg-amber-500/10 p-3 text-sm text-amber-700 dark:text-amber-400">
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span>Ca khám đã kết thúc, không thể đổi lịch.</span>
              </div>
            )}
          </div>
        ) : (
          /* ================= MODE 2: RESCHEDULE FORM ================= */
          <div className="space-y-4 py-2">
            {/* 1. Bác sĩ */}
            <div className="space-y-1.5">
              <Label htmlFor="reschedule-doctor" className="flex items-center gap-1">
                <Stethoscope className="h-4 w-4 text-muted-foreground" />
                Bác sĩ phụ trách <span className="text-destructive">*</span>
              </Label>
              <select
                id="reschedule-doctor"
                value={selectedDoctorId}
                onChange={(e) => handleDoctorChange(e.target.value)}
                disabled={isLoadingDoctors || rescheduleMutation.isPending}
                className="h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus:outline-none focus:ring-2 focus:ring-ring"
              >
                {isLoadingDoctors ? (
                  <option value="">Đang tải danh sách bác sĩ...</option>
                ) : (
                  doctors.map((doc) => {
                    const id = getDocId(doc);
                    return (
                      <option key={id} value={id}>
                        {doc.fullName}
                      </option>
                    );
                  })
                )}
              </select>
            </div>

            {/* 2. Ngày khám */}
            <div className="space-y-1.5">
              <Label htmlFor="reschedule-date" className="flex items-center gap-1">
                <Calendar className="h-4 w-4 text-muted-foreground" />
                Ngày khám mới <span className="text-destructive">*</span>
              </Label>
              <DatePicker
                id="reschedule-date"
                value={selectedDate}
                minDate={new Date()}
                onChange={handleDateChange}
                disabled={rescheduleMutation.isPending}
                className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              />
            </div>

            {/* 3. Khung giờ */}
            <div className="space-y-1.5">
              <Label htmlFor="reschedule-slot" className="flex items-center gap-1">
                <Clock className="h-4 w-4 text-muted-foreground" />
                Khung giờ mới <span className="text-destructive">*</span>
              </Label>
              <select
                id="reschedule-slot"
                value={selectedSlotId}
                onChange={(e) => {
                  setSelectedSlotId(e.target.value);
                  setValidationError(null);
                }}
                disabled={
                  !selectedDoctorId ||
                  !selectedDate ||
                  isLoadingSlots ||
                  rescheduleMutation.isPending
                }
                className="h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus:outline-none focus:ring-2 focus:ring-ring disabled:opacity-50"
              >
                {isLoadingSlots ? (
                  <option value="">Đang tải khung giờ...</option>
                ) : !selectedDoctorId || !selectedDate ? (
                  <option value="">Vui lòng chọn bác sĩ và ngày</option>
                ) : availableSlots.length === 0 ? (
                  <option value="">Không có khung giờ trống</option>
                ) : (
                  <>
                    <option value="">
                      -- Chọn khung giờ ({availableSlots.length} slot trống) --
                    </option>
                    {availableSlots.map((slot) => (
                      <option key={slot.slotId} value={slot.slotId}>
                        {formatSlotLabel(slot)}
                      </option>
                    ))}
                  </>
                )}
              </select>
            </div>

            {/* 4. Lý do khám mới */}
            <div className="space-y-1.5">
              <Label htmlFor="reschedule-new-reason" className="flex items-center gap-1">
                <FileText className="h-4 w-4 text-muted-foreground" />
                Lý do khám
              </Label>
              <Textarea
                id="reschedule-new-reason"
                rows={2}
                value={newReason}
                onChange={(e) => setNewReasonOverride(e.target.value)}
                placeholder="Lý do khám của bệnh nhân (tuỳ chọn)"
                disabled={rescheduleMutation.isPending}
              />
            </div>

            {/* 5. Lý do đổi lịch (bắt buộc) */}
            <div className="space-y-1.5">
              <Label htmlFor="reschedule-reason" className="flex items-center gap-1">
                <AlertCircle className="h-4 w-4 text-muted-foreground" />
                Lý do đổi lịch <span className="text-destructive">*</span>
              </Label>
              <Textarea
                id="reschedule-reason"
                rows={2}
                value={rescheduleReason}
                onChange={(e) => {
                  setRescheduleReason(e.target.value);
                  setValidationError(null);
                }}
                placeholder="VD: Bác sĩ bận, bệnh nhân yêu cầu đổi giờ..."
                disabled={rescheduleMutation.isPending}
              />
            </div>

            {/* Inline validation error */}
            {validationError && (
              <div className="flex items-center gap-1.5 text-sm text-destructive">
                <AlertCircle className="h-4 w-4" />
                <span>{validationError}</span>
              </div>
            )}

            {/* 6. Checkbox Check-in ngay */}
            <div className="flex items-start space-x-3 rounded-lg border border-border p-3 bg-muted/20">
              <Checkbox
                id="auto-checkin"
                checked={autoCheckin}
                onCheckedChange={(checked) => setAutoCheckinOverride(Boolean(checked))}
                disabled={rescheduleMutation.isPending}
              />
              <div className="grid gap-1 leading-none">
                <label
                  htmlFor="auto-checkin"
                  className="text-sm font-medium leading-none cursor-pointer"
                >
                  Check-in ngay cho bệnh nhân
                </label>
                <p className="text-xs text-muted-foreground">
                  Bật nếu bệnh nhân đang có mặt tại phòng khám. Khi bật, lịch hẹn mới
                  sẽ được chuyển thẳng sang trạng thái đã check-in.
                </p>
              </div>
            </div>
          </div>
        )}

        <DialogFooter className="gap-2 sm:gap-2">
          {mode === "view" ? (
            <>
              <Button type="button" variant="outline" onClick={onClose}>
                Đóng
              </Button>
              {isBooked && onCheckin && (
                <Button
                  type="button"
                  onClick={() => {
                    onCheckin(item);
                    onClose();
                  }}
                >
                  Check-in
                </Button>
              )}
              {canReschedule && (
                <Button
                  type="button"
                  variant="default"
                  onClick={() => setMode("reschedule")}
                  className="gap-1.5"
                >
                  <CalendarClock className="h-4 w-4" />
                  Đổi lịch
                </Button>
              )}
            </>
          ) : (
            <>
              <Button
                type="button"
                variant="outline"
                onClick={() => {
                  setMode("view");
                  setValidationError(null);
                }}
                disabled={rescheduleMutation.isPending}
                className="gap-1.5"
              >
                <ArrowLeft className="h-4 w-4" />
                Quay lại
              </Button>
              <Button
                type="button"
                onClick={handleSubmitReschedule}
                disabled={
                  !selectedSlotId ||
                  !rescheduleReason.trim() ||
                  rescheduleMutation.isPending
                }
              >
                {rescheduleMutation.isPending ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    Đang xử lý...
                  </>
                ) : (
                  "Xác nhận đổi lịch"
                )}
              </Button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export function AppointmentDetailModal({
  isOpen,
  item,
  onClose,
  onCheckin,
}: AppointmentDetailModalProps) {
  if (!isOpen || !item) {
    return null;
  }

  return (
    <AppointmentDetailModalInner
      key={item.appointmentId}
      item={item}
      onClose={onClose}
      onCheckin={onCheckin}
    />
  );
}
