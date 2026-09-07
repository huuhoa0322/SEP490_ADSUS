import { BellOff } from "lucide-react";

export function NotificationEmpty() {
  return (
    <div className="flex h-48 flex-col items-center justify-center p-4 text-center">
      <BellOff className="mb-3 size-12 text-muted-foreground/40" />
      <h4 className="font-medium text-muted-foreground">Không có thông báo nào</h4>
      <p className="mt-1 text-sm text-muted-foreground/70">
        Bạn sẽ nhận thông báo về lịch hẹn, đơn thuốc và cập nhật tại đây
      </p>
    </div>
  );
}
