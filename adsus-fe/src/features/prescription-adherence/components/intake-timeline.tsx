"use client";

import { useIntakeLogs } from "../hooks/use-prescriptions";

interface IntakeTimelineProps {
  prescriptionId: string;
}

/**
 * Module 7 UC-11 — timeline liều thuốc 1 đơn.
 * Status dùng convention DB: "PENDING" / "TAKEN" derive từ ConfirmedAt,
 * KHÔNG có cột status trong table.
 */
export function IntakeTimeline({ prescriptionId }: IntakeTimelineProps) {
  const { data, isLoading, isError } = useIntakeLogs(prescriptionId);

  if (isLoading) return <p className="text-muted-foreground">Đang tải...</p>;
  if (isError) return <p className="text-destructive">Không tải được lịch sử uống thuốc.</p>;
  if (!data || data.items.length === 0)
    return <p className="text-muted-foreground">Chưa có liều thuốc nào được ghi nhận.</p>;

  return (
    <ol className="space-y-2 border-l-2 border-muted pl-4">
      {data.items.map((log) => (
        <li key={log.intakeId} className="flex items-start justify-between gap-4">
          <div>
            <p className="text-sm font-medium">{log.medicineName}</p>
            <p className="text-xs text-muted-foreground">
              Hẹn: {new Date(log.scheduledTime).toLocaleString("vi-VN")}
            </p>
            {log.confirmedAt && (
              <p className="text-xs text-[#1cba9f]">
                Đã uống lúc {new Date(log.confirmedAt).toLocaleString("vi-VN")}
              </p>
            )}
          </div>
          <span
            className={`shrink-0 rounded-full px-2 py-0.5 text-xs ${
              log.status === "TAKEN"
                ? "bg-[#1cba9f]/10 text-[#1cba9f]"
                : "bg-amber-500/10 text-amber-600"
            }`}
          >
            {log.status === "TAKEN" ? "Đã uống" : "Đang chờ"}
          </span>
        </li>
      ))}
    </ol>
  );
}
