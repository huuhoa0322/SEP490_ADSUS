"use client";

import { useMemo, useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useParams, useRouter } from "next/navigation";
import {
  ArrowLeft,
  Pill,
  Bell,
  BellRing,
  Check,
  Clock,
  AlertCircle,
  CalendarClock,
  TrendingUp,
  ChevronDown,
  ChevronRight,
  AlertTriangle,
  CircleCheck,
  Hourglass,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import {
  getPatientPrescriptions,
  sendReminders,
  type TodayDoseDto,
  type PrescriptionCardDto,
} from "@/features/medication-tracking/api/medication-tracking.api";
import toast from "react-hot-toast";

// ─── Helpers ──────────────────────────────────────────────────────────────────

function SkeletonBox({ className = "" }: { className?: string }) {
  return (
    <div className={`animate-pulse rounded-md bg-muted ${className}`} />
  );
}

function doseStatusConfig(status: TodayDoseDto["status"]) {
  switch (status) {
    case "TAKEN":
      return {
        label: "Đã uống",
        pillClass: "bg-[var(--success)]/10 text-[var(--success)] border-[var(--success)]/40",
        icon: Check,
      };
    case "OVERTIME":
      return {
        label: "Quá giờ",
        pillClass: "bg-destructive/10 text-destructive border-destructive/20",
        icon: AlertCircle,
      };
    case "PENDING":
      return {
        label: "Chưa đến",
        pillClass: "bg-[#e0912f]/10 text-[#e0912f] border-[#e0912f]/20",
        icon: Clock,
      };
  }
}

function StatusChip({ status }: { status: TodayDoseDto["status"] }) {
  const cfg = doseStatusConfig(status);
  const Icon = cfg.icon;
  return (
    <span
      className={`inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs font-medium ${cfg.pillClass}`}
    >
      <Icon className="size-3" />
      {cfg.label}
    </span>
  );
}

function AdherenceBadge({
  taken,
  total,
  percent,
}: {
  taken: number;
  total: number;
  percent: number;
}) {
  const textClass =
    percent >= 80
      ? "text-[var(--success)]"
      : percent >= 50
        ? "text-[#e0912f]"
        : "text-destructive";
  return (
    <span className={`font-mono text-sm font-semibold ${textClass}`}>
      {taken}/{total}
      <span className="ml-1 text-xs font-normal opacity-70">({percent}%)</span>
    </span>
  );
}

// ─── Inferred prescription lifecycle (FE-only) ──────────────────────────────
//
// BE không có "Expired" status — chỉ có Active / Completed.
// FE suy luận trạng thái từ data có sẵn:
//
//   todayDoses.length > 0                                  → "today_active" (hôm nay có liều)
//   todayDoses rỗng + overallPercent >= 100               → "completed"
//   todayDoses rỗng + overallPercent < 80 + quá 30 ngày    → "stale_warning"
//   todayDoses rỗng + overallPercent < 80                 → "low_adherence"
//   todayDoses rỗng + overallPercent tốt                  → "dormant" (chưa đến liều tiếp theo)
//
type PrescriptionLifecycle =
  | "today_active"
  | "completed"
  | "stale_warning"
  | "low_adherence"
  | "dormant";

const lifecycleConfig: Record<
  PrescriptionLifecycle,
  {
    label: string;
    pillClass: string;
    icon: React.ElementType;
    rowClass: string;
  }
> = {
  today_active: {
    label: "Có liều hôm nay",
    pillClass: "bg-[var(--success)]/10 text-[var(--success)] border-[var(--success)]/40",
    icon: CalendarClock,
    rowClass: "",
  },
  completed: {
    label: "Hoàn thành",
    pillClass: "bg-[var(--success)]/10 text-[var(--success)] border-[var(--success)]/40",
    icon: CircleCheck,
    rowClass: "opacity-60",
  },
  stale_warning: {
    label: "Cảnh báo tuân thủ",
    pillClass: "bg-destructive/10 text-destructive border-destructive/30",
    icon: AlertTriangle,
    rowClass: "bg-destructive/5",
  },
  low_adherence: {
    label: "Tuân thủ thấp",
    pillClass: "bg-[#e0912f]/10 text-[#e0912f] border-[#e0912f]/30",
    icon: AlertTriangle,
    rowClass: "",
  },
  dormant: {
    label: "Đang chạy",
    pillClass: "bg-muted text-muted-foreground border-border",
    icon: Hourglass,
    rowClass: "",
  },
};

function inferLifecycle(
  prescription: PrescriptionCardDto,
  caseDate: Date,
  now: Date,
): PrescriptionLifecycle {
  const daysSinceCase = Math.floor(
    (now.getTime() - caseDate.getTime()) / (1000 * 60 * 60 * 24),
  );
  const overallPct = prescription.adherenceOverall.percent;

  if (prescription.todayDoses.length > 0) return "today_active";
  if (overallPct >= 100) return "completed";
  if (overallPct < 80 && daysSinceCase > 30) return "stale_warning";
  if (overallPct < 80) return "low_adherence";
  return "dormant";
}

function LifecycleChip({ state }: { state: PrescriptionLifecycle }) {
  const cfg = lifecycleConfig[state];
  const Icon = cfg.icon;
  return (
    <span
      className={`inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs font-medium ${cfg.pillClass}`}
    >
      <Icon className="size-3" />
      {cfg.label}
    </span>
  );
}

// ─── Dose details panel (expanded view) ──────────────────────────────────────

function DoseDetailsTable({ doses }: { doses: TodayDoseDto[] }) {
  if (doses.length === 0) {
    return (
      <div className="px-4 py-6 text-center text-sm text-muted-foreground">
        Hôm nay không có liều nào.
      </div>
    );
  }
  return (
    <table className="w-full text-sm">
      <thead className="bg-muted/40 text-xs uppercase tracking-wide text-muted-foreground">
        <tr>
          <th className="px-3 py-2 text-left font-medium">Giờ</th>
          <th className="px-3 py-2 text-left font-medium">Tên thuốc</th>
          <th className="hidden px-3 py-2 text-left font-medium sm:table-cell">
            Liều
          </th>
          <th className="px-3 py-2 text-right font-medium">Trạng thái</th>
        </tr>
      </thead>
      <tbody className="divide-y">
        {doses.map((dose) => (
          <tr key={dose.intakeId} className="hover:bg-muted/20">
            <td className="px-3 py-2.5 font-mono text-sm font-semibold">
              {dose.scheduledTime}
            </td>
            <td className="px-3 py-2.5 font-medium">{dose.medicineName}</td>
            <td className="hidden px-3 py-2.5 text-muted-foreground sm:table-cell">
              {dose.dosage || "—"}
            </td>
            <td className="px-3 py-2.5 text-right">
              <StatusChip status={dose.status} />
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

// ─── Compact row (single line per prescription) ──────────────────────────────

function PrescriptionRow({
  prescription,
  lifecycle,
}: {
  prescription: PrescriptionCardDto;
  lifecycle: PrescriptionLifecycle;
}) {
  const queryClient = useQueryClient();
  const params = useParams();
  const patientId = params.patientId as string;

  const [expanded, setExpanded] = useState(false);
  const pendingOrOverdue = prescription.todayDoses.filter(
    (d) => d.status === "PENDING" || d.status === "OVERTIME",
  ).length;
  const cfg = lifecycleConfig[lifecycle];

  const mutation = useMutation({
    mutationFn: () =>
      sendReminders(patientId, { prescriptionId: prescription.prescriptionId }),
    onSuccess: (res) => {
      toast.success(res.message);
      queryClient.invalidateQueries({
        queryKey: ["doctor-medication-tracking", "prescriptions", patientId],
      });
    },
    onError: () => {
      toast.error("Không gửi được nhắc nhở. Vui lòng thử lại.");
    },
  });

  const canRemind = pendingOrOverdue > 0 && lifecycle !== "completed";

  return (
    <>
      <tr
        className={`cursor-pointer border-b transition-colors hover:bg-muted/30 ${cfg.rowClass}`}
        onClick={() => setExpanded((v) => !v)}
      >
        {/* Expand caret */}
        <td className="w-8 px-2 py-3">
          {expanded ? (
            <ChevronDown className="size-4 text-muted-foreground" />
          ) : (
            <ChevronRight className="size-4 text-muted-foreground" />
          )}
        </td>

        {/* Case name + lifecycle */}
        <td className="px-3 py-3">
          <div className="flex flex-col gap-1">
            <span className="truncate font-medium">{prescription.caseName}</span>
            <LifecycleChip state={lifecycle} />
          </div>
        </td>

        {/* Today */}
        <td className="hidden px-3 py-3 sm:table-cell">
          {prescription.adherenceToday.total === 0 ? (
            <span className="text-xs text-muted-foreground">—</span>
          ) : (
            <AdherenceBadge
              taken={prescription.adherenceToday.taken}
              total={prescription.adherenceToday.total}
              percent={prescription.adherenceToday.percent}
            />
          )}
        </td>

        {/* Overall */}
        <td className="hidden px-3 py-3 md:table-cell">
          {prescription.adherenceOverall.total === 0 ? (
            <span className="text-xs text-muted-foreground">—</span>
          ) : (
            <AdherenceBadge
              taken={prescription.adherenceOverall.taken}
              total={prescription.adherenceOverall.total}
              percent={prescription.adherenceOverall.percent}
            />
          )}
        </td>

        {/* Remind action */}
        <td className="px-3 py-3 text-right">
          <Button
            size="sm"
            variant="outline"
            className="gap-1.5 border-amber-200 bg-amber-50 text-amber-700 hover:bg-amber-100"
            disabled={!canRemind || mutation.isPending}
            onClick={(e) => {
              e.stopPropagation();
              mutation.mutate();
            }}
          >
            {mutation.isPending ? (
              <BellRing className="size-3.5 animate-pulse" />
            ) : (
              <Bell className="size-3.5" />
            )}
            {pendingOrOverdue > 0 ? `Nhắc (${pendingOrOverdue})` : "Nhắc"}
          </Button>
        </td>
      </tr>

      {/* Expanded detail */}
      {expanded && (
        <tr className="border-b bg-muted/10">
          <td colSpan={5} className="p-0">
            <div className="overflow-hidden rounded-b-lg border-x border-b">
              <DoseDetailsTable doses={prescription.todayDoses} />
            </div>
          </td>
        </tr>
      )}
    </>
  );
}

// ─── Date grouping ───────────────────────────────────────────────────────────

function extractCaseDate(caseName: string): Date | null {
  const match = caseName.match(/(\d{2})\/(\d{2})\/(\d{4})$/);
  if (!match) return null;
  const [, day, month, year] = match;
  return new Date(Number(year), Number(month) - 1, Number(day));
}

function formatDateHeader(date: Date): string {
  return `Ngày ${String(date.getDate()).padStart(2, "0")}/${String(date.getMonth() + 1).padStart(2, "0")}/${date.getFullYear()}`;
}

// ─── Patient summary stats ───────────────────────────────────────────────────

interface PatientStats {
  totalPrescriptions: number;
  totalDosesToday: number;
  takenToday: number;
  overdueToday: number;
  warningCount: number;
  completedCount: number;
}

function deriveStats(prescriptions: PrescriptionCardDto[]): PatientStats {
  let totalDosesToday = 0;
  let takenToday = 0;
  let overdueToday = 0;
  prescriptions.forEach((p) => {
    totalDosesToday += p.adherenceToday.total;
    takenToday += p.adherenceToday.taken;
    p.todayDoses.forEach((d) => {
      if (d.status === "OVERTIME") overdueToday++;
    });
  });
  const overallPercent =
    totalDosesToday > 0
      ? Math.round((takenToday / totalDosesToday) * 100)
      : 0;
  return {
    totalPrescriptions: prescriptions.length,
    totalDosesToday,
    takenToday,
    overdueToday,
    warningCount: overallPercent < 80 && prescriptions.length > 0 ? 1 : 0,
    completedCount: prescriptions.filter(
      (p) => p.adherenceOverall.percent >= 100,
    ).length,
  };
}

function PatientHeader({
  name,
  stats,
}: {
  name: string;
  stats: PatientStats;
}) {
  return (
    <div className="mb-6 rounded-xl border bg-card p-5 shadow-sm">
      <div className="flex flex-wrap items-center gap-4">
        <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-primary text-xl font-semibold text-primary-foreground">
          {name.charAt(0).toUpperCase()}
        </div>
        <div className="min-w-0 flex-1">
          <h1 className="font-heading text-2xl font-semibold text-primary">
            {name}
          </h1>
          <p className="mt-0.5 text-sm text-muted-foreground">
            Theo dõi tiến độ uống thuốc
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-x-5 gap-y-2 text-sm">
          <div className="flex items-center gap-1.5">
            <Pill className="size-4 text-muted-foreground" />
            <span className="font-mono font-semibold">
              {stats.totalPrescriptions}
            </span>
            <span className="text-muted-foreground">đơn</span>
          </div>
          <div className="flex items-center gap-1.5">
            <CalendarClock className="size-4 text-muted-foreground" />
            <span className="font-mono font-semibold">
              {stats.takenToday}/{stats.totalDosesToday}
            </span>
            <span className="text-muted-foreground">liều hôm nay</span>
          </div>
          {stats.completedCount > 0 && (
            <div className="flex items-center gap-1.5 rounded-full bg-[var(--success)]/10 px-2.5 py-1">
              <CircleCheck className="size-4 text-[var(--success)]" />
              <span className="font-mono font-semibold text-[var(--success)]">
                {stats.completedCount}
              </span>
              <span className="text-[var(--success)]">hoàn thành</span>
            </div>
          )}
          {stats.overdueToday > 0 && (
            <div className="flex items-center gap-1.5 rounded-full bg-destructive/10 px-2.5 py-1">
              <AlertCircle className="size-4 text-destructive" />
              <span className="font-mono font-semibold text-destructive">
                {stats.overdueToday}
              </span>
              <span className="text-destructive">quá giờ hôm nay</span>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// ─── Page ────────────────────────────────────────────────────────────────────

export default function PatientPrescriptionDetailPage() {
  const params = useParams();
  const router = useRouter();
  const patientId = params.patientId as string;

  const { data, isLoading, isError } = useQuery({
    queryKey: ["doctor-medication-tracking", "prescriptions", patientId],
    queryFn: () => getPatientPrescriptions(patientId),
    enabled: !!patientId,
  });

  const now = useMemo(() => new Date(), []);

  const stats = useMemo(
    () => deriveStats(data?.prescriptions ?? []),
    [data?.prescriptions],
  );

  // Annotate prescriptions with lifecycle + case date, then group by date
  const grouped = useMemo(() => {
    type Annotated = {
      prescription: PrescriptionCardDto;
      lifecycle: PrescriptionLifecycle;
      caseDate: Date | null;
    };
    const annotated: Annotated[] = (data?.prescriptions ?? []).map((p) => ({
      prescription: p,
      lifecycle: inferLifecycle(p, extractCaseDate(p.caseName) ?? now, now),
      caseDate: extractCaseDate(p.caseName),
    }));

    return annotated.reduce<Record<string, Annotated[]>>((acc, a) => {
      const key = a.caseDate
        ? a.caseDate.toISOString().slice(0, 10)
        : "unknown";
      if (!acc[key]) acc[key] = [];
      acc[key].push(a);
      return acc;
    }, {});
  }, [data?.prescriptions, now]);

  const sortedDates = Object.keys(grouped).sort((a, b) => {
    if (a === "unknown") return 1;
    if (b === "unknown") return -1;
    return new Date(b).getTime() - new Date(a).getTime();
  });

  return (
    <div className="w-full px-4 py-6 sm:px-6">
      {/* Back */}
      <div className="mb-4 flex items-center gap-2">
        <Button
          variant="ghost"
          size="sm"
          className="gap-1.5 text-muted-foreground"
          onClick={() => router.back()}
        >
          <ArrowLeft className="size-4" />
          Quay lại
        </Button>
      </div>

      {isError ? (
        <div className="flex flex-col items-center justify-center gap-4 py-20">
          <p className="text-muted-foreground">
            Không tải được thông tin đơn thuốc.
          </p>
          <Button variant="outline" onClick={() => router.back()}>
            <ArrowLeft className="size-4" />
            Quay lại
          </Button>
        </div>
      ) : isLoading ? (
        <div className="space-y-4">
          <SkeletonBox className="h-24 w-full rounded-xl" />
          <SkeletonBox className="h-64 w-full rounded-xl" />
        </div>
      ) : !data?.prescriptions.length ? (
        <>
          <PatientHeader name={data?.patientName ?? "Bệnh nhân"} stats={stats} />
          <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-muted-foreground/20 py-16 text-muted-foreground">
            <Pill className="mb-2 size-10 opacity-30" />
            <p>Không có đơn thuốc Active nào.</p>
          </div>
        </>
      ) : (
        <>
          <PatientHeader name={data.patientName} stats={stats} />

          {/* Compact grouped table */}
          <Card className="overflow-hidden">
            <CardContent className="p-0">
              {sortedDates.map((dateKey) => {
                const group = grouped[dateKey];
                const groupDate = group[0]?.caseDate;
                const dayTotalDoses = group.reduce(
                  (sum, a) => sum + a.prescription.adherenceToday.total,
                  0,
                );
                const dayTaken = group.reduce(
                  (sum, a) => sum + a.prescription.adherenceToday.taken,
                  0,
                );
                return (
                  <section key={dateKey}>
                    <div className="flex flex-wrap items-center justify-between gap-2 border-b bg-muted/30 px-4 py-2 text-xs">
                      <div className="flex items-center gap-2 font-semibold uppercase tracking-wide text-muted-foreground">
                        <span className="inline-block h-1.5 w-1.5 rounded-full bg-muted-foreground" />
                        {groupDate
                          ? formatDateHeader(groupDate)
                          : "Chưa rõ ngày"}
                      </div>
                      {dayTotalDoses > 0 && (
                        <span className="text-muted-foreground">
                          Hôm nay:{" "}
                          <span className="font-mono font-semibold text-foreground">
                            {dayTaken}/{dayTotalDoses}
                          </span>{" "}
                          liều · {group.length} đơn
                        </span>
                      )}
                    </div>

                    <div className="overflow-x-auto">
                      <table className="w-full text-sm">
                        <thead className="bg-muted/20 text-xs uppercase tracking-wide text-muted-foreground">
                          <tr>
                            <th className="w-8 px-2 py-2"></th>
                            <th className="px-3 py-2 text-left font-medium">
                              Đơn thuốc
                            </th>
                            <th className="hidden px-3 py-2 text-left font-medium sm:table-cell">
                              Hôm nay
                            </th>
                            <th className="hidden px-3 py-2 text-left font-medium md:table-cell">
                              Toàn đơn
                            </th>
                            <th className="px-3 py-2 text-right font-medium">
                              Hành động
                            </th>
                          </tr>
                        </thead>
                        <tbody>
                          {group.map(({ prescription, lifecycle }) => (
                            <PrescriptionRow
                              key={prescription.prescriptionId}
                              prescription={prescription}
                              lifecycle={lifecycle}
                            />
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </section>
                );
              })}
            </CardContent>
          </Card>
        </>
      )}
    </div>
  );
}
