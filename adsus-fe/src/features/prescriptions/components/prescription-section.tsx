"use client";

import { useCasePrescriptionWithCompliance } from "../hooks/use-prescriptions";
import type { PrescriptionWithComplianceResponse } from "../types/prescriptions.types";

import { AdherencePill } from "./adherence-pill";

/** Format ngày yyyy-MM-dd → dd/MM/yyyy. */
function formatDate(value: string | null | undefined): string {
  if (!value) return "—";
  const [y, m, d] = value.slice(0, 10).split("-");
  return `${d}/${m}/${y}`;
}

/** Tính ngày kết thúc: startDate + durationDays = ngày uống liều cuối. */
function calcEndDate(startDate: string, durationDays: number): string {
  const d = new Date(`${startDate}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + durationDays);
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
  const { data: prescriptions, isLoading } = useCasePrescriptionWithCompliance(caseId);

  if (isLoading) {
    return (
      <div className="mt-5 rounded-xl border border-gray-300 dark:border-gray-700 bg-card p-6 shadow-sm">
        <p className="text-base font-bold text-foreground">Đang tải đơn thuốc...</p>
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
    <section className="rounded-xl border border-gray-300 dark:border-gray-700 bg-card p-6 shadow-sm">
      <div className="mb-3 flex flex-wrap items-baseline justify-between gap-3">
        <div className="flex flex-wrap items-baseline gap-3">
          <h2 className="font-heading text-xl font-bold text-foreground">
            Đơn thuốc
          </h2>
          <span className="text-sm font-bold text-foreground">
            Ngày kê:{" "}
            <span className="font-bold text-foreground">
              {formatDate(prescription.prescribedDate)}
            </span>
          </span>
        </div>
        <AdherencePill percent={prescription.adherencePercent} label="tuân thủ" />
      </div>

      <div className="mb-4 overflow-hidden rounded-xl border border-gray-300 dark:border-gray-700">
        <table className="w-full text-sm">
          <thead className="border-b border-border bg-teal/5 dark:bg-teal/10">
            <tr className="[&>th:first-child]:pl-6 [&>td:first-child]:pl-6">
              <th className="px-3 py-2 text-left text-xs font-bold uppercase tracking-wider text-foreground">Tên thuốc</th>
              <th className="px-3 py-2 text-left text-xs font-bold uppercase tracking-wider text-foreground">Liều dùng</th>
              <th className="px-3 py-2 text-left text-xs font-bold uppercase tracking-wider text-foreground">Khung giờ</th>
              <th className="px-3 py-2 text-left text-xs font-bold uppercase tracking-wider text-foreground">Thời gian</th>
              <th className="px-3 py-2 text-left text-xs font-bold uppercase tracking-wider text-foreground">Cách dùng</th>
              <th className="px-3 py-2 text-left text-xs font-bold uppercase tracking-wider text-foreground">Tuân thủ</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-black/20 dark:divide-white/20">
            {prescription.items.map((item) => (
              <tr key={item.prescriptionItemId} className="[&>th:first-child]:pl-6 [&>td:first-child]:pl-6">
                <td className="px-3 py-2 font-medium text-foreground">{item.medicineName}</td>
                <td className="px-3 py-2 text-foreground">{item.dosage}</td>
                <td className="px-3 py-2 text-foreground">
                  {item.scheduleSlots?.map((s) => SLOT_LABEL[s] ?? s).join(", ") ?? "—"}
                </td>
                <td className="px-3 py-2 text-foreground">
                  {formatDate(item.startDate)} → {formatDate(calcEndDate(item.startDate, item.durationDays))}
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
        <div className="rounded-xl border border-gray-300 dark:border-gray-700 bg-muted/20 p-3 text-sm">
          <span className="font-bold text-foreground">Ghi chú: </span>
          <span className="font-medium text-foreground">{prescription.generalNote}</span>
        </div>
      )}
    </section>
  );
}

