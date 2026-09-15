"use client";

import { useMemo } from "react";
import { startOfDay, startOfWeek, addDays, format } from "date-fns";

interface WeekSelectorProps {
  selectedWeekIndex: number;
  onSelectWeek: (index: number) => void;
}

export function WeekSelector({
  selectedWeekIndex,
  onSelectWeek,
}: WeekSelectorProps) {
  const weeks = useMemo(() => {
    const today = startOfDay(new Date());
    const currentMonday = startOfWeek(today, { weekStartsOn: 1 });

    return [0, 1, 2, 3].map((i) => {
      const weekMonday = addDays(currentMonday, i * 7);
      const weekSunday = addDays(weekMonday, 6);
      const rangeLabel = `${format(weekMonday, "dd/MM")} - ${format(weekSunday, "dd/MM")}`;

      let title = `Tuần ${i + 1}`;
      if (i === 0) title = "Tuần này";
      else if (i === 1) title = "Tuần sau";

      return {
        index: i,
        title,
        rangeLabel,
      };
    });
  }, []);

  return (
    <div className="space-y-2">
      <span className="text-xs font-bold tracking-wider text-muted-foreground uppercase">
        Chọn tuần khám
      </span>
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 sm:gap-3">
        {weeks.map((w) => {
          const isSelected = selectedWeekIndex === w.index;
          return (
            <button
              key={w.index}
              type="button"
              onClick={() => onSelectWeek(w.index)}
              className={`flex flex-col items-center justify-center rounded-lg border p-2.5 sm:p-3 text-center transition-all ${
                isSelected
                  ? "border-primary bg-primary text-primary-foreground shadow-sm"
                  : "border-border bg-background text-foreground hover:bg-muted/50"
              }`}
            >
              <span className="text-xs font-medium opacity-90">{w.title}</span>
              <span className="text-sm font-semibold mt-0.5">{w.rangeLabel}</span>
            </button>
          );
        })}
      </div>
    </div>
  );
}
