"use client";

import type { AdherenceLevel } from "../types/prescription.types";

interface AdherenceProgressProps {
  percent: number;
  level: AdherenceLevel;
  label?: string;
}

const LEVEL_CLASS: Record<AdherenceLevel, string> = {
  good: "bg-[#1cba9f]",
  warning: "bg-amber-500",
  poor: "bg-red-500",
};

/**
 * Module 7 — thanh tỉ lệ tuân thủ (UC-11). Màu theo AdherenceLevel:
 * good = teal `#1cba9f`, warning = amber, poor = red.
 */
export function AdherenceProgress({ percent, level, label }: AdherenceProgressProps) {
  const safe = Math.max(0, Math.min(100, percent));
  return (
    <div className="w-full">
      {label && (
        <div className="mb-1 flex justify-between text-xs text-muted-foreground">
          <span>{label}</span>
          <span className="font-mono">{safe.toFixed(0)}%</span>
        </div>
      )}
      <div className="h-2 w-full overflow-hidden rounded-full bg-muted">
        <div
          className={`h-full transition-all ${LEVEL_CLASS[level]}`}
          style={{ width: `${safe}%` }}
          aria-label={`Adherence ${safe}%`}
        />
      </div>
    </div>
  );
}
