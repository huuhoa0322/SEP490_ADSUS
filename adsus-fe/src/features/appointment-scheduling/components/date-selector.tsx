"use client";

import { useMemo } from "react";
import { format, startOfDay, startOfWeek, addDays } from "date-fns";

interface DateSelectorProps {
  displayDates: Date[];
  selectedDate: string | null;
  onSelectDate: (dateStr: string) => void;
  selectedWeekIndex: number;
}

const VI_WEEKDAYS = ["CN", "T2", "T3", "T4", "T5", "T6", "T7"];

export function DateSelector({
  displayDates,
  selectedDate,
  onSelectDate,
  selectedWeekIndex,
}: DateSelectorProps) {
  const weekRangeLabel = useMemo(() => {
    const today = startOfDay(new Date());
    const currentMonday = startOfWeek(today, { weekStartsOn: 1 });
    const weekMonday = addDays(currentMonday, selectedWeekIndex * 7);
    const weekSunday = addDays(weekMonday, 6);
    return `${format(weekMonday, "dd/MM")} - ${format(weekSunday, "dd/MM")}`;
  }, [selectedWeekIndex]);

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <label className="text-xs font-bold tracking-wider text-muted-foreground uppercase">
          Ngày khám ({weekRangeLabel})
        </label>
        {displayDates.length > 0 && (
          <span className="text-xs text-muted-foreground">
            {displayDates.length} ngày có lịch
          </span>
        )}
      </div>

      {displayDates.length === 0 ? (
        <div className="rounded-lg border border-dashed border-border bg-muted/20 p-4 text-center text-sm text-muted-foreground">
          Không có khung giờ nào còn trống trong tuần này. Vui lòng chọn tuần khác.
        </div>
      ) : (
        <div className="flex flex-wrap gap-2">
          {displayDates.map((d) => {
            const dateStr = format(d, "yyyy-MM-dd");
            const isSelected = selectedDate === dateStr;
            const dayOfWeek = VI_WEEKDAYS[d.getDay()];
            const dayMonth = format(d, "dd/MM");

            return (
              <button
                key={dateStr}
                type="button"
                onClick={() => onSelectDate(dateStr)}
                className={`flex h-11 min-w-[92px] items-center justify-center rounded-lg border px-3 text-sm font-medium transition-all ${
                  isSelected
                    ? "border-primary bg-primary text-primary-foreground shadow-sm"
                    : "border-border bg-background text-foreground hover:bg-muted/50"
                }`}
              >
                <span>
                  {dayOfWeek} ({dayMonth})
                </span>
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
