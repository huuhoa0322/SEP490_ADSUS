"use client";

import { format, parseISO } from "date-fns";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import type { AppointmentSummaryResponse } from "../types/booking.types";
import { isExpired } from "./appointment-history-card";

interface AppointmentHistoryDetailDialogProps {
  appointment: AppointmentSummaryResponse | null;
  onClose: () => void;
  onCancel: (appointmentId: string) => void;
  onReschedule: (appointmentId: string) => void;
}

/** Inline row: label left, value right, separated by border. */
function InfoRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex justify-between py-2 border-b border-muted last:border-b-0">
      <span className="text-sm text-muted-foreground">{label}</span>
      <span className="text-sm font-medium">{value}</span>
    </div>
  );
}

function getStatusBadgeVariant(
  status: string
): "default" | "secondary" | "destructive" | "outline" | "soft-warning" | "soft-primary" {
  switch (status) {
    case "BOOKED":
      return "default";
    case "APPROVED":
      return "soft-primary";
    case "CANCELLED":
    case "NO_SHOW":
      return "destructive";
    case "COMPLETED":
      return "secondary";
    default:
      return "outline";
  }
}

function getStatusLabel(status: string): string {
  switch (status) {
    case "BOOKED":
      return "Đã đặt";
    case "APPROVED":
      return "Đã duyệt";
    case "CANCELLED":
      return "Đã huỷ";
    case "COMPLETED":
      return "Hoàn thành";
    case "NO_SHOW":
      return "Vắng mặt";
    default:
      return status;
  }
}

export function AppointmentHistoryDetailDialog({
  appointment,
  onClose,
  onCancel,
  onReschedule,
}: AppointmentHistoryDetailDialogProps) {
  if (!appointment) return null;

  const { status, slotDate, endTime } = appointment;
  const expired = isExpired(slotDate, endTime);
  const canAct = (status === "BOOKED" || status === "APPROVED") && !expired;

  // Format date/time for display
  let dateDisplay = slotDate;
  try {
    dateDisplay = format(parseISO(slotDate), "dd/MM/yyyy");
  } catch {
    // use raw slotDate
  }

  const startDisplay = appointment.startTime ? appointment.startTime.slice(0, 5) : "";
  const endDisplay = appointment.endTime ? appointment.endTime.slice(0, 5) : "";

  const statusLabel = getStatusLabel(status);
  const badgeVariant = getStatusBadgeVariant(status);

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <div className="flex items-center justify-between pr-6">
            <DialogTitle>Chi tiết lịch khám</DialogTitle>
            <Badge variant={badgeVariant}>{statusLabel}</Badge>
          </div>
        </DialogHeader>

        <div className="space-y-1">
          {/* Người khám + Quan hệ (chỉ khi đặt hộ) */}
          {appointment.isBookedForOthers && (
            <>
              <InfoRow
                label="Người khám"
                value={appointment.patientFullName ?? "—"}
              />
              {appointment.relationshipLabel && (
                <InfoRow
                  label="Quan hệ"
                  value={appointment.relationshipLabel}
                />
              )}
            </>
          )}

          <InfoRow label="Ngày khám" value={dateDisplay} />
          <InfoRow label="Giờ khám" value={`${startDisplay} – ${endDisplay}`} />
          <InfoRow label="Bác sĩ" value={appointment.doctorName} />

          {appointment.reason && (
            <InfoRow label="Lý do khám" value={appointment.reason} />
          )}

          {status === "CANCELLED" && appointment.cancellationReason && (
            <div className="flex justify-between py-2 border-b border-muted">
              <span className="text-sm text-muted-foreground">Lý do hủy</span>
              <span className="text-sm font-medium text-destructive">
                {appointment.cancellationReason}
              </span>
            </div>
          )}
        </div>

        <DialogFooter className="gap-2 sm:gap-2">
          {canAct && (
            <>
              <Button
                type="button"
                variant="outline"
                className="flex-1 text-destructive border-destructive/40 hover:bg-destructive/10 hover:text-destructive"
                onClick={() => onCancel(appointment.appointmentId)}
              >
                Hủy lịch
              </Button>
              <Button
                type="button"
                variant="default"
                className="flex-1"
                onClick={() => onReschedule(appointment.appointmentId)}
              >
                Đặt lại lịch
              </Button>
            </>
          )}
          <Button type="button" variant="ghost" onClick={onClose}>
            Đóng
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
