"use client";

import { format } from "date-fns";
import { Check, Loader2 } from "lucide-react";
import type { CheckinQueueItem } from "../types/checkin.types";
import { Button } from "@/components/ui/button";

interface CheckinQueueTableProps {
  queue: CheckinQueueItem[];
  isLoading: boolean;
  onCheckin: (item: CheckinQueueItem) => void;
  checkingInId: string | null;
}

export function CheckinQueueTable({
  queue,
  isLoading,
  onCheckin,
  checkingInId,
}: CheckinQueueTableProps) {
  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (queue.length === 0) {
    return (
      <div className="flex h-64 items-center justify-center rounded-lg border border-dashed">
        <p className="text-muted-foreground">Không có lịch hẹn nào hôm nay.</p>
      </div>
    );
  }

  return (
    <div className="rounded-lg border">
      <table className="w-full">
        <thead className="bg-muted/50">
          <tr>
            <th className="px-4 py-3 text-left text-sm font-medium">Giờ</th>
            <th className="px-4 py-3 text-left text-sm font-medium">Bệnh nhân</th>
            <th className="px-4 py-3 text-left text-sm font-medium">Bác sĩ</th>
            <th className="px-4 py-3 text-left text-sm font-medium">Lý do khám</th>
            <th className="px-4 py-3 text-right text-sm font-medium">Thao tác</th>
          </tr>
        </thead>
        <tbody className="divide-y">
          {queue.map((item) => {
            const isCheckedIn = item.status === "Approved";
            const slotTime = new Date(item.slotTime);

            return (
              <tr key={item.appointmentId} className="hover:bg-muted/30">
                <td className="px-4 py-3">
                  <span className="font-mono text-sm font-medium">
                    {format(slotTime, "HH:mm")}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <div className="font-medium">{item.patientFullName}</div>
                  {item.patientPhone && (
                    <div className="text-sm text-muted-foreground">
                      {item.patientPhone}
                    </div>
                  )}
                </td>
                <td className="px-4 py-3 text-sm">{item.doctorName}</td>
                <td className="px-4 py-3 text-sm text-muted-foreground">
                  {item.reason || "—"}
                </td>
                <td className="px-4 py-3 text-right">
                  {isCheckedIn ? (
                    <span className="inline-flex items-center gap-1 rounded-full bg-green-100 px-3 py-1 text-sm font-medium text-green-700">
                      <Check className="h-4 w-4" />
                      Đã check-in
                    </span>
                  ) : (
                    <Button
                      size="sm"
                      onClick={() => onCheckin(item)}
                      disabled={checkingInId !== null}
                    >
                      {checkingInId === item.appointmentId ? (
                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      ) : null}
                      Check-in
                    </Button>
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
