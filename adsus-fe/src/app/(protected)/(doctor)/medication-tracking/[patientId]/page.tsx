"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useParams, useRouter } from "next/navigation";
import { ArrowLeft, Pill, Bell, BellRing } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import {
  getPatientPrescriptions,
  sendReminders,
  type TodayDoseDto,
  type PrescriptionCardDto,
} from "@/features/medication-tracking/api/medication-tracking.api";
import toast from "react-hot-toast";

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
        pillClass: "bg-green-100 text-green-700 border-green-200",
      };
    case "OVERTIME":
      return {
        label: "Quá giờ",
        pillClass: "bg-red-100 text-red-700 border-red-200",
      };
    case "PENDING":
      return {
        label: "Chưa đến",
        pillClass: "bg-amber-100 text-amber-700 border-amber-200",
      };
  }
}

function AdherenceBar({
  taken,
  total,
  percent,
  label,
}: {
  taken: number;
  total: number;
  percent: number;
  label: string;
}) {
  const colorClass =
    percent >= 80
      ? "bg-green-500"
      : percent >= 50
        ? "bg-amber-500"
        : "bg-red-500";
  const textClass =
    percent >= 80
      ? "text-green-700"
      : percent >= 50
        ? "text-amber-700"
        : "text-red-600";

  return (
    <div className="mt-2 space-y-1">
      <div className="flex items-center justify-between text-xs">
        <span className="text-muted-foreground">{label}</span>
        <span className={`font-semibold ${textClass}`}>
          {taken}/{total} ({percent}%)
        </span>
      </div>
      <div className="h-1.5 w-full overflow-hidden rounded-full bg-muted">
        <div
          className={`h-full rounded-full transition-all ${colorClass}`}
          style={{ width: `${Math.min(percent, 100)}%` }}
        />
      </div>
    </div>
  );
}

function PrescriptionCard({ prescription }: { prescription: PrescriptionCardDto }) {
  const queryClient = useQueryClient();
  const params = useParams();
  const patientId = params.patientId as string;

  const pendingOrOverdueCount = prescription.todayDoses.filter(
    (d) => d.status === "PENDING" || d.status === "OVERTIME",
  ).length;

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

  return (
    <Card className="overflow-hidden">
      <CardHeader className="bg-muted/40 pb-3">
        <div className="flex items-center justify-between gap-3">
          <div className="flex min-w-0 items-center gap-2">
            <Pill className="size-5 shrink-0 text-primary" />
            <span className="min-w-0 truncate text-base font-medium leading-snug">
              {prescription.caseName}
            </span>
          </div>
          <Button
            size="sm"
            variant="outline"
            className="shrink-0 gap-1.5 border-amber-200 bg-amber-50 text-amber-700 hover:bg-amber-100"
            disabled={pendingOrOverdueCount === 0 || mutation.isPending}
            onClick={() => mutation.mutate()}
          >
            {mutation.isPending ? (
              <BellRing className="size-4 animate-pulse" />
            ) : (
              <Bell className="size-4" />
            )}
            Nhắc{pendingOrOverdueCount > 0 ? ` (${pendingOrOverdueCount})` : ""}
          </Button>
        </div>
      </CardHeader>

      <CardContent className="divide-y pt-0">
        {prescription.todayDoses.map((dose) => {
          const cfg = doseStatusConfig(dose.status);
          return (
            <div
              key={dose.intakeId}
              className="flex items-center gap-3 py-3 first:pt-2 last:pb-1"
            >
              <div className="flex w-14 shrink-0 flex-col items-center">
                <span className="font-mono text-sm font-semibold">
                  {dose.scheduledTime}
                </span>
              </div>

              <div className="flex flex-1 items-center gap-2">
                <span className="text-sm font-medium">{dose.medicineName}</span>
                <span
                  className={`inline-flex items-center gap-1 rounded border px-1.5 py-0.5 text-xs font-medium ${cfg.pillClass}`}
                >
                  {cfg.label}
                </span>
              </div>
            </div>
          );
        })}

        <div className="pt-3 pb-1">
          <AdherenceBar
            taken={prescription.adherenceToday.taken}
            total={prescription.adherenceToday.total}
            percent={prescription.adherenceToday.percent}
            label="Hôm nay"
          />
          <AdherenceBar
            taken={prescription.adherenceOverall.taken}
            total={prescription.adherenceOverall.total}
            percent={prescription.adherenceOverall.percent}
            label="Toàn đơn"
          />
        </div>
      </CardContent>
    </Card>
  );
}

function extractCaseDate(caseName: string): string {
  const match = caseName.match(/(\d{2}\/\d{2}\/\d{4})$/);
  if (!match) return caseName;
  const [day, month, year] = match[1].split("/");
  return `${year}-${month}-${day}`;
}

function formatDateHeader(dateKey: string): string {
  const [year, month, day] = dateKey.split("-");
  return `Ngày ${day}/${month}/${year}`;
}

export default function PatientPrescriptionDetailPage() {
  const params = useParams();
  const router = useRouter();
  const patientId = params.patientId as string;

  const { data, isLoading, isFetching, isError } = useQuery({
    queryKey: ["doctor-medication-tracking", "prescriptions", patientId],
    queryFn: () => getPatientPrescriptions(patientId),
    enabled: !!patientId,
  });

  if (isError) {
    return (
      <div className="flex flex-col items-center justify-center gap-4 py-20">
        <p className="text-muted-foreground">Không tải được thông tin đơn thuốc.</p>
        <Button variant="outline" onClick={() => router.back()}>
          <ArrowLeft className="size-4" />
          Quay lại
        </Button>
      </div>
    );
  }

  // Group prescriptions by case date
  const grouped = (data?.prescriptions ?? []).reduce<
    Record<string, PrescriptionCardDto[]>
  >((acc, p) => {
    const key = extractCaseDate(p.caseName);
    if (!acc[key]) acc[key] = [];
    acc[key].push(p);
    return acc;
  }, {});

  const sortedDates = Object.keys(grouped).sort(
    (a, b) => new Date(b).getTime() - new Date(a).getTime(),
  );

  return (
    <div className="mx-auto max-w-3xl px-4 py-8 sm:px-6">
      <div className="mb-6 flex items-center gap-3">
        <Button
          variant="ghost"
          size="icon"
          className="shrink-0"
          onClick={() => router.back()}
        >
          <ArrowLeft className="size-5" />
        </Button>
        <h1 className="font-heading text-2xl font-semibold text-primary">
          {isLoading ? (
            <SkeletonBox className="h-8 w-48" />
          ) : (
            data?.patientName ?? "Bệnh nhân"
          )}
        </h1>
      </div>

      {isLoading ? (
        <div className="space-y-4">
          {[1, 2].map((i) => (
            <SkeletonBox key={i} className="h-48 w-full rounded-xl" />
          ))}
        </div>
      ) : !data?.prescriptions.length ? (
        <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-muted-foreground/20 py-16 text-muted-foreground">
          <Pill className="mb-2 size-10 opacity-30" />
          <p>Không có đơn thuốc Active nào.</p>
        </div>
      ) : (
        <div className="space-y-6">
          {sortedDates.map((dateKey) => (
            <section key={dateKey}>
              <h2 className="mb-3 text-sm font-semibold text-muted-foreground">
                {formatDateHeader(dateKey)}
              </h2>
              <div className="space-y-4">
                {grouped[dateKey].map((prescription) => (
                  <PrescriptionCard
                    key={prescription.prescriptionId}
                    prescription={prescription}
                  />
                ))}
              </div>
            </section>
          ))}
        </div>
      )}
    </div>
  );
}
