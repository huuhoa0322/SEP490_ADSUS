"use client";

import { useState, useDeferredValue } from "react";
import { useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { Pill, AlertCircle, CheckCircle2 } from "lucide-react";
import { Badge } from "@/components/ui/badge";
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

export default function MedicationTrackingPage() {
  const router = useRouter();
  const [search, setSearch] = useState("");
  const deferredSearch = useDeferredValue(search);

  const [adherenceLevel, setAdherenceLevel] = useState<string>("");
  const [hasOverdue, setHasOverdue] = useState<string>("");

  const { data, isLoading, isFetching } = useQuery({
    queryKey: [
      "doctor-medication-tracking",
      "patients",
      deferredSearch,
      adherenceLevel || undefined,
      hasOverdue || undefined,
    ],
    queryFn: () =>
      getPatientList({
        search: deferredSearch || undefined,
        adherenceLevel: adherenceLevel || undefined,
        hasOverdueDoses: hasOverdue === "true" ? true : hasOverdue === "false" ? false : undefined,
      }),
  });

  return (
    <div className="mx-auto w-4/5 py-8">
      <div className="mb-6 flex items-center gap-3">
        <Pill className="size-7 text-primary" />
        <h1 className="font-heading text-2xl font-semibold text-primary">
          Theo dõi thuốc
        </h1>
      </div>

      {/* Search + Filter bar */}
      <div className="mb-6 flex flex-wrap items-center gap-3">
        <Input
          placeholder="Tìm bệnh nhân..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="max-w-xs"
        />
        <Select value={adherenceLevel} onValueChange={setAdherenceLevel}>
          <SelectTrigger className="w-44">
            <SelectValue placeholder="Mức tuân thủ" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="good">Tốt (≥80%)</SelectItem>
            <SelectItem value="warning">Trung bình (50–79%)</SelectItem>
            <SelectItem value="poor">Kém (&lt;50%)</SelectItem>
          </SelectContent>
        </Select>
        <Select value={hasOverdue} onValueChange={setHasOverdue}>
          <SelectTrigger className="w-44">
            <SelectValue placeholder="Trạng thái" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="true">Có liều quá giờ</SelectItem>
            <SelectItem value="false">Không có quá giờ</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {/* Patient list */}
      {isLoading ? (
        <div className="space-y-3">
          {[1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-24 w-full rounded-xl" />
          ))}
        </div>
      ) : !data?.patients.length ? (
        <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-muted-foreground/20 py-16 text-muted-foreground">
          <Pill className="mb-2 size-10 opacity-30" />
          <p>Không có bệnh nhân nào được tìm thấy.</p>
        </div>
      ) : (
        <div className="space-y-3">
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
      )}
    </div>
  );
}

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
  const pct = patient.todayAdherencePercent;
  const pctColor =
    patient.adherenceLevel === "good"
      ? "text-green-600"
      : patient.adherenceLevel === "warning"
        ? "text-amber-600"
        : "text-red-600";

  return (
    <Card
      className={`cursor-pointer transition-shadow hover:shadow-md ${isFetching ? "opacity-60" : ""}`}
      onClick={onClick}
    >
      <CardContent className="flex items-center gap-4 p-4">
        {/* Avatar placeholder */}
        <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-primary/10 text-lg font-semibold text-primary">
          {patient.patientName.charAt(0).toUpperCase()}
        </div>

        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <span className="font-medium">{patient.patientName}</span>
            <Badge
              variant={
                patient.adherenceLevel === "good"
                  ? "default"
                  : patient.adherenceLevel === "warning"
                    ? "secondary"
                    : "destructive"
              }
            >
              {level.label}
            </Badge>
            {patient.hasOverdueToday && (
              <Badge variant="destructive" className="gap-1">
                <AlertCircle className="size-3" />
                Có liều quá giờ
              </Badge>
            )}
          </div>

          <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-muted-foreground">
            <span>
              Hôm nay:{" "}
              <span className={`font-medium ${pctColor}`}>
                {patient.todayTaken}/{patient.todayTotal} ({pct}%)
              </span>
            </span>
            <span>{patient.activePrescriptionCount} đơn Active</span>
          </div>
        </div>

        <CheckCircle2 className="size-5 shrink-0 text-muted-foreground/30" />
      </CardContent>
    </Card>
  );
}
