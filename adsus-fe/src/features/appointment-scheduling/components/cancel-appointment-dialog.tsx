"use client";

import { useState } from "react";
import { TriangleAlert } from "lucide-react";
import toast from "react-hot-toast";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { Textarea } from "@/components/ui/textarea";
import { Button } from "@/components/ui/button";
import { useCancellationStatusToday, useCancelMyAppointment } from "../hooks/use-appointment-history";

interface CancelAppointmentDialogProps {
  appointmentId: string | null;
  onClose: () => void;
}

const PRESET_REASONS = [
  { value: "Bận đột xuất", label: "Bận đột xuất" },
  { value: "Đổi lịch trình", label: "Đổi lịch trình" },
  { value: "other", label: "Khác (ghi rõ)" },
] as const;

export function CancelAppointmentDialog({
  appointmentId,
  onClose,
}: CancelAppointmentDialogProps) {
  const [selectedPreset, setSelectedPreset] = useState<string>("");
  const [customReason, setCustomReason] = useState("");
  /**
   * Step machine:
   *  null       — dialog closed
   *  "reason"   — showing reason form
   *  "warning"  — showing warning dialog
   */
  const [step, setStep] = useState<"reason" | "warning" | null>(null);

  const isOpen = appointmentId !== null;

  const { data: statusData, isLoading: statusLoading } = useCancellationStatusToday(
    isOpen && step !== "warning"
  );
  const cancelMutation = useCancelMyAppointment();

  const isNextCancellationFinal = statusData?.isNextCancellationFinal === true;

  /**
   * Derived step — if next cancellation is final, jump straight to warning
   * when the dialog first opens (appointmentId goes null → non-null).
   * Once the user is in the flow, we stay in whatever step they are.
   */
  const effectiveStep: "reason" | "warning" | null = isOpen
    ? step ?? (isNextCancellationFinal ? "warning" : "reason")
    : null;

  /** Resolve the effective reason string. */
  function resolveReason(): string {
    if (selectedPreset === "other") return customReason.trim();
    return selectedPreset;
  }

  const canSubmit = resolveReason().length > 0 && !cancelMutation.isPending;

  function handleConfirm() {
    if (!canSubmit) return;

    if (effectiveStep === "reason" && isNextCancellationFinal) {
      setStep("warning");
      return;
    }

    if (!appointmentId) return;

    cancelMutation.mutate(
      { appointmentId, reason: resolveReason() },
      {
        onSuccess: () => {
          toast.success("Đã hủy lịch khám thành công");
          onClose();
          reset();
        },
        onError: (error: unknown) => {
          const message =
            error instanceof Error
              ? error.message
              : "Có lỗi xảy ra khi hủy lịch khám.";
          toast.error(message);
          // Keep dialog open so user can retry
        },
      }
    );
  }

  function handleBackFromWarning() {
    setStep("reason");
  }

  function reset() {
    setSelectedPreset("");
    setCustomReason("");
    setStep(null);
  }

  function handleOpenChange(open: boolean) {
    if (!open) {
      onClose();
      reset();
    }
  }

  return (
    <>
      {/* Step 1 — Reason form */}
      <Dialog open={effectiveStep === "reason"} onOpenChange={handleOpenChange}>
        <DialogContent className="max-w-sm">
          <DialogHeader>
            <DialogTitle>Hủy lịch khám</DialogTitle>
          </DialogHeader>

          <div className="space-y-3">
            {/* Preset buttons */}
            <div className="flex flex-wrap gap-2">
              {PRESET_REASONS.map((preset) => (
                <button
                  key={preset.value}
                  type="button"
                  onClick={() => setSelectedPreset(preset.value)}
                  className={
                    selectedPreset === preset.value
                      ? "rounded-full px-4 py-1.5 text-sm font-medium bg-[#128C82] text-white transition-colors"
                      : "rounded-full px-4 py-1.5 text-sm font-medium bg-muted text-muted-foreground hover:bg-muted/80 transition-colors"
                  }
                >
                  {preset.label}
                </button>
              ))}
            </div>

            {/* Textarea: always shown when "Khác" selected or user is typing */}
            {selectedPreset === "other" && (
              <Textarea
                value={customReason}
                onChange={(e) => setCustomReason(e.target.value)}
                placeholder="Nhập lý do hủy..."
                rows={3}
                className="resize-none"
                data-testid="custom-reason-textarea"
              />
            )}
          </div>

          <DialogFooter className="gap-2 sm:gap-2">
            <Button
              type="button"
              variant="outline"
              className="flex-1"
              onClick={() => handleOpenChange(false)}
            >
              Đóng
            </Button>
            <Button
              type="button"
              variant="destructive"
              className="flex-1"
              disabled={!canSubmit}
              onClick={handleConfirm}
              data-testid="confirm-cancel-button"
            >
              {cancelMutation.isPending ? "Đang hủy..." : "Xác nhận hủy"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Step 2 — Warning dialog */}
      <Dialog
        open={effectiveStep === "warning"}
        onOpenChange={(open) => {
          if (!open) {
            handleBackFromWarning();
          }
        }}
      >
        <DialogContent className="max-w-sm" showCloseButton={false}>
          <DialogHeader>
            <div className="flex items-center gap-2">
              <TriangleAlert className="size-5 text-amber-500 shrink-0" />
              <DialogTitle>Cảnh báo</DialogTitle>
            </div>
          </DialogHeader>

          <p className="text-sm text-muted-foreground">
            Bạn đã hủy 2 lần trong ngày. Nếu tiếp tục, bạn sẽ không thể đặt lịch online trong hôm nay.
          </p>

          <DialogFooter className="gap-2 sm:gap-2">
            <Button
              type="button"
              variant="outline"
              className="flex-1"
              onClick={handleBackFromWarning}
              data-testid="back-from-warning-button"
            >
              Quay lại
            </Button>
            <Button
              type="button"
              variant="destructive"
              className="flex-1"
              onClick={handleConfirm}
              data-testid="final-confirm-button"
            >
              Tiếp tục hủy
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
