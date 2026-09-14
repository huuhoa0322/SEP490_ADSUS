"use client";

import { format, parseISO } from "date-fns";
import { ChevronRight } from "lucide-react";
import { cn } from "@/lib/utils";
import { Card } from "@/components/ui/card";
import type { AppointmentSummaryResponse } from "../types/booking.types";

/** Format "YYYY-MM-DD" → "dd/MM/yyyy" */
export function formatSlotDate(slotDate: string): string {
  try {
    return format(parseISO(slotDate), "dd/MM/yyyy");
  } catch {
    return slotDate;
  }
}

/** True nếu giờ kết thúc đã qua so với hiện tại. */
export function isExpired(slotDate: string, endTime: string): boolean {
  try {
    const slotDateTime = parseISO(`${slotDate}T${endTime}`);
    return slotDateTime < new Date();
  } catch {
    return false;
  }
}

/** Trả về Tailwind color class cho accent bar. */
export function getAccentColor(appointment: AppointmentSummaryResponse): string {
  const status = appointment.status;
  if (status === "CANCELLED" || status === "COMPLETED" || status === "NO_SHOW") {
    return "bg-gray-400";
  }
  // BOOKED or APPROVED
  if (isExpired(appointment.slotDate, appointment.endTime)) {
    return "bg-orange-400";
  }
  return "bg-teal-600";
}

/** Trả về class cho status Badge. */
function getStatusBadgeClass(status: string): string {
  switch (status) {
    case "BOOKED":
      return "bg-emerald-100 text-emerald-800";
    case "APPROVED":
      return "bg-amber-100 text-amber-800";
    case "CANCELLED":
      return "bg-red-100 text-red-800";
    case "COMPLETED":
      return "bg-gray-100 text-gray-600";
    case "NO_SHOW":
      return "bg-gray-100 text-gray-600";
    default:
      return "bg-gray-100 text-gray-600";
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

interface AppointmentHistoryCardProps {
  appointment: AppointmentSummaryResponse;
  onClick: (appointment: AppointmentSummaryResponse) => void;
}

export function AppointmentHistoryCard({
  appointment,
  onClick,
}: AppointmentHistoryCardProps) {
  const accentColor = getAccentColor(appointment);
  const statusBadgeClass = getStatusBadgeClass(appointment.status);
  const statusLabel = getStatusLabel(appointment.status);

  // Format time range: "HH:mm - HH:mm"
  const startDisplay = appointment.startTime ? appointment.startTime.slice(0, 5) : "";
  const endDisplay = appointment.endTime ? appointment.endTime.slice(0, 5) : "";
  const timeRange = `${startDisplay} – ${endDisplay}`;

  return (
    <Card
      className="flex cursor-pointer flex-row items-center gap-0 overflow-hidden px-0 py-0 transition-colors hover:bg-muted/50"
      onClick={() => onClick(appointment)}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          onClick(appointment);
        }
      }}
      data-testid="appointment-history-card"
    >
      {/* Left accent bar */}
      <div className={cn("h-full w-1 shrink-0", accentColor)} data-testid="accent-bar" />

      {/* Body */}
      <div className="flex flex-1 flex-col gap-0.5 px-4 py-3">
        {/* isBookedForOthers badge + patient name */}
        {appointment.isBookedForOthers && appointment.relationshipLabel ? (
          <div className="mb-0.5 flex flex-wrap items-center gap-1">
            <span className="inline-flex items-center rounded-full bg-violet-100 px-2 py-0.5 text-xs font-medium text-violet-700">
              Đặt hộ: {appointment.relationshipLabel}
            </span>
            {appointment.patientFullName && (
              <span className="text-sm font-semibold text-foreground">
                {appointment.patientFullName}
              </span>
            )}
          </div>
        ) : null}

        {/* Date */}
        <p className="text-sm text-muted-foreground" data-testid="date-label">
          Lịch khám: {formatSlotDate(appointment.slotDate)}
        </p>

        {/* Time range */}
        <p
          className="font-mono text-xl font-bold text-[#1F2A44]"
          data-testid="time-range"
        >
          {timeRange}
        </p>

        {/* Doctor name */}
        <p className="text-sm text-muted-foreground" data-testid="doctor-name">
          BS. {appointment.doctorName}
        </p>
      </div>

      {/* Right side */}
      <div className="flex flex-col items-center gap-2 px-4 py-3">
        <span
          className={cn(
            "inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold",
            statusBadgeClass
          )}
          data-testid="status-badge"
        >
          {statusLabel}
        </span>
        <ChevronRight className="size-4 text-muted-foreground" data-testid="chevron-icon" />
      </div>
    </Card>
  );
}
