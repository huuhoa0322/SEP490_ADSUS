"use client";

import { useQuery } from "@tanstack/react-query";
import { getCasePrescriptionWithCompliance } from "../api/prescriptions.api";
import type { PrescriptionWithComplianceResponse } from "../types/prescriptions.types";

import { AdherencePill } from "./adherence-pill";

/** Format ngày yyyy-MM-dd → dd/MM/yyyy. */
function formatDate(value: string | null | undefined): string {
  if (!value) return "—";
  const [y, m, d] = value.slice(0, 10).split("-");
  return `${d}/${m}/${y}`;
}

/** Tính ngày kết thúc đơn thuốc: startDate + durationDays - 1. */
function calcEndDate(startDate: string, durationDays: number): string {
  const d = new Date(`${startDate}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + durationDays - 1);
  return `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, "0")}-${String(d.getUTCDate()).padStart(2, "0")}`;
}

const SLOT_LABEL: Record<string, string> = {
  Morning: "Sáng",
  Noon: "Trưa",
  Evening: "Tối",
};

interface PrescriptionSectionProps {
  caseId: string;
}

export function PrescriptionSection({ caseId }: PrescriptionSectionProps) {
  const { data: prescriptions, isLoading } = useQuery({
    queryKey: ["case-prescription-with-compliance", caseId],
    queryFn: () => getCasePrescriptionWithCompliance(caseId),
    staleTime: 30 * 1000,
  });

  if (isLoading) {
    return (
      <div className="mt-5 rounded-xl border border-border p-6">
        <p className="text-sm text-muted-foreground">Đang tải đơn thuốc...</p>
      </div>
    );
  }

  if (!prescriptions || prescriptions.length === 0) {
    return null;
  }

  return (
    <div className="mt-5 space-y-4">
      {prescriptions.map((prescription) => (
        <PrescriptionTable key={prescription.prescriptionId} prescription={prescription} />
      ))}
    </div>
  );
}

function PrescriptionTable({
  prescription,
}: {
  prescription: PrescriptionWithComplianceResponse;
}) {
  return (
    <section className="rounded-xl border border-border p-6">
      <div className="mb-3 flex flex-wrap items-baseline justify-between gap-3">
        <div className="flex flex-wrap items-baseline gap-3">
          <h2 className="font-heading text-lg font-semibold text-foreground">
            Đơn thuốc
          </h2>
          <span className="text-sm text-muted-foreground">
            Ngày kê:{" "}
            <span className="font-medium text-foreground">
              {formatDate(prescription.prescribedDate)}
            </span>
          </span>
        </div>
        <AdherencePill percent={prescription.adherencePercent} label="tuân thủ" />
      </div>

      <div className="mb-4 overflow-hidden rounded-xl border border-border">
        <table className="w-full text-sm">
          <thead className="bg-teal/5">
            <tr>
              <th className="px-3 py-2 text-left font-semibold text-primary">Tên thuốc</th>
              <th className="px-3 py-2 text-left font-semibold text-primary">Liều dùng</th>
              <th className="px-3 py-2 text-left font-semibold text-primary">Khung giờ</th>
              <th className="px-3 py-2 text-left font-semibold text-primary">Thời gian</th>
              <th className="px-3 py-2 text-left font-semibold text-primary">Cách dùng</th>
              <th className="px-3 py-2 text-left font-semibold text-primary">Tuân thủ</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {prescription.items.map((item) => (
              <tr key={item.prescriptionItemId}>
                <td className="px-3 py-2 font-medium text-foreground">{item.medicineName}</td>
                <td className="px-3 py-2 text-foreground">{item.dosage}</td>
                <td className="px-3 py-2 text-foreground">
                  {item.scheduleSlots?.map((s) => SLOT_LABEL[s] ?? s).join(", ") ?? "—"}
                </td>
                <td className="px-3 py-2 text-foreground">
                  {formatDate(item.startDate)} → {formatDate(calcEndDate(item.startDate, item.durationDays))}{" "}
                  <span className="text-xs text-muted-foreground">({item.durationDays} ngày)</span>
                </td>
                <td className="px-3 py-2 text-foreground">{item.instructions || "—"}</td>
                <td className="px-3 py-2">
                  <AdherencePill percent={item.adherencePercent} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {prescription.generalNote && (
        <div className="rounded-xl border border-border bg-surface p-3 text-sm">
          <span className="font-semibold text-primary">Ghi chú: </span>
          <span className="text-foreground">{prescription.generalNote}</span>
        </div>
      )}
    </section>
  );
}

