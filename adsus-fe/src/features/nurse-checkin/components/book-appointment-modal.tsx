"use client";

import { useState, useMemo } from "react";
import { format } from "date-fns";
import {
  Calendar,
  Clock,
  User,
  Users,
  Stethoscope,
  FileText,
  Phone,
  Info,
  Loader2,
  CalendarCheck,
  Plus,
} from "lucide-react";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { DatePicker } from "@/components/ui/date-picker";
import type {
  DoctorSummary,
  AvailableSlot,
} from "../types/checkin.types";
import {
  useDoctorList,
  useAvailableSlots,
} from "../hooks/use-reschedule";
import { useStaffBookAppointment } from "../hooks/use-staff-book-appointment";
import { useRelativesForGuardian } from "@/features/appointment-scheduling/hooks/use-relatives";
import { AddRelativeModal } from "@/features/appointment-scheduling/components/add-relative-modal";

export interface BookAppointmentModalProps {
  patientProfileId: string;
  patientName: string;
  patientPhone: string | null;
  patientUserId?: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function BookAppointmentModal({
  patientProfileId,
  patientName,
  patientPhone,
  patientUserId,
  open,
  onOpenChange,
}: BookAppointmentModalProps) {
  // Form state
  const [targetType, setTargetType] = useState<"SELF" | "RELATIVE">("SELF");
  const [selectedRelativeId, setSelectedRelativeId] = useState<string>("");
  const [isAddRelativeOpen, setIsAddRelativeOpen] = useState(false);
  const [selectedDoctorId, setSelectedDoctorId] = useState<string>("");
  const [selectedDate, setSelectedDate] = useState<string>("");
  const [selectedSlotId, setSelectedSlotId] = useState<string>("");
  const [reason, setReason] = useState<string>("");
  const [validationError, setValidationError] = useState<string | null>(null);

  // Queries and mutations
  const { data: relatives = [] } = useRelativesForGuardian(patientUserId);
  const { data: doctors = [], isLoading: isLoadingDoctors } = useDoctorList();
  const bookMutation = useStaffBookAppointment();

  // Allowed booking range (today to 14 days later)
  const { minDate, maxDate } = useMemo(() => {
    const now = new Date();
    const max = new Date(now.getTime() + 14 * 24 * 60 * 60 * 1000);
    return { minDate: now, maxDate: max };
  }, []);

  // Helper to extract doctor id (supports backend DTO userId or doctorId)
  const getDocId = (doc: DoctorSummary): string => doc.doctorId || doc.userId || "";

  // Fetch available slots for the selected doctor and date
  const {
    data: availableSlots = [],
    isLoading: isLoadingSlots,
  } = useAvailableSlots({
    doctorId: selectedDoctorId || undefined,
    fromDate: selectedDate || undefined,
    toDate: selectedDate || undefined,
  });

  // Filter out slots that have already passed (past dates or earlier time slots today)
  const filteredAvailableSlots = useMemo(() => {
    const now = new Date();
    const todayStr = format(now, "yyyy-MM-dd");
    const currentTimeStr = format(now, "HH:mm:ss");

    return availableSlots.filter((slot) => {
      let slotDateStr = selectedDate;
      if (slot.slotDate) {
        try {
          slotDateStr = format(new Date(slot.slotDate), "yyyy-MM-dd");
        } catch {
          slotDateStr = slot.slotDate;
        }
      }

      // Hide if date is before today
      if (slotDateStr < todayStr) {
        return false;
      }

      // If date is today, hide if slot startTime <= currentTime
      if (slotDateStr === todayStr) {
        const start = slot.startTime?.length === 5 ? `${slot.startTime}:00` : slot.startTime || "";
        return start > currentTimeStr;
      }

      return true;
    });
  }, [availableSlots, selectedDate]);

  // Derived effective selected slot: ensures slot is valid in filteredAvailableSlots without triggering setState in an effect
  const effectiveSelectedSlotId = useMemo(() => {
    if (!selectedSlotId) return "";
    return filteredAvailableSlots.some((s) => s.slotId === selectedSlotId) ? selectedSlotId : "";
  }, [filteredAvailableSlots, selectedSlotId]);

  const formatSlotLabel = (slot: AvailableSlot): string => {
    const start = slot.startTime ? slot.startTime.slice(0, 5) : "";
    const end = slot.endTime ? slot.endTime.slice(0, 5) : "";
    return `${start} – ${end}`;
  };

  const handleDoctorChange = (doctorId: string) => {
    setSelectedDoctorId(doctorId);
    setSelectedSlotId("");
    setValidationError(null);
  };

  const handleDateChange = (val: string | Date | null | undefined) => {
    if (!val) {
      setSelectedDate("");
      setSelectedSlotId("");
      return;
    }
    const str = typeof val === "string" ? val : format(val, "yyyy-MM-dd");
    setSelectedDate(str);
    setSelectedSlotId("");
    setValidationError(null);
  };

  const handleResetAndClose = () => {
    setSelectedDoctorId("");
    setSelectedDate("");
    setSelectedSlotId("");
    setReason("");
    setTargetType("SELF");
    setSelectedRelativeId("");
    setIsAddRelativeOpen(false);
    setValidationError(null);
    onOpenChange(false);
  };

  const handleSubmit = async () => {
    if (targetType === "RELATIVE" && !selectedRelativeId) {
      setValidationError("Vui lòng chọn người thân cần đặt lịch.");
      return;
    }
    if (!selectedDoctorId) {
      setValidationError("Vui lòng chọn bác sĩ.");
      return;
    }
    if (!selectedDate) {
      setValidationError("Vui lòng chọn ngày khám.");
      return;
    }
    if (!effectiveSelectedSlotId) {
      setValidationError("Vui lòng chọn khung giờ khám.");
      return;
    }

    setValidationError(null);

    const selectedRelative =
      targetType === "RELATIVE"
        ? relatives.find((r) => r.relationshipId === selectedRelativeId)
        : null;

    try {
      await bookMutation.mutateAsync({
        patientProfileId: selectedRelative?.patientProfileId || patientProfileId,
        scheduleSlotId: effectiveSelectedSlotId,
        reason: reason.trim() || undefined,
        relationshipId: targetType === "RELATIVE" ? selectedRelativeId : undefined,
      });
      handleResetAndClose();
    } catch {
      // Error toast is handled in useStaffBookAppointment hook
    }
  };

  return (
    <>
      <Dialog
        open={open}
        onOpenChange={(nextOpen) => {
        if (!nextOpen) {
          handleResetAndClose();
        } else {
          onOpenChange(true);
        }
      }}
    >
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <div className="flex items-center gap-2 pr-6">
            <CalendarCheck className="h-5 w-5 text-emerald-600" />
            <DialogTitle>Đặt lịch hẹn cho bệnh nhân</DialogTitle>
          </div>
          <div className="flex flex-wrap items-center gap-x-4 gap-y-1 pt-1 text-sm text-muted-foreground">
            <span className="flex items-center gap-1 font-medium text-foreground">
              <User className="size-3.5 text-muted-foreground" />
              {patientName}
            </span>
            {patientPhone && (
              <span className="flex items-center gap-1 font-mono text-xs">
                <Phone className="size-3 text-muted-foreground" />
                {patientPhone}
              </span>
            )}
            {targetType === "RELATIVE" && selectedRelativeId && (
              <span className="flex items-center gap-1 font-medium text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded text-xs dark:bg-emerald-950/40 dark:text-emerald-300">
                Đặt cho: {relatives.find((r) => r.relationshipId === selectedRelativeId)?.patientName}
              </span>
            )}
          </div>
        </DialogHeader>

        <div className="space-y-4 py-2">
          {/* 0. Chọn đối tượng khám (nếu bệnh nhân có tài khoản) */}
          {Boolean(patientUserId) && (
            <div className="space-y-2 rounded-lg border border-border bg-muted/30 p-3">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                  Đặt lịch cho
                </Label>
                {targetType === "RELATIVE" && (
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    onClick={() => setIsAddRelativeOpen(true)}
                    className="h-6 px-2 text-xs text-primary hover:text-primary hover:bg-primary/10"
                  >
                    <Plus className="size-3 mr-1" />
                    Thêm người thân
                  </Button>
                )}
              </div>
              <div className="grid grid-cols-2 gap-2">
                <button
                  type="button"
                  onClick={() => {
                    setTargetType("SELF");
                    setSelectedRelativeId("");
                    setValidationError(null);
                  }}
                  className={`flex items-center justify-center gap-1.5 rounded-md border p-2 text-xs font-medium transition-colors ${
                    targetType === "SELF"
                      ? "border-primary bg-primary text-primary-foreground shadow-xs"
                      : "border-border bg-background text-foreground hover:bg-muted/50"
                  }`}
                >
                  <User className="size-3.5" />
                  <span className="truncate">{patientName} (Bản thân)</span>
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setTargetType("RELATIVE");
                    if (relatives.length === 1) {
                      setSelectedRelativeId(relatives[0].relationshipId);
                    }
                    setValidationError(null);
                  }}
                  className={`flex items-center justify-center gap-1.5 rounded-md border p-2 text-xs font-medium transition-colors ${
                    targetType === "RELATIVE"
                      ? "border-primary bg-primary text-primary-foreground shadow-xs"
                      : "border-border bg-background text-foreground hover:bg-muted/50"
                  }`}
                >
                  <Users className="size-3.5" />
                  <span>Người thân ({relatives.length})</span>
                </button>
              </div>

              {targetType === "RELATIVE" && (
                <div className="pt-1.5 space-y-1.5">
                  <div className="flex items-center justify-between">
                    <Label htmlFor="book-relative" className="text-xs text-muted-foreground">
                      Chọn người thân <span className="text-destructive">*</span>
                    </Label>
                  </div>

                  {relatives.length === 0 ? (
                    <div className="flex items-center justify-between rounded-md border border-dashed border-amber-300 bg-amber-50/50 p-2 text-xs text-amber-800 dark:border-amber-800/50 dark:bg-amber-950/20 dark:text-amber-300">
                      <span>Bệnh nhân chưa có hồ sơ người thân nào.</span>
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        onClick={() => setIsAddRelativeOpen(true)}
                        className="h-7 text-xs border-amber-300 bg-white hover:bg-amber-50"
                      >
                        <Plus className="size-3 mr-1" />
                        Thêm ngay
                      </Button>
                    </div>
                  ) : (
                    <div className="flex gap-2">
                      <select
                        id="book-relative"
                        value={selectedRelativeId}
                        onChange={(e) => {
                          setSelectedRelativeId(e.target.value);
                          setValidationError(null);
                        }}
                        disabled={bookMutation.isPending}
                        className="h-9 flex-1 rounded-md border border-input bg-background px-3 py-1.5 text-xs ring-offset-background focus:outline-none focus:ring-2 focus:ring-ring"
                      >
                        <option value="">-- Chọn người thân --</option>
                        {relatives.map((rel) => (
                          <option key={rel.relationshipId} value={rel.relationshipId}>
                            {rel.patientName} ({rel.relationshipName || "Người thân"})
                            {rel.dateOfBirth ? ` - Sinh: ${rel.dateOfBirth}` : ""}
                          </option>
                        ))}
                      </select>
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        onClick={() => setIsAddRelativeOpen(true)}
                        className="h-9 shrink-0 text-xs px-2.5"
                        title="Thêm người thân mới"
                      >
                        <Plus className="size-3.5" />
                        <span className="sr-only sm:not-sr-only sm:ml-1">Thêm</span>
                      </Button>
                    </div>
                  )}
                </div>
              )}
            </div>
          )}

          {/* 1. Bác sĩ */}
          <div className="space-y-1.5">
            <Label htmlFor="book-doctor" className="flex items-center gap-1">
              <Stethoscope className="h-4 w-4 text-muted-foreground" />
              Bác sĩ <span className="text-destructive">*</span>
            </Label>
            <select
              id="book-doctor"
              value={selectedDoctorId}
              onChange={(e) => handleDoctorChange(e.target.value)}
              disabled={isLoadingDoctors || bookMutation.isPending}
              className="h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus:outline-none focus:ring-2 focus:ring-ring"
            >
              <option value="">-- Chọn bác sĩ --</option>
              {doctors.map((doc) => {
                const id = getDocId(doc);
                return (
                  <option key={id} value={id}>
                    {doc.fullName} {doc.specialty ? `(${doc.specialty})` : ""}
                  </option>
                );
              })}
            </select>
          </div>

          {/* 2. Ngày khám */}
          <div className="space-y-1.5">
            <Label htmlFor="book-date" className="flex items-center gap-1">
              <Calendar className="h-4 w-4 text-muted-foreground" />
              Ngày khám <span className="text-destructive">*</span>
            </Label>
            <DatePicker
              id="book-date"
              value={selectedDate}
              minDate={minDate}
              maxDate={maxDate}
              onChange={handleDateChange}
              disabled={!selectedDoctorId || bookMutation.isPending}
              className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm disabled:opacity-50"
            />
          </div>

          {/* 3. Khung giờ */}
          <div className="space-y-1.5">
            <Label htmlFor="book-slot" className="flex items-center gap-1">
              <Clock className="h-4 w-4 text-muted-foreground" />
              Khung giờ <span className="text-destructive">*</span>
            </Label>
            <select
              id="book-slot"
              value={effectiveSelectedSlotId}
              onChange={(e) => {
                setSelectedSlotId(e.target.value);
                setValidationError(null);
              }}
              disabled={
                !selectedDoctorId ||
                !selectedDate ||
                isLoadingSlots ||
                bookMutation.isPending
              }
              className="h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus:outline-none focus:ring-2 focus:ring-ring disabled:opacity-50"
            >
              {isLoadingSlots ? (
                <option value="">Đang tải khung giờ...</option>
              ) : !selectedDoctorId ? (
                <option value="">Vui lòng chọn bác sĩ trước</option>
              ) : !selectedDate ? (
                <option value="">Vui lòng chọn ngày khám</option>
              ) : filteredAvailableSlots.length === 0 ? (
                <option value="">Không có khung giờ trống trong ngày này</option>
              ) : (
                <>
                  <option value="">
                    -- Chọn khung giờ ({filteredAvailableSlots.length} slot trống) --
                  </option>
                  {filteredAvailableSlots.map((slot) => (
                    <option key={slot.slotId} value={slot.slotId}>
                      {formatSlotLabel(slot)}
                    </option>
                  ))}
                </>
              )}
            </select>
          </div>

          {/* 4. Lý do khám */}
          <div className="space-y-1.5">
            <Label htmlFor="book-reason" className="flex items-center gap-1">
              <FileText className="h-4 w-4 text-muted-foreground" />
              Lý do khám <span className="text-xs text-muted-foreground">(tùy chọn)</span>
            </Label>
            <Input
              id="book-reason"
              placeholder="Nhập lý do khám hoặc ghi chú..."
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              disabled={bookMutation.isPending}
            />
          </div>

          {/* Validation error */}
          {validationError && (
            <div className="rounded-md border border-destructive/20 bg-destructive/10 p-2.5 text-xs text-destructive">
              {validationError}
            </div>
          )}

          {/* Info note */}
          <div className="rounded-md border border-blue-200 bg-blue-50/50 p-3 text-xs text-blue-800 dark:border-blue-900/50 dark:bg-blue-950/30 dark:text-blue-300">
            <div className="flex items-start gap-2">
              <Info className="size-4 shrink-0 mt-0.5 text-blue-600 dark:text-blue-400" />
              <div className="space-y-0.5">
                <p className="font-medium">Lịch hẹn sẽ được tạo ở trạng thái &quot;Đã đặt&quot; (BOOKED).</p>
                <p className="text-muted-foreground">Ca bệnh sẽ được tạo tự động. Bệnh nhân sẽ nhận thông báo.</p>
              </div>
            </div>
          </div>
        </div>

        <DialogFooter className="flex flex-col-reverse sm:flex-row gap-2">
          <Button
            type="button"
            variant="outline"
            onClick={handleResetAndClose}
            disabled={bookMutation.isPending}
          >
            Hủy
          </Button>
          <Button
            type="button"
            onClick={handleSubmit}
            disabled={
              (targetType === "RELATIVE" && !selectedRelativeId) ||
              !selectedDoctorId ||
              !selectedDate ||
              !effectiveSelectedSlotId ||
              bookMutation.isPending
            }
          >
            {bookMutation.isPending ? (
              <>
                <Loader2 className="mr-2 size-4 animate-spin" />
                Đang đặt lịch...
              </>
            ) : (
              "Xác nhận đặt lịch"
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>

    {isAddRelativeOpen && (
      <AddRelativeModal
        guardianUserId={patientUserId}
        guardianName={patientName}
        open={isAddRelativeOpen}
        onOpenChange={setIsAddRelativeOpen}
        onSuccess={(created) => {
          setSelectedRelativeId(created.relationshipId);
          setValidationError(null);
        }}
      />
    )}
  </>
);
}
