"use client";

import { useMemo } from "react";
import { format } from "date-fns";
import { useScheduleSlots } from "../hooks/use-schedule-slot";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Input } from "@/components/ui/input";
import { DatePicker } from "@/components/ui/date-picker";

import type { ScheduleSlotResponse } from "../types/schedule-slot.types";

export interface FollowUpSectionProps {
  isFollowUp: boolean;
  setIsFollowUp: (val: boolean) => void;
  followUpDate: string;
  setFollowUpDate: (val: string) => void;
  followUpSlotId: string;
  setFollowUpSlotId: (val: string) => void;
  followUpReason: string;
  setFollowUpReason: (val: string) => void;
}

export function FollowUpSection({
  isFollowUp,
  setIsFollowUp,
  followUpDate,
  setFollowUpDate,
  followUpSlotId,
  setFollowUpSlotId,
  followUpReason,
  setFollowUpReason,
}: FollowUpSectionProps) {
  
  const today = new Date();

  // Chỉ fetch các ca trống của ngày đã chọn (hoặc ngày hôm nay nếu chưa chọn)
  const { data: slotsData, isLoading } = useScheduleSlots({
    fromDate: followUpDate || format(today, "yyyy-MM-dd"),
    toDate: followUpDate || format(today, "yyyy-MM-dd"),
    status: "OPEN",
    pageSize: 100,
  });

  const slotsForDate: ScheduleSlotResponse[] = useMemo(() => {
    const slots = slotsData || [];
    return [...slots].sort((a, b) => a.startTime.localeCompare(b.startTime));
  }, [slotsData]);

  return (
    <div className="rounded-lg border border-border bg-card p-4 shadow-sm">
      <div className="flex items-center space-x-2 border-b border-border pb-3 mb-3">
        <Checkbox 
          id="is-follow-up" 
          checked={isFollowUp} 
          onCheckedChange={(checked) => setIsFollowUp(checked === true)} 
        />
        <Label htmlFor="is-follow-up" className="text-base font-bold text-primary cursor-pointer">
          🔄 Hẹn tái khám
        </Label>
      </div>

      {isFollowUp && (
        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="follow-up-date">📅 Ngày tái khám <span className="text-red-500">*</span></Label>
            <DatePicker
              id="follow-up-date"
              value={followUpDate}
              onChange={(val) => {
                setFollowUpDate(val);
                setFollowUpSlotId(""); // reset slot khi đổi ngày
              }}
              placeholder="Chọn hoặc nhập (dd/MM/yyyy)"
              minDate={today}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="follow-up-slot">🕐 Ca khám <span className="text-red-500">*</span></Label>
            <Select 
              value={followUpSlotId} 
              onValueChange={setFollowUpSlotId}
              disabled={!followUpDate || isLoading}
            >
              <SelectTrigger id="follow-up-slot">
                <SelectValue placeholder={isLoading ? "Đang tải ca..." : "Chọn ca khám"} />
              </SelectTrigger>
              {/* Force position="popper" and side="bottom" so it drops DOWN like standard combobox */}
              <SelectContent position="popper" side="bottom" align="start">
                {slotsForDate.length === 0 ? (
                  <SelectItem value="none" disabled>Không có ca trống</SelectItem>
                ) : (
                  slotsForDate.map((slot) => (
                    <SelectItem key={slot.slotId} value={slot.slotId}>
                      {slot.startTime.substring(0, 5)} - {slot.endTime.substring(0, 5)}
                    </SelectItem>
                  ))
                )}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="follow-up-reason">📝 Lý do tái khám</Label>
            <Input 
              id="follow-up-reason"
              placeholder="VD: Tái khám sau 2 tuần, kiểm tra lại siêu âm..." 
              value={followUpReason}
              onChange={(e) => setFollowUpReason(e.target.value)}
            />
          </div>
        </div>
      )}
    </div>
  );
}
