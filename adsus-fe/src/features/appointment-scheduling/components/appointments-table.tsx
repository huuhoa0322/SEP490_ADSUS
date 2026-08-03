"use client";

import type { AppointmentSummary } from "../types/schedule-slot.types";

interface Props {
  data: AppointmentSummary[];
  loading: boolean;
}

/**
 * Module 8 UC-15 AF-02 — bảng bệnh nhân đã book slot.
 */
export function AppointmentsTable({ data, loading }: Props) {
  if (loading) return <p className="text-muted-foreground">Đang tải...</p>;
  if (data.length === 0)
    return (
      <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
        Chưa có bệnh nhân nào đặt lịch.
      </div>
    );

  return (
    <table className="w-full border-collapse text-sm">
      <thead>
        <tr className="border-b bg-muted text-left">
          <th className="px-3 py-2 font-medium">Bệnh nhân</th>
          <th className="px-3 py-2 font-medium">Trạng thái</th>
          <th className="px-3 py-2 font-medium">Lý do</th>
        </tr>
      </thead>
      <tbody>
        {data.map((a) => (
          <tr key={a.appointmentId} className="border-b">
            <td className="px-3 py-2">{a.patientName}</td>
            <td className="px-3 py-2">
              <span
                className={`rounded-full px-2 py-0.5 text-xs ${
                  a.status === "BOOKED"
                    ? "bg-[#1cba9f]/10 text-[#1cba9f]"
                    : "bg-red-500/10 text-red-600"
                }`}
              >
                {a.status === "BOOKED" ? "Đã đặt" : "Đã hủy"}
              </span>
            </td>
            <td className="px-3 py-2 text-muted-foreground">
              {a.reason ?? a.cancelledReason ?? "—"}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}