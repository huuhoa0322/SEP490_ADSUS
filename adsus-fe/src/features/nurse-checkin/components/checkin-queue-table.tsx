"use client";

import { format } from "date-fns";
import { Check, Loader2 } from "lucide-react";
import type { CheckinQueueItem } from "../types/checkin.types";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

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
    <div className="rounded-lg border border-border overflow-hidden">
      <table className="w-full">
        <thead className="bg-muted/50">
          <tr>
            {/* Số thứ tự = vị trí trong hàng đợi (đã sắp theo giờ hẹn), giống số phiếu lấy
                số ở quầy tiếp đón — không phải trường dữ liệu mới, chỉ là số thứ tự hiển thị. */}
            <th className="w-14 px-4 py-3 text-left text-sm font-medium text-muted-foreground">STT</th>
            <th className="px-4 py-3 text-left text-sm font-medium text-muted-foreground">Giờ</th>
            <th className="px-4 py-3 text-left text-sm font-medium text-muted-foreground">Bệnh nhân</th>
            <th className="px-4 py-3 text-left text-sm font-medium text-muted-foreground">Bác sĩ</th>
            <th className="px-4 py-3 text-left text-sm font-medium text-muted-foreground">Lý do khám</th>
            <th className="px-4 py-3 text-right text-sm font-medium text-muted-foreground">Thao tác</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {queue.map((item, index) => {
            const isCheckedIn = item.status === "Approved";
            const slotTime = new Date(item.slotTime);

            return (
              <tr key={item.appointmentId} className="hover:bg-muted/30">
                <td className="px-4 py-3">
                  <span
                    className={cn(
                      "flex size-7 items-center justify-center rounded-full text-xs font-700 tabular-nums",
                      isCheckedIn ? "bg-[var(--success)]/12 text-[var(--success)]" : "bg-muted text-muted-foreground",
                    )}
                  >
                    {index + 1}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <span className="font-mono text-sm font-medium text-foreground">
                    {format(slotTime, "HH:mm")}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <div className="font-medium text-foreground">{item.patientFullName}</div>
                  {item.patientPhone && (
                    <div className="text-sm text-muted-foreground">
                      {item.patientPhone}
                    </div>
                  )}
                </td>
                <td className="px-4 py-3 text-sm text-foreground">{item.doctorName}</td>
                <td className="px-4 py-3 text-sm text-muted-foreground">
                  {item.reason || "—"}
                </td>
                <td className="px-4 py-3 text-right">
                  {isCheckedIn ? (
                    <span className="inline-flex items-center gap-1 rounded-full bg-[var(--success)]/12 px-3 py-1 text-sm font-medium text-[var(--success)]">
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
