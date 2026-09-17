import { clsx, type ClassValue } from "clsx"
import { twMerge } from "tailwind-merge"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function formatCurrency(value: number): string {
  return new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
  }).format(value);
}

export interface FormatMetricPercentOptions {
  isRatio?: boolean;
  decimals?: number;
  fallback?: string;
}

/**
 * Formats a metric number (ratio 0..1 or percentage 0..100) into a clamped percentage string.
 * Returns "Chưa có dữ liệu" for null, undefined, NaN, or non-finite values.
 */
export function formatMetricPercent(
  value?: number | null,
  isRatioOrOptions: boolean | FormatMetricPercentOptions = false,
  decimals = 1
): string {
  let isRatio = false;
  let dec = decimals;
  let fallback = "Chưa có dữ liệu";

  if (typeof isRatioOrOptions === "object" && isRatioOrOptions !== null) {
    isRatio = isRatioOrOptions.isRatio ?? false;
    dec = isRatioOrOptions.decimals ?? decimals;
    fallback = isRatioOrOptions.fallback ?? fallback;
  } else {
    isRatio = Boolean(isRatioOrOptions);
  }

  if (value === null || value === undefined || typeof value !== "number" || Number.isNaN(value) || !Number.isFinite(value)) {
    return fallback;
  }

  const percentage = isRatio ? value * 100 : value;
  const clamped = Math.min(100, Math.max(0, percentage));
  return `${clamped.toFixed(dec)}%`;
}

/**
 * Checks whether a given string contains HTML tags.
 * Matches HTML open, close, and self-closing tags (e.g. <html>, <script>, <div>, </b>, <br/>, <img ...>).
 * Safely allows valid medical notations such as "< 3 ngày", "<38.5°C", "SpO2 > 95%".
 */
export function containsHtmlTags(input?: string | null): boolean {
  if (!input) return false;
  return /<[a-zA-Z\/][^>]*>/.test(input);
}

