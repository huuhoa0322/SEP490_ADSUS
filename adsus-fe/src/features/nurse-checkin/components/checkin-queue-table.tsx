"use client";

import { format } from "date-fns";
import { Check, Eye, Loader2, XCircle } from "lucide-react";
import type { CheckinQueueItem } from "../types/checkin.types";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

interface CheckinQueueTableProps {
  queue: CheckinQueueItem[];
  isLoading: boolean;
  onCheckin: (item: CheckinQueueItem) => void;
  checkingInId: string | null;
  page?: number;
  pageSize?: number;
  onDetail?: (item: CheckinQueueItem) => void;
}

export function CheckinQueueTable({
  queue,
  isLoading,
  onCheckin,
  checkingInId,
  page = 1,
  pageSize = 15,
  onDetail,
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
        <p className="text-muted-foreground">Không tìm thấy lịch hẹn phù hợp.</p>
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
            <th className="w-[260px] px-4 py-3 text-right text-sm font-medium text-muted-foreground">Thao tác</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {queue.map((item, index) => {
            const normStatus = (item.status || "").toUpperCase();
            const isApproved = normStatus === "APPROVED";
            const isBooked = normStatus === "BOOKED";
            const isCompleted = normStatus === "COMPLETED";
            const isCheckedIn = isApproved || isCompleted;
            const isCancelled =
              normStatus === "CANCELLED" ||
              normStatus === "NOSHOW" ||
              normStatus === "NO_SHOW";
            const slotTime = new Date(item.slotTime);
            const sttNumber = (page - 1) * pageSize + index + 1;

            return (
              <tr key={item.appointmentId} className="hover:bg-muted/30">
                <td className="px-4 py-3">
                  <span
                    className={cn(
                      "flex size-7 items-center justify-center rounded-full text-xs font-700 tabular-nums",
                      isCheckedIn ? "bg-[var(--success)]/12 text-[var(--success)]" : "bg-muted text-muted-foreground",
                    )}
                  >
                    {sttNumber}
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
                <td className="w-[260px] px-4 py-3 text-right">
                  <div className="flex items-center justify-end gap-2.5">
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      onClick={() => onDetail?.(item)}
                      className="gap-1.5 shrink-0"
                    >
                      <Eye className="h-3.5 w-3.5" />
                      Chi tiết
                    </Button>
                    <div className="w-[135px] shrink-0 flex items-center justify-end">
                      {isCheckedIn && (
                        <span className="inline-flex w-full items-center justify-center gap-1.5 rounded-lg border border-emerald-600/30 bg-emerald-50 px-2.5 py-1 text-xs font-semibold text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400">
                          <Check className="h-3.5 w-3.5" />
                          Đã check-in
                        </span>
                      )}
                      {isBooked && (
                        <Button
                          size="sm"
                          className="w-full bg-[#2E37A4] hover:bg-[#232b85] text-white font-medium shadow-xs"
                          onClick={() => onCheckin(item)}
                          disabled={checkingInId !== null}
                        >
                          {checkingInId === item.appointmentId ? (
                            <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
                          ) : null}
                          Check-in
                        </Button>
                      )}
                      {isCancelled && (
                        <span className="inline-flex w-full items-center justify-center gap-1.5 rounded-lg border border-rose-600/30 bg-rose-50 px-2.5 py-1 text-xs font-semibold text-rose-700 dark:bg-rose-950/40 dark:text-rose-400">
                          <XCircle className="h-3.5 w-3.5" />
                          Đã huỷ
                        </span>
                      )}
                      {!isCheckedIn && !isBooked && !isCancelled && (
                        <span className="inline-flex w-full items-center justify-center gap-1 rounded-lg bg-muted px-2.5 py-1 text-xs font-medium text-muted-foreground">
                          {item.status}
                        </span>
                      )}
                    </div>
                  </div>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
