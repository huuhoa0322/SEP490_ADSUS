"use client";

import { usePrescriptionDetail } from "../hooks/use-prescriptions";
import { AdherenceProgress } from "./adherence-progress";
import { IntakeTimeline } from "./intake-timeline";

interface PrescriptionDetailProps {
  prescriptionId: string;
}

/**
 * Module 7 UC-11 — chi tiết 1 đơn thuốc:
 * - Thông tin chung (bệnh nhân, bác sĩ, ngày, trạng thái)
 * - Adherence tổng
 * - Ghi chú chung
 * - Danh sách thuốc + adherence per-item
 * - Timeline uống thuốc
 */
export function PrescriptionDetail({ prescriptionId }: PrescriptionDetailProps) {
  const { data, isLoading, isError } = usePrescriptionDetail(prescriptionId);

  if (isLoading) return <p className="text-muted-foreground">Đang tải...</p>;
  if (isError) return <p className="text-destructive">Không tải được đơn thuốc.</p>;
  if (!data) return null;

  return (
    <article className="space-y-6">
      <header>
        <h2 className="font-heading text-2xl font-bold text-[#223a66]">
          Đơn ngày {data.prescribedDate}
        </h2>
        <p className="text-sm text-muted-foreground">
          Bệnh nhân: {data.patientName} · Bác sĩ: {data.doctorName}
        </p>
        <p className="mt-1 text-sm">
          Trạng thái: <span className="font-medium text-[#1cba9f]">{data.status}</span>
        </p>
      </header>

      <AdherenceProgress
        percent={data.adherencePercent}
        level={data.adherenceLevel}
        label="Tỉ lệ tuân thủ tổng"
      />

      {data.generalNote && (
        <section className="rounded-lg border bg-card p-4">
          <h3 className="mb-1 text-sm font-semibold">Ghi chú chung</h3>
          <p className="text-sm">{data.generalNote}</p>
        </section>
      )}

      <section>
        <h3 className="mb-3 font-heading text-lg font-semibold text-[#223a66]">
          Danh sách thuốc
        </h3>
        <div className="grid gap-3 md:grid-cols-2">
          {data.items.map((item) => (
            <div key={item.prescriptionItemId} className="rounded-lg border bg-card p-4">
              <p className="font-medium">{item.medicineName}</p>
              <p className="text-sm text-muted-foreground">
                {item.dosage} · {item.durationDays} ngày
              </p>
              {item.instructions && (
                <p className="mt-1 text-xs italic">{item.instructions}</p>
              )}
              <div className="mt-3">
                <AdherenceProgress
                  percent={item.adherencePercent}
                  level={item.adherenceLevel}
                  label={`${item.takenDoses}/${item.totalDoses} liều`}
                />
              </div>
            </div>
          ))}
        </div>
      </section>

      <section>
        <h3 className="mb-3 font-heading text-lg font-semibold text-[#223a66]">
          Lịch sử uống thuốc
        </h3>
        <IntakeTimeline prescriptionId={prescriptionId} />
      </section>
    </article>
  );
}
