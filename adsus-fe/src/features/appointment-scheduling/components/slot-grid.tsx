"use client";

import { AlertCircle, Clock, Info } from "lucide-react";
import type { OpenSlotResponse } from "../types/booking.types";
import { Skeleton } from "@/components/ui/skeleton";

interface SlotGridProps {
  selectedDoctorId: string | null;
  selectedDate: string | null;
  slots: OpenSlotResponse[];
  selectedSlotId: string | null;
  onSelectSlot: (slotId: string) => void;
  isLoading?: boolean;
}

export function SlotGrid({
  selectedDoctorId,
  selectedDate,
  slots,
  selectedSlotId,
  onSelectSlot,
  isLoading,
}: SlotGridProps) {
  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <label className="text-xs font-bold tracking-wider text-muted-foreground uppercase">
          Khung giờ khả dụng
        </label>
        {selectedDoctorId && selectedDate && slots.length > 0 && (
          <span className="text-xs text-muted-foreground">
            {slots.length} khung giờ trống
          </span>
        )}
      </div>

      {isLoading ? (
        <div className="grid grid-cols-2 sm:grid-cols-3 gap-2.5">
          {[1, 2, 3, 4, 5, 6].map((i) => (
            <Skeleton key={i} className="h-16 w-full rounded-lg" />
          ))}
        </div>
      ) : !selectedDoctorId ? (
        // 1. Warning: Chưa chọn bác sĩ
        <div className="flex items-start gap-2.5 rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-300">
          <Info className="h-5 w-5 shrink-0 text-amber-600 dark:text-amber-400 mt-0.5" />
          <span>Vui lòng chọn bác sĩ để xem các khung giờ.</span>
        </div>
      ) : !selectedDate ? (
        // 2. Warning: Chưa chọn ngày
        <div className="flex items-start gap-2.5 rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-300">
          <Info className="h-5 w-5 shrink-0 text-amber-600 dark:text-amber-400 mt-0.5" />
          <span>Vui lòng chọn ngày khám để xem các khung giờ.</span>
        </div>
      ) : slots.length === 0 ? (
        // 3. Warning: Không có slot
        <div className="flex items-start gap-2.5 rounded-lg border border-border bg-muted/20 p-4 text-sm text-muted-foreground">
          <AlertCircle className="h-5 w-5 shrink-0 text-muted-foreground mt-0.5" />
          <span>Không có khung giờ cho ngày đã chọn. Vui lòng chọn ngày khác hoặc bác sĩ khác.</span>
        </div>
      ) : (
        // 4. Grid 3 cột các slot
        <div className="grid grid-cols-2 sm:grid-cols-3 gap-2.5">
          {slots.map((slot) => {
            const isSelected = selectedSlotId === slot.slotId;
            const timeLabel = `${slot.startTime.slice(0, 5)} - ${slot.endTime.slice(0, 5)}`;

            return (
              <button
                key={slot.slotId}
                type="button"
                onClick={() => onSelectSlot(slot.slotId)}
                className={`flex flex-col items-center justify-center rounded-lg border p-3 text-center transition-all ${
                  isSelected
                    ? "border-primary bg-primary text-primary-foreground shadow-md ring-2 ring-primary ring-offset-2"
                    : "border-border bg-background text-foreground hover:border-primary/50 hover:bg-muted/40"
                }`}
              >
                <div className="flex items-center gap-1.5 text-sm font-semibold">
                  <Clock className="h-3.5 w-3.5 shrink-0 opacity-80" />
                  <span>{timeLabel}</span>
                </div>
                <span className="mt-1 text-xs opacity-80 truncate max-w-full">
                  BS. {slot.doctorName}
                </span>
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
