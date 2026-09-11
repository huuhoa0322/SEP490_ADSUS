"use client";

import { useState, useDeferredValue, useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { Pill, AlertCircle, CheckCircle2, Users, TrendingUp, Clock, FileText, ChevronLeft, ChevronRight } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  getPatientList,
  type DoctorPatientDto,
} from "@/features/medication-tracking/api/medication-tracking.api";

const adherenceLevelLabels: Record<string, { label: string; color: "default" | "secondary" | "destructive" }> = {
  good: { label: "Tốt", color: "default" },
  warning: { label: "Trung bình", color: "secondary" },
  poor: { label: "Kém", color: "destructive" },
};

// ─── Summary stats derived from patient list ──────────────────────────────────
interface SummaryStats {
  totalPatients: number;
  avgAdherencePercent: number;
  overdueCount: number;
  activePrescriptionCount: number;
}

function deriveStats(patients: DoctorPatientDto[]): SummaryStats {
  const totalPatients = patients.length;
  const avgAdherencePercent =
    totalPatients > 0
      ? Math.round(
          patients.reduce((sum, p) => sum + p.todayAdherencePercent, 0) /
            totalPatients,
        )
      : 0;
  const overdueCount = patients.filter((p) => p.hasOverdueToday).length;
  const activePrescriptionCount = patients.reduce(
    (sum, p) => sum + p.activePrescriptionCount,
    0,
  );
  return { totalPatients, avgAdherencePercent, overdueCount, activePrescriptionCount };
}

// ─── Stat tile ────────────────────────────────────────────────────────────────
function StatTile({
  icon: Icon,
  label,
  value,
  sub,
  accent,
}: {
  icon: React.ElementType;
  label: string;
  value: string | number;
  sub?: string;
  accent?: "good" | "warning" | "critical";
}) {
  const accentColor = {
    good: "bg-[var(--success)]/10 text-[var(--success)]",
    warning: "bg-[#e0912f]/10 text-[#e0912f]",
    critical: "bg-destructive/10 text-destructive",
  }[accent ?? "good"];

  return (
    <div className="flex items-center gap-3 rounded-xl border bg-card p-4 shadow-sm">
      <div className={`rounded-lg p-2.5 ${accentColor}`}>
        <Icon className="size-5" />
      </div>
      <div>
        <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
          {label}
        </p>
        <p className="mt-0.5 font-heading text-2xl font-semibold text-foreground">
          {value}
          {sub && <span className="ml-1 text-sm font-normal text-muted-foreground">{sub}</span>}
        </p>
      </div>
    </div>
  );
}

// ─── Progress bar ────────────────────────────────────────────────────────────
function AdherenceBar({ percent }: { percent: number }) {
  const color =
    percent >= 80 ? "bg-[var(--success)]" : percent >= 50 ? "bg-[#e0912f]" : "bg-destructive";
  return (
    <div className="mt-1 h-1.5 w-full overflow-hidden rounded-full bg-secondary">
      <div
        className={`h-full rounded-full transition-all ${color}`}
        style={{ width: `${percent}%` }}
      />
    </div>
  );
}

// ─── Patient card ────────────────────────────────────────────────────────────
function PatientCard({
  patient,
  isFetching,
  onClick,
}: {
  patient: DoctorPatientDto;
  isFetching: boolean;
  onClick: () => void;
}) {
  const level = adherenceLevelLabels[patient.adherenceLevel] ?? {
    label: patient.adherenceLevel,
    color: "secondary" as const,
  };

  return (
    <Card
      className={`w-full cursor-pointer transition-shadow hover:shadow-md ${isFetching ? "opacity-60" : ""}`}
      onClick={onClick}
    >
      <CardContent className="flex items-start gap-4 p-4">
        {/* Avatar */}
        <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-primary text-lg font-semibold text-primary-foreground">
          {patient.patientName.charAt(0).toUpperCase()}
        </div>

        {/* Main info */}
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <span className="break-words font-medium">{patient.patientName}</span>
            <Badge
              variant={
                patient.adherenceLevel === "good"
                  ? "default"
                  : patient.adherenceLevel === "warning"
                    ? "secondary"
                    : "destructive"
              }
              className="text-xs"
            >
              {level.label}
            </Badge>
            {patient.hasOverdueToday && (
              <Badge variant="destructive" className="gap-1 text-xs">
                <AlertCircle className="size-3" />
                Quá giờ
              </Badge>
            )}
          </div>

          {/* Progress bar */}
          <AdherenceBar percent={patient.todayAdherencePercent} />

          <div className="mt-1.5 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-muted-foreground">
            <span>
              Hôm nay:{" "}
              <span
                className={`font-semibold ${
                  patient.adherenceLevel === "good"
                    ? "text-[var(--success)]"
                    : patient.adherenceLevel === "warning"
                      ? "text-[#e0912f]"
                      : "text-destructive"
                }`}
              >
                {patient.todayTaken}/{patient.todayTotal}
              </span>
            </span>
            <span className="text-muted-foreground/60">·</span>
            <span>Đơn Active: {patient.activePrescriptionCount}</span>
          </div>
        </div>

        <div className="flex shrink-0 flex-col items-end gap-2">
          <span
            className={`font-heading text-xl font-semibold ${
              patient.adherenceLevel === "good"
                ? "text-[var(--success)]"
                : patient.adherenceLevel === "warning"
                  ? "text-[#e0912f]"
                  : "text-destructive"
            }`}
          >
            {patient.todayAdherencePercent}%
          </span>
          <CheckCircle2 className="size-4 text-muted-foreground/30" />
        </div>
      </CardContent>
    </Card>
  );
}

const PAGE_SIZE = 10;

// ─── Pagination controls ────────────────────────────────────────────────────
function Pagination({
  page,
  totalPages,
  totalCount,
  pageSize,
  onPageChange,
}: {
  page: number;
  totalPages: number;
  totalCount: number;
  pageSize: number;
  onPageChange: (p: number) => void;
}) {
  if (totalPages <= 1) return null;

  const start = (page - 1) * pageSize + 1;
  const end = Math.min(page * pageSize, totalCount);

  return (
    <div className="flex items-center justify-between px-1 pt-4">
      <span className="text-sm text-muted-foreground">
        Hiển thị <span className="font-medium text-foreground">{start}–{end}</span> /{" "}
        <span className="font-medium text-foreground">{totalCount}</span> bệnh nhân
      </span>
      <div className="flex items-center gap-1">
        <Button
          variant="outline"
          size="icon"
          className="h-8 w-8"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
        >
          <ChevronLeft className="size-4" />
        </Button>
        {Array.from({ length: totalPages }, (_, i) => i + 1).map((p) => (
          <Button
            key={p}
            variant={p === page ? "default" : "outline"}
            size="icon"
            className="h-8 w-8"
            onClick={() => onPageChange(p)}
          >
            {p}
          </Button>
        ))}
        <Button
          variant="outline"
          size="icon"
          className="h-8 w-8"
          disabled={page >= totalPages}
          onClick={() => onPageChange(page + 1)}
        >
          <ChevronRight className="size-4" />
        </Button>
      </div>
    </div>
  );
}

// ─── Page ────────────────────────────────────────────────────────────────────
export default function MedicationTrackingPage() {
  const router = useRouter();
  const [search, setSearch] = useState("");
  const deferredSearch = useDeferredValue(search);

  const [adherenceLevel, setAdherenceLevel] = useState<string>("");
  const [hasOverdue, setHasOverdue] = useState<string>("");
  const [page, setPage] = useState(1);

  const { data, isLoading, isFetching } = useQuery({
    queryKey: [
      "doctor-medication-tracking",
      "patients",
      deferredSearch,
      adherenceLevel || undefined,
      hasOverdue || undefined,
      page,
    ],
    queryFn: () =>
      getPatientList({
        search: deferredSearch || undefined,
        adherenceLevel: adherenceLevel || undefined,
        hasOverdueDoses:
          hasOverdue === "true" ? true : hasOverdue === "false" ? false : undefined,
        page,
        pageSize: PAGE_SIZE,
      }),
  });

  const stats = useMemo(
    () => deriveStats(data?.patients ?? []),
    [data?.patients],
  );

  const hasActiveFilter =
    search.trim().length > 0 || !!adherenceLevel || !!hasOverdue;

  function resetFilters() {
    setSearch("");
    setAdherenceLevel("");
    setHasOverdue("");
    setPage(1);
  }

  return (
    <div className="w-full px-4 py-6 sm:px-6">
      {/* Header */}
      <div className="mb-6">
        <div className="flex items-center gap-3">
          <Pill className="size-7 shrink-0 text-primary" />
          <h1 className="font-heading text-2xl font-semibold text-primary">
            Theo dõi tiến độ uống thuốc
          </h1>
        </div>
        <p className="mt-1 ml-10 text-sm text-muted-foreground">
          Chỉ hiển thị những bệnh nhân đang có ít nhất 1 đơn thuốc active
        </p>
      </div>

      {/* Summary tiles */}
      {!isLoading ? (
        <div className="mb-6 grid grid-cols-2 gap-3 lg:grid-cols-4">
          <StatTile
            icon={Users}
            label="Bệnh nhân"
            value={stats.totalPatients}
            accent="good"
          />
          <StatTile
            icon={TrendingUp}
            label="Tuân thủ TB hôm nay"
            value={stats.avgAdherencePercent}
            sub="%"
            accent={
              stats.avgAdherencePercent >= 80
                ? "good"
                : stats.avgAdherencePercent >= 50
                  ? "warning"
                  : "critical"
            }
          />
          <StatTile
            icon={Clock}
            label="Quá giờ hôm nay"
            value={stats.overdueCount}
            accent={stats.overdueCount > 0 ? "critical" : "good"}
          />
          <StatTile
            icon={FileText}
            label="Đơn đang Active"
            value={stats.activePrescriptionCount}
            accent="good"
          />
        </div>
      ) : (
        <div className="mb-6 grid grid-cols-2 gap-3 lg:grid-cols-4">
          {[1, 2, 3, 4].map((i) => (
            <Skeleton key={i} className="h-20 rounded-xl" />
          ))}
        </div>
      )}

      {/* Search + Filter bar */}
      <div className="mb-4 flex flex-wrap items-center gap-2">
        <Input
          placeholder="Tìm bệnh nhân..."
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setPage(1);
          }}
          className="w-full sm:w-56"
        />
        <Select
          value={adherenceLevel}
          onValueChange={(val) => {
            setAdherenceLevel(val);
            setPage(1);
          }}
        >
          <SelectTrigger className="w-full sm:w-44">
            <SelectValue placeholder="Mức tuân thủ" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="good">Tốt (≥80%)</SelectItem>
            <SelectItem value="warning">Trung bình (50–79%)</SelectItem>
            <SelectItem value="poor">Kém (&lt;50%)</SelectItem>
          </SelectContent>
        </Select>
        <Select
          value={hasOverdue}
          onValueChange={(val) => {
            setHasOverdue(val);
            setPage(1);
          }}
        >
          <SelectTrigger className="w-full sm:w-40">
            <SelectValue placeholder="Quá giờ" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="true">Có liều quá giờ</SelectItem>
            <SelectItem value="false">Không có quá giờ</SelectItem>
          </SelectContent>
        </Select>
        {hasActiveFilter && (
          <Button variant="ghost" size="sm" onClick={resetFilters}>
            Đặt lại
          </Button>
        )}
      </div>

      {/* Patient list */}
      {isLoading ? (
        <div className="space-y-3">
          {[1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-28 w-full rounded-xl" />
          ))}
        </div>
      ) : !data?.patients.length ? (
        <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-muted-foreground/20 py-16 text-muted-foreground">
          <Pill className="mb-2 size-10 opacity-30" />
          <p>Không có bệnh nhân nào được tìm thấy.</p>
        </div>
      ) : (
        <>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            {(data?.patients ?? []).map((patient) => (
              <PatientCard
                key={patient.patientProfileId}
                patient={patient}
                isFetching={isFetching}
                onClick={() =>
                  router.push(`/medication-tracking/${patient.patientProfileId}`)
                }
              />
            ))}
          </div>
          <Pagination
            page={data?.page ?? 1}
            totalPages={data?.totalPages ?? 1}
            totalCount={data?.totalCount ?? 0}
            pageSize={PAGE_SIZE}
            onPageChange={(p) => setPage(p)}
          />
        </>
      )}
    </div>
  );
}
