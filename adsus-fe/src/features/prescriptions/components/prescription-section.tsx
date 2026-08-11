"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { getCasePrescription } from "../api/prescriptions.api";
import type { PrescriptionResponse } from "../types/prescriptions.types";

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

function PrescriptionDetailModal({
  prescription,
  onClose,
}: {
  prescription: PrescriptionResponse;
  onClose: () => void;
}) {
  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50"
      onClick={onClose}
    >
      <div
        className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-2xl bg-white p-6 shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-4 flex items-center justify-between">
          <h2 className="font-exo text-lg font-semibold text-navy">Chi tiết đơn thuốc</h2>
          <button
            onClick={onClose}
            className="rounded-full px-3 py-1 text-sm text-muted-foreground hover:bg-gray-100"
          >
            ✕ Đóng
          </button>
        </div>

        <div className="mb-4 rounded-xl border border-border p-4 text-sm">
          <p>
            <strong className="text-navy">Ngày kê:</strong>{" "}
            {formatDate(prescription.prescribedDate)}
          </p>
          {prescription.generalNote && (
            <p className="mt-1">
              <strong className="text-navy">Ghi chú:</strong> {prescription.generalNote}
            </p>
          )}
        </div>

        <div className="overflow-hidden rounded-xl border border-border">
          <table className="w-full text-sm">
            <thead className="bg-teal/5">
              <tr>
                <th className="px-3 py-2 text-left font-semibold text-navy">Tên thuốc</th>
                <th className="px-3 py-2 text-left font-semibold text-navy">Liều dùng</th>
                <th className="px-3 py-2 text-left font-semibold text-navy">Khung giờ</th>
                <th className="px-3 py-2 text-center font-semibold text-navy">Thời gian</th>
                <th className="px-3 py-2 text-left font-semibold text-navy">Cách dùng</th>
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
                  <td className="px-3 py-2 text-center text-foreground">
                    {formatDate(item.startDate)} → {formatDate(calcEndDate(item.startDate, item.durationDays))}
                    <br />
                    <span className="text-xs text-muted-foreground">{item.durationDays} ngày</span>
                  </td>
                  <td className="px-3 py-2 text-foreground">{item.instructions || "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

interface PrescriptionSectionProps {
  caseId: string;
}

export function PrescriptionSection({ caseId }: PrescriptionSectionProps) {
  const [showModal, setShowModal] = useState(false);

  const { data: prescription, isLoading } = useQuery({
    queryKey: ["case-prescription", caseId],
    queryFn: () => getCasePrescription(caseId),
    staleTime: 30 * 1000,
  });

  if (isLoading) {
    return (
      <div className="mt-5 rounded-xl border border-border p-6">
        <p className="text-sm text-muted-foreground">Đang tải đơn thuốc...</p>
      </div>
    );
  }

  if (!prescription) {
    return null;
  }

  return (
    <>
      {showModal && (
        <PrescriptionDetailModal
          prescription={prescription}
          onClose={() => setShowModal(false)}
        />
      )}

      <section className="mt-5 rounded-xl border border-border p-6">
        <h2 className="mb-3 font-heading text-lg font-semibold text-foreground">
          Đơn thuốc
        </h2>

        <div className="mb-4 overflow-hidden rounded-xl border border-border">
          <table className="w-full text-sm">
            <thead className="bg-teal/5">
              <tr>
                <th className="px-3 py-2 text-left font-semibold text-navy">Tên thuốc</th>
                <th className="px-3 py-2 text-left font-semibold text-navy">Liều dùng</th>
                <th className="px-3 py-2 text-left font-semibold text-navy">Khung giờ</th>
                <th className="px-3 py-2 text-left font-semibold text-navy">Thời gian</th>
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
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="flex justify-end">
          <button
            type="button"
            onClick={() => setShowModal(true)}
            className="rounded-lg border border-border px-4 py-2 text-sm font-medium hover:bg-accent"
          >
            Xem chi tiết đầy đủ
          </button>
        </div>
      </section>
    </>
  );
}
