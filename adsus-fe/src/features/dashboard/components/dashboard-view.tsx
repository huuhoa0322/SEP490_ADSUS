"use client";

import { AlertCircle, CalendarCheck, Loader2, ScanLine, Users } from "lucide-react";
import { useState } from "react";

import { getApiErrorMessage } from "@/lib/api-client";

import { useDashboardStatistics } from "../hooks/use-dashboard";

import {
  BarList,
  ChartCard,
  RateMeter,
  StatTile,
  StatusBreakdown,
} from "./chart-primitives";

/** Các mốc thời gian bấm nhanh, tính lùi từ hôm nay. */
const PRESETS = [
  { label: "7 ngày", days: 7 },
  { label: "30 ngày", days: 30 },
  { label: "90 ngày", days: 90 },
] as const;

function isoDaysAgo(days: number): string {
  const date = new Date();
  date.setDate(date.getDate() - days);
  return date.toISOString().slice(0, 10);
}

const TODAY = new Date().toISOString().slice(0, 10);

/**
 * SCR-08 — thống kê vận hành hệ thống (UC-05).
 *
 * BR-01: mọi con số đều đã tổng hợp, không có tên hay số điện thoại của ai.
 * BR-02: chỉ đọc — màn này không có nút nào thay đổi dữ liệu.
 * BR-03: chỉ Admin vào được, chặn ở AuthGuard và ở backend.
 */
export function DashboardView() {
  const [fromDate, setFromDate] = useState(isoDaysAgo(30));
  const [toDate, setToDate] = useState(TODAY);

  const { data, isLoading, isError, error } = useDashboardStatistics({ fromDate, toDate });

  function applyPreset(days: number) {
    setFromDate(isoDaysAgo(days));
    setToDate(TODAY);
  }

  return (
    <div className="mx-auto max-w-7xl px-6 py-10">
      <div className="flex flex-wrap items-end justify-between gap-5">
        <div>
          <h1 className="font-heading text-[32px] font-bold tracking-[-0.02em] text-foreground">
            Thống kê hệ thống
          </h1>
          <p className="mt-1.5 text-[15px] text-muted-foreground">
            Số liệu vận hành đã tổng hợp. Màn này không hiển thị thông tin cá nhân của bệnh
            nhân.
          </p>
        </div>

        {/* Bộ lọc thời gian gom về một hàng ngay trên các biểu đồ. */}
        <div className="flex flex-wrap items-center gap-2">
          {PRESETS.map((preset) => (
            <button
              key={preset.label}
              type="button"
              onClick={() => applyPreset(preset.days)}
              className="rounded-full border border-border px-4 py-2 text-sm transition-colors hover:bg-secondary"
            >
              {preset.label}
            </button>
          ))}

          <input
            type="date"
            value={fromDate}
            max={toDate}
            onChange={(e) => setFromDate(e.target.value)}
            aria-label="Từ ngày"
            className="rounded-full border border-border bg-background px-4 py-2 text-sm outline-none focus:border-accent"
          />
          <span className="text-muted-foreground">→</span>
          <input
            type="date"
            value={toDate}
            min={fromDate}
            max={TODAY}
            onChange={(e) => setToDate(e.target.value)}
            aria-label="Đến ngày"
            className="rounded-full border border-border bg-background px-4 py-2 text-sm outline-none focus:border-accent"
          />
        </div>
      </div>

      {isError && (
        <div
          role="alert"
          className="mt-6 flex items-start gap-2.5 rounded-2xl border border-destructive/25 bg-destructive/5 px-4 py-3 text-sm text-destructive"
        >
          <AlertCircle aria-hidden className="mt-0.5 size-4 shrink-0" />
          <span>{getApiErrorMessage(error, "Không tải được số liệu thống kê.")}</span>
        </div>
      )}

      {isLoading && !data && (
        <div className="flex min-h-72 items-center justify-center">
          <Loader2 className="size-6 animate-spin text-muted-foreground" />
        </div>
      )}

      {data && (
        <>
          {/* Bốn con số quan trọng nhất — tự nó đã đủ nghĩa, không cần vẽ biểu đồ. */}
          <div className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <StatTile
              label="Tài khoản"
              value={data.accounts.total}
              hint={`${data.accounts.newInRange} tài khoản mới trong kỳ`}
              icon={<Users className="size-5 text-muted-foreground" />}
            />
            <StatTile
              label="Ca khám"
              value={data.clinical.caseCount}
              hint={`${data.clinical.aiRunCount} lượt chạy AI`}
              icon={<ScanLine className="size-5 text-muted-foreground" />}
            />
            <StatTile
              label="Lượt đặt lịch"
              value={data.appointments.bookedCount + data.appointments.cancelledCount}
              hint={`${data.appointments.slotCount} khung giờ đã mở`}
              icon={<CalendarCheck className="size-5 text-muted-foreground" />}
            />
            <StatTile
              label="Liều thuốc theo dõi"
              value={data.adherence.scheduledDoseCount}
              hint={`${data.adherence.takenDoseCount} liều đã xác nhận uống`}
            />
          </div>

          <div className="mt-5 grid gap-5 lg:grid-cols-2">
            <ChartCard
              title="Tài khoản theo vai trò"
              description="Tính trên toàn hệ thống, không phụ thuộc khoảng thời gian đang chọn."
            >
              <BarList
                items={[
                  { label: "Bác sĩ", value: data.accounts.doctorCount },
                  { label: "Điều dưỡng", value: data.accounts.nurseCount },
                  { label: "Bệnh nhân", value: data.accounts.patientCount },
                  { label: "Quản trị viên", value: data.accounts.adminCount },
                ]}
              />
            </ChartCard>

            <ChartCard
              title="Tình trạng tài khoản"
              description={`${data.accounts.activeRate}% tài khoản đang hoạt động.`}
            >
              <StatusBreakdown
                segments={[
                  { label: "Đang hoạt động", value: data.accounts.activeCount, tone: "good" },
                  { label: "Đã khoá", value: data.accounts.lockedCount, tone: "warning" },
                  {
                    label: "Đã vô hiệu hoá",
                    value: data.accounts.deactivatedCount,
                    tone: "critical",
                  },
                ]}
              />
            </ChartCard>

            <ChartCard
              title="Kết quả AI"
              description="Bệnh nhân chỉ thấy được kết quả sau khi bác sĩ xác nhận."
            >
              <StatusBreakdown
                segments={[
                  {
                    label: "Bác sĩ đã xác nhận",
                    value: data.clinical.aiConfirmedCount,
                    tone: "good",
                  },
                  {
                    label: "Chờ bác sĩ duyệt",
                    value: data.clinical.aiPendingCount,
                    tone: "neutral",
                  },
                  {
                    label: "Bác sĩ đã từ chối",
                    value: data.clinical.aiRejectedCount,
                    tone: "critical",
                  },
                ]}
              />
              <p className="mt-5 border-t border-border pt-4 text-sm text-muted-foreground">
                Tỉ lệ xác nhận{" "}
                <span className="font-600 tabular-nums text-foreground">
                  {data.clinical.aiConfirmRate}%
                </span>{" "}
                — tính trên số kết quả đã duyệt, không tính phần còn đang chờ.
              </p>
            </ChartCard>

            <ChartCard
              title="Lịch hẹn"
              description={`Trung bình ${data.appointments.averageBookingsPerSlot} lượt đặt trên mỗi khung giờ.`}
            >
              <StatusBreakdown
                segments={[
                  { label: "Đã đặt", value: data.appointments.bookedCount, tone: "good" },
                  {
                    label: "Đã huỷ",
                    value: data.appointments.cancelledCount,
                    tone: "critical",
                  },
                ]}
              />
            </ChartCard>
          </div>

          <div className="mt-5">
            <ChartCard
              title="Tuân thủ uống thuốc"
              description="Tỉ lệ liều thuốc được bệnh nhân xác nhận đã uống, tính chung toàn phòng khám."
            >
              <RateMeter
                value={data.adherence.adherenceRate}
                caption={`${data.adherence.takenDoseCount} trên ${data.adherence.scheduledDoseCount} liều được hẹn trong kỳ.`}
              />
            </ChartCard>
          </div>

          <p className="mt-6 text-xs text-muted-foreground">
            Số liệu từ {data.fromDate} đến {data.toDate}. Tài khoản được tính trên toàn hệ
            thống; các chỉ số còn lại chỉ tính trong khoảng thời gian này.
          </p>
        </>
      )}
    </div>
  );
}
