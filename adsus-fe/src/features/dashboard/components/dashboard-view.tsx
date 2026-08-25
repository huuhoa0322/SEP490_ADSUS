"use client";

import { AlertCircle, CalendarCheck, Loader2, ScanLine, Users } from "lucide-react";
import { useState } from "react";

import { getApiErrorMessage } from "@/lib/api-client";

import { useDashboardStatistics } from "../hooks/use-dashboard";

import { AuditLogPanel } from "./audit-log-panel";
import { BarList, ChartCard, DonutChart, RateMeter, StatTile, StatusBreakdown } from "./chart-primitives";
import { APPOINTMENT_SERIES, GroupedBarChart } from "./trend-chart";

/** Các mốc thời gian bấm nhanh, tính lùi từ hôm nay. */
const PRESETS = [
  { label: "7 ngày", days: 7 },
  { label: "30 ngày", days: 30 },
  { label: "90 ngày", days: 90 },
] as const;

/**
 * Ngày theo lịch ĐỊA PHƯƠNG, dạng yyyy-MM-dd.
 */
function toIsoDate(date: Date): string {
  const thang = `${date.getMonth() + 1}`.padStart(2, "0");
  const ngay = `${date.getDate()}`.padStart(2, "0");
  return `${date.getFullYear()}-${thang}-${ngay}`;
}

/**
 * Mốc đầu của khoảng N ngày gần nhất, TÍNH CẢ HÔM NAY.
 */
function isoDaysAgo(days: number): string {
  const date = new Date();
  date.setDate(date.getDate() - (days - 1));
  return toIsoDate(date);
}

const TODAY = toIsoDate(new Date());

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
    <div className="mx-auto w-full max-w-screen-2xl px-6 py-8">
      {/* Page header */}
      <div className="flex flex-wrap items-end justify-between gap-5">
        <div>
          <h1 className="font-heading text-[28px] font-bold tracking-[-0.02em] text-foreground">
            Admin Dashboard
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Số liệu vận hành đã tổng hợp. Không hiển thị thông tin cá nhân của bệnh nhân.
          </p>
        </div>

        {/* Date range filter */}
        <div className="flex flex-wrap items-center gap-2">
          {PRESETS.map((preset) => (
            <button
              key={preset.label}
              type="button"
              onClick={() => applyPreset(preset.days)}
              className="rounded-full border border-[var(--border)] px-4 py-2 text-sm font-medium text-foreground transition-colors hover:bg-[var(--secondary)]"
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
            className="rounded-full border border-[var(--border)] bg-background px-4 py-2 text-sm outline-none focus:border-[var(--cat-navy)]"
          />
          <span className="text-muted-foreground">→</span>
          <input
            type="date"
            value={toDate}
            min={fromDate}
            max={TODAY}
            onChange={(e) => setToDate(e.target.value)}
            aria-label="Đến ngày"
            className="rounded-full border border-[var(--border)] bg-background px-4 py-2 text-sm outline-none focus:border-[var(--cat-navy)]"
          />
        </div>
      </div>

      {isError && (
        <div
          role="alert"
          className="mt-6 flex items-start gap-2.5 rounded-xl border border-destructive/25 bg-destructive/5 px-4 py-3 text-sm text-destructive"
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
          {/* ── Row 1: 4 stat tiles ───────────────────────────────────── */}
          <div className="mt-7 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <StatTile
              label="Tài khoản"
              value={data.accounts.total}
              hint={`${data.accounts.newInRange} tài khoản mới trong kỳ`}
              icon={<Users className="size-5" />}
              cat="navy"
              trend={`+${data.accounts.newInRange} mới`}
            />
            <StatTile
              label="Ca khám"
              value={data.clinical.caseCount}
              hint={`${data.clinical.aiRunCount} lượt chạy AI`}
              icon={<ScanLine className="size-5" />}
              cat="teal"
              trend={`${data.clinical.aiRunCount} lượt AI`}
            />
            <StatTile
              label="Lượt đặt lịch"
              value={data.appointments.bookedCount + data.appointments.cancelledCount}
              hint={`${data.appointments.slotCount} khung giờ đã mở`}
              icon={<CalendarCheck className="size-5" />}
              cat="magenta"
              trend={`${data.appointments.bookedCount} đã đặt`}
            />
            <StatTile
              label="Liều thuốc theo dõi"
              value={data.adherence.scheduledDoseCount}
              hint={`${data.adherence.takenDoseCount} liều đã xác nhận uống`}
              icon={<ScanLine className="size-5" />}
              cat="blue"
              trend={`${data.adherence.adherenceRate}% tuân thủ`}
            />
          </div>

          {/* ── Row 2: Appointment Statistics (full-width grouped bar) ── */}
          <div className="mt-5">
            <ChartCard title="Appointment Statistics" description="Biểu đồ lượt hẹn theo ngày">
              <GroupedBarChart
                points={data.trend}
                series={APPOINTMENT_SERIES}
                title=""
              />
            </ChartCard>
          </div>

          {/* ── Row 3: 2-col + 1-col cards ──────────────────────────── */}
          <div className="mt-5 grid gap-5 lg:grid-cols-3">
            {/* Left: Tài khoản theo vai trò */}
            <ChartCard
              title="Tài khoản theo vai trò"
              description="Tính trên toàn hệ thống."
              className="lg:col-span-1"
            >
              <DonutChart
                segments={[
                  { label: "Bác sĩ", value: data.accounts.doctorCount, color: "var(--cat-navy)" },
                  { label: "Điều dưỡng", value: data.accounts.nurseCount, color: "var(--cat-teal)" },
                  { label: "Bệnh nhân", value: data.accounts.patientCount, color: "var(--cat-magenta)" },
                  { label: "Quản trị", value: data.accounts.adminCount, color: "var(--cat-amber)" },
                ]}
              />
            </ChartCard>

            {/* Center: Tài khoản (bar list) */}
            <ChartCard
              title="Phân bổ tài khoản"
              description="Theo vai trò trong hệ thống."
              className="lg:col-span-1"
            >
              <BarList
                items={[
                  { label: "Bác sĩ", value: data.accounts.doctorCount },
                  { label: "Điều dưỡng", value: data.accounts.nurseCount },
                  { label: "Bệnh nhân", value: data.accounts.patientCount },
                  { label: "Quản trị", value: data.accounts.adminCount },
                ]}
                colors={["navy", "teal", "magenta", "amber"]}
              />
            </ChartCard>

            {/* Right: Lịch hẹn */}
            <ChartCard
              title="Lịch hẹn"
              description={`${data.appointments.slotCount} khung giờ đã mở trong kỳ.`}
              className="lg:col-span-1"
            >
              <StatusBreakdown
                segments={[
                  { label: "Đã đặt", value: data.appointments.bookedCount, tone: "good" },
                  { label: "Đã huỷ", value: data.appointments.cancelledCount, tone: "critical" },
                ]}
              />
            </ChartCard>
          </div>

          {/* ── Row 4: AI accuracy + adherence ──────────────────────── */}
          <div className="mt-5 grid gap-5 lg:grid-cols-2">
            <ChartCard
              title="Độ chính xác AI"
              description={`Phiên bản: ${data.activeAiModel.versionCode || "Không có"}`}
            >
              <div className="mt-4 flex items-center gap-6 text-center justify-around">
                <div>
                  <div className="text-3xl font-bold font-heading text-foreground tabular-nums">
                    {data.activeAiModel.precision != null
                      ? (data.activeAiModel.precision * 100).toFixed(1) + "%"
                      : (
                        <span className="text-lg text-muted-foreground">
                          Chưa có<br />dữ liệu
                        </span>
                      )}
                  </div>
                  <div className="mt-1 text-sm font-medium text-muted-foreground">Precision</div>
                </div>
                <div className="h-12 w-px bg-[var(--border)]" />
                <div>
                  <div className="text-3xl font-bold font-heading text-foreground tabular-nums">
                    {data.activeAiModel.recall != null
                      ? (data.activeAiModel.recall * 100).toFixed(1) + "%"
                      : (
                        <span className="text-lg text-muted-foreground">
                          Chưa có<br />dữ liệu
                        </span>
                      )}
                  </div>
                  <div className="mt-1 text-sm font-medium text-muted-foreground">Recall</div>
                </div>
                <div className="h-12 w-px bg-[var(--border)]" />
                <div>
                  <div className="text-3xl font-bold font-heading text-[var(--cat-teal)] tabular-nums">
                    {data.activeAiModel.map50 != null
                      ? data.activeAiModel.map50.toFixed(1) + "%"
                      : (
                        <span className="text-lg text-muted-foreground">
                          Chưa có<br />dữ liệu
                        </span>
                      )}
                  </div>
                  <div className="mt-1 text-sm font-medium text-muted-foreground">mAP50</div>
                </div>
              </div>
              {data.activeAiModel.lastEvaluatedAt && (
                <p className="mt-6 border-t border-[var(--border)] pt-4 text-center text-xs text-muted-foreground">
                  mAP50 tính lần cuối: {new Date(data.activeAiModel.lastEvaluatedAt).toLocaleString("vi-VN")}
                </p>
              )}
            </ChartCard>

            <ChartCard
              title="Tuân thủ uống thuốc"
              description="Tỉ lệ liều thuốc được xác nhận đã uống."
            >
              <div className="flex items-start gap-6">
                <div className="flex-1">
                  <RateMeter
                    value={data.adherence.adherenceRate}
                    caption={`${data.adherence.takenDoseCount} trên ${data.adherence.scheduledDoseCount} liều được hẹn trong kỳ.`}
                    tone={data.adherence.adherenceRate >= 80 ? "good" : "warning"}
                  />
                </div>
                <div className="flex flex-col items-center gap-3 pt-2">
                  <div className="flex size-14 items-center justify-center rounded-2xl bg-[var(--cat-teal)]/10">
                    <span className="text-2xl font-bold text-[var(--cat-teal)] tabular-nums">
                      {data.adherence.takenDoseCount}
                    </span>
                  </div>
                  <span className="text-xs text-muted-foreground">đã uống</span>
                  <div className="flex size-14 items-center justify-center rounded-2xl bg-[var(--cat-magenta)]/10">
                    <span className="text-2xl font-bold text-[var(--cat-magenta)] tabular-nums">
                      {data.adherence.scheduledDoseCount - data.adherence.takenDoseCount}
                    </span>
                  </div>
                  <span className="text-xs text-muted-foreground">chưa uống</span>
                </div>
              </div>
            </ChartCard>
          </div>

          {/* ── Row 5: 3 trend mini-charts ─────────────────────────── */}
          <div className="mt-5 grid gap-4 sm:grid-cols-3">
            <div className="rounded-2xl border border-[var(--border)] bg-background p-6">
              <div className="mb-3 flex items-baseline justify-between">
                <span className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                  Tài khoản mới
                </span>
                <span className="text-xs tabular-nums text-muted-foreground">
                  {data.accounts.newInRange} / kỳ
                </span>
              </div>
              <div className="flex items-end gap-1.5">
                {data.trend.slice(-14).map((p, i) => {
                  const maxVal = Math.max(...data.trend.map((pt) => pt.newAccounts), 1);
                  const h = Math.max(4, (p.newAccounts / maxVal) * 48);
                  return (
                    <div
                      key={i}
                      className="flex-1 rounded-sm bg-[var(--cat-navy)] transition-all hover:opacity-80"
                      style={{ height: `${h}px` }}
                      title={`${p.date}: ${p.newAccounts}`}
                    />
                  );
                })}
              </div>
            </div>

            <div className="rounded-2xl border border-[var(--border)] bg-background p-6">
              <div className="mb-3 flex items-baseline justify-between">
                <span className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                  Ca khám
                </span>
                <span className="text-xs tabular-nums text-muted-foreground">
                  {data.clinical.caseCount} / kỳ
                </span>
              </div>
              <div className="flex items-end gap-1.5">
                {data.trend.slice(-14).map((p, i) => {
                  const maxVal = Math.max(...data.trend.map((pt) => pt.cases), 1);
                  const h = Math.max(4, (p.cases / maxVal) * 48);
                  return (
                    <div
                      key={i}
                      className="flex-1 rounded-sm bg-[var(--cat-teal)] transition-all hover:opacity-80"
                      style={{ height: `${h}px` }}
                      title={`${p.date}: ${p.cases}`}
                    />
                  );
                })}
              </div>
            </div>

            <div className="rounded-2xl border border-[var(--border)] bg-background p-6">
              <div className="mb-3 flex items-baseline justify-between">
                <span className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                  Lượt hẹn
                </span>
                <span className="text-xs tabular-nums text-muted-foreground">
                  {data.appointments.bookedCount} / kỳ
                </span>
              </div>
              <div className="flex items-end gap-1.5">
                {data.trend.slice(-14).map((p, i) => {
                  const maxVal = Math.max(...data.trend.map((pt) => pt.appointments), 1);
                  const h = Math.max(4, (p.appointments / maxVal) * 48);
                  return (
                    <div
                      key={i}
                      className="flex-1 rounded-sm bg-[var(--cat-magenta)] transition-all hover:opacity-80"
                      style={{ height: `${h}px` }}
                      title={`${p.date}: ${p.appointments}`}
                    />
                  );
                })}
              </div>
            </div>
          </div>

          {/* ── Audit Log Panel ─────────────────────────────────────── */}
          <div className="mt-5">
            <AuditLogPanel />
          </div>

          <p className="mt-6 text-xs text-muted-foreground">
            Số liệu từ {data.fromDate} đến {data.toDate}. Tài khoản tính trên toàn hệ thống;
            các chỉ số còn lại chỉ tính trong khoảng thời gian này.
          </p>
        </>
      )}
    </div>
  );
}
