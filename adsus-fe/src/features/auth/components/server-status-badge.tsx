"use client";

import { AlertCircle, CheckCircle2, Loader2 } from "lucide-react";
import { useEffect, useState } from "react";

import { useBackendHealth } from "../hooks/use-backend-health";

// Ping /api/health chỉ mất chưa tới 1-2s khi server đã tỉnh sẵn (trường hợp bình thường,
// nhờ keep-alive) — chỉ đổi sang câu giải thích "đang đánh thức" sau ngưỡng này, để không
// làm người dùng hoang mang mỗi lần vào trang dù server vốn đang tỉnh.
const SLOW_THRESHOLD_MS = 3000;

// Giữ dòng "Đã kết nối" hiện đủ lâu để đọc được, rồi tự ẩn — không nên ở lại vĩnh viễn,
// lấn vào góc màn hình trong suốt phiên làm việc.
const SUCCESS_DISPLAY_MS = 2500;

/** Góc phải màn hình đăng nhập — báo trạng thái Backend trong lúc health-check chạy nền. */
export function ServerStatusBadge() {
  const { isPending, isError, isSuccess } = useBackendHealth();
  const [isSlow, setIsSlow] = useState(false);
  const [successDismissed, setSuccessDismissed] = useState(false);

  // Không reset isSlow về false khi hết pending — không cần thiết, vì nhánh isSuccess/isError
  // được ưu tiên render trước isSlow bên dưới một khi !isPending, nên giá trị cũ không bao
  // giờ lộ ra. Tránh gọi setState đồng bộ trong effect (bị react-hooks/set-state-in-effect
  // chặn) mà không đổi hành vi thật.
  useEffect(() => {
    if (!isPending) return;
    const timer = setTimeout(() => setIsSlow(true), SLOW_THRESHOLD_MS);
    return () => clearTimeout(timer);
  }, [isPending]);

  // Tương tự — chỉ đặt hẹn giờ trong effect, không gọi setState ngay khi effect chạy.
  // showSuccess được suy ra ngay dưới đây từ isSuccess + successDismissed, không cần thêm
  // state riêng để "bật" nó lên.
  useEffect(() => {
    if (!isSuccess) return;
    const timer = setTimeout(() => setSuccessDismissed(true), SUCCESS_DISPLAY_MS);
    return () => clearTimeout(timer);
  }, [isSuccess]);

  const showSuccess = isSuccess && !successDismissed;

  if (!isPending && !isError && !showSuccess) return null;

  return (
    <div
      role="status"
      aria-live="polite"
      className="fixed right-4 top-4 z-50 flex items-center gap-2 rounded-full border border-border bg-background/95 px-3.5 py-2 text-xs font-500 text-muted-foreground shadow-lg backdrop-blur"
    >
      {isError ? (
        <>
          <AlertCircle aria-hidden className="size-3.5 text-destructive" />
          <span className="text-destructive">Không kết nối được máy chủ</span>
        </>
      ) : showSuccess ? (
        <>
          <CheckCircle2 aria-hidden className="size-3.5 text-accent" />
          <span>Đã kết nối đến máy chủ</span>
        </>
      ) : isSlow ? (
        <>
          <Loader2 aria-hidden className="size-3.5 animate-spin text-accent" />
          <span>Đang kết nối đến máy chủ, vui lòng đợi trong giây lát...</span>
        </>
      ) : (
        <>
          <Loader2 aria-hidden className="size-3.5 animate-spin" />
          <span>Đang kiểm tra máy chủ...</span>
        </>
      )}
    </div>
  );
}
