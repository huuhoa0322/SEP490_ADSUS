"use client";

import { useState } from "react";

import { usePrescriptionList } from "../hooks/use-prescriptions";
import { AdherenceProgress } from "./adherence-progress";

interface PrescriptionHistoryViewProps {
  patientProfileId: string;
}

type StatusFilter = "ALL" | "ACTIVE" | "COMPLETED";

const STATUS_LABELS: Record<StatusFilter, string> = {
  ALL: "Tất cả",
  ACTIVE: "Đang dùng",
  COMPLETED: "Đã hoàn tất",
};

/**
 * Module 7 UC-11 — danh sách đơn thuốc của bệnh nhân, filter theo status.
 * Doctor + Nurse xem được (read-only).
 */
export function PrescriptionHistoryView({ patientProfileId }: PrescriptionHistoryViewProps) {
  const [status, setStatus] = useState<StatusFilter>("ALL");

  const { data, isLoading, isError } = usePrescriptionList({
    patientProfileId,
    status,
    page: 1,
    pageSize: 20,
  });

  if (isLoading) {
    return <p className="text-muted-foreground">Đang tải lịch sử đơn thuốc...</p>;
  }
  if (isError) {
    return <p className="text-destructive">Không tải được lịch sử đơn thuốc.</p>;
  }
  if (!data || data.items.length === 0) {
    return (
      <div className="rounded-lg border border-dashed p-8 text-center">
        <p className="text-muted-foreground">Bệnh nhân chưa có đơn thuốc nào.</p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex gap-2">
        {(Object.keys(STATUS_LABELS) as StatusFilter[]).map((s) => (
          <button
            key={s}
            type="button"
            onClick={() => setStatus(s)}
            className={`rounded-full px-3 py-1 text-sm transition-colors ${
              status === s
                ? "bg-[#223a66] text-white"
                : "bg-muted text-muted-foreground hover:bg-muted/70"
            }`}
          >
            {STATUS_LABELS[s]}
          </button>
        ))}
      </div>

      <ul className="space-y-3">
        {data.items.map((p) => (
          <li
            key={p.prescriptionId}
            className="rounded-lg border bg-card p-4 shadow-sm transition hover:shadow-md"
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="font-heading text-base font-semibold text-[#223a66]">
                  Đơn ngày {p.prescribedDate}
                </p>
                <p className="text-sm text-muted-foreground">
                  Bác sĩ {p.doctorName} · {p.itemCount} thuốc ·{" "}
                  <span
                    className={`font-medium ${
                      p.status === "ACTIVE" ? "text-[#1cba9f]" : "text-muted-foreground"
                    }`}
                  >
                    {p.status === "ACTIVE" ? "Đang dùng" : "Đã hoàn tất"}
                  </span>
                </p>
              </div>
              <a
                href={`/patients/${patientProfileId}/prescriptions/${p.prescriptionId}`}
                className="text-sm font-medium text-[#4488be] hover:underline"
              >
                Xem chi tiết →
              </a>
            </div>
            <div className="mt-3">
              <AdherenceProgress
                percent={p.adherencePercent}
                level={p.adherenceLevel}
                label="Tỉ lệ tuân thủ"
              />
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}
