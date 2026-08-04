"use client";

import { AlertTriangle, Loader2, AlertCircle } from "lucide-react";

import { getApiErrorMessage } from "@/lib/api-client";

interface ConfirmDialogProps {
  open: boolean;
  title: string;
  message: string;
  confirmLabel: string;
  /** true thì nút xác nhận mang màu cảnh báo — dùng cho hành động không hoàn tác được. */
  destructive?: boolean;
  isPending?: boolean;
  error?: unknown;
  onConfirm: () => void;
  onCancel: () => void;
}

/**
 * Hộp thoại xác nhận cho những thao tác cần chặn tay lỡ bấm.
 *
 * UC-04 AF-02 yêu cầu rõ: trước khi vô hiệu hoá phải cảnh báo đây là hành động một chiều.
 * Tự viết thay vì kéo thêm thư viện, vì cả dự án mới cần đúng một hộp thoại.
 */
export function ConfirmDialog({
  open,
  title,
  message,
  confirmLabel,
  destructive = false,
  isPending = false,
  error,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-foreground/40 p-4 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="confirm-dialog-title"
    >
      <div className="w-full max-w-md rounded-3xl bg-background p-7 shadow-2xl">
        <div className="flex items-start gap-3.5">
          <span
            className={`flex size-10 shrink-0 items-center justify-center rounded-full ${
              destructive ? "bg-destructive/12 text-destructive" : "bg-accent/12 text-accent"
            }`}
          >
            <AlertTriangle className="size-5" />
          </span>
          <div>
            <h2
              id="confirm-dialog-title"
              className="font-heading text-lg font-bold text-foreground"
            >
              {title}
            </h2>
            <p className="mt-2 text-sm leading-relaxed text-muted-foreground">{message}</p>
          </div>
        </div>

        {error && (
          <div className="mt-5 flex items-start gap-2.5 rounded-xl border border-destructive/25 bg-destructive/5 px-4 py-3 text-sm text-destructive">
            <AlertCircle aria-hidden className="mt-0.5 size-4 shrink-0" />
            <span>{getApiErrorMessage(error, "Thao tác thất bại. Vui lòng thử lại.")}</span>
          </div>
        )}

        <div className="mt-7 flex justify-end gap-3">
          <button
            type="button"
            onClick={onCancel}
            disabled={isPending}
            className="rounded-full px-5 py-2.5 text-sm font-600 text-muted-foreground transition-colors hover:bg-secondary disabled:opacity-50"
          >
            Huỷ
          </button>
          <button
            type="button"
            onClick={onConfirm}
            disabled={isPending}
            className={`flex items-center gap-2 rounded-full px-5 py-2.5 font-heading text-sm font-600 uppercase tracking-wider text-white transition-colors disabled:cursor-not-allowed disabled:opacity-60 ${
              destructive ? "bg-destructive hover:bg-destructive/90" : "bg-accent hover:bg-accent/90"
            }`}
          >
            {isPending && <Loader2 className="size-4 animate-spin" />}
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
