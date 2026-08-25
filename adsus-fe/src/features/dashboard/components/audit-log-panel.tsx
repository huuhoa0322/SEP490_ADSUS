"use client";

import { useQuery } from "@tanstack/react-query";
import {
  KeyRound,
  Loader2,
  Lock,
  LockOpen,
  PencilLine,
  UserMinus,
  UserPlus,
} from "lucide-react";
import type { ReactNode } from "react";

import { getApiErrorMessage } from "@/lib/api-client";

import { getRecentAuditLogs } from "../api/audit-log.api";
import type { AuditLogEntry } from "../types/audit-log.types";

/**
 * Nhãn tiếng Việt và biểu tượng cho từng hành động.
 *
 * Hành động nào không có ở đây thì hiện nguyên mã tiếng Anh — module khác cũng ghi vào cùng
 * bảng nhật ký này (đăng ký, kích hoạt phiên bản mô hình AI...), và hiện mã thô vẫn đọc hiểu
 * được, còn hơn bỏ trống dòng đó.
 */
const ACTIONS: Record<string, { label: string; icon: ReactNode; tone: string }> = {
  CREATE_ACCOUNT: {
    label: "Tạo tài khoản",
    icon: <UserPlus className="size-4" />,
    tone: "text-[var(--status-good)]",
  },
  UPDATE_ACCOUNT: {
    label: "Sửa tài khoản",
    icon: <PencilLine className="size-4" />,
    tone: "text-muted-foreground",
  },
  LOCK_ACCOUNT: {
    label: "Khoá tài khoản",
    icon: <Lock className="size-4" />,
    tone: "text-[var(--status-warning)]",
  },
  UNLOCK_ACCOUNT: {
    label: "Mở khoá tài khoản",
    icon: <LockOpen className="size-4" />,
    tone: "text-[var(--status-good)]",
  },
  DEACTIVATE_ACCOUNT: {
    label: "Vô hiệu hoá tài khoản",
    icon: <UserMinus className="size-4" />,
    tone: "text-[var(--status-critical)]",
  },
  ADMIN_RESET_PASSWORD: {
    label: "Cấp lại mật khẩu",
    icon: <KeyRound className="size-4" />,
    tone: "text-[var(--status-warning)]",
  },
  SELF_RESET_PASSWORD: {
    label: "Người dùng tự cấp lại mật khẩu",
    icon: <KeyRound className="size-4" />,
    tone: "text-muted-foreground",
  },
};

/**
 * Mười thao tác quản trị gần nhất (UC-04).
 *
 * Cố ý KHÔNG lấy theo khoảng thời gian đang chọn ở trên: khoảng lọc dùng để so sánh số liệu
 * giữa các kỳ, còn câu hỏi ở đây luôn là "vừa có ai động vào cái gì" — chọn về tháng trước
 * rồi thấy danh sách trống thì tưởng hệ thống không ghi.
 */
export function AuditLogPanel() {
  const { data, isLoading, isError, error } = useQuery({
    queryKey: ["audit-logs", "recent", 10] as const,
    queryFn: () => getRecentAuditLogs(10),
  });

  return (
    <section className="rounded-3xl border border-border bg-background p-6">
      <h2 className="font-heading text-[17px] font-bold tracking-[-0.01em] text-foreground">
        Thao tác quản trị gần đây
      </h2>

      <div className="mt-6">
        {isLoading && (
          <div className="flex min-h-32 items-center justify-center">
            <Loader2 className="size-5 animate-spin text-muted-foreground" />
          </div>
        )}

        {isError && (
          <p role="alert" className="text-sm text-destructive">
            {getApiErrorMessage(error, "Không tải được nhật ký thao tác.")}
          </p>
        )}

        {data && data.length === 0 && (
          <p className="text-sm text-muted-foreground">
            Chưa có thao tác nào được ghi lại.
          </p>
        )}

        {data && data.length > 0 && (
          <ol className="flex flex-col">
            {data.map((entry) => (
              <AuditRow key={entry.logId} entry={entry} />
            ))}
          </ol>
        )}
      </div>
    </section>
  );
}

function AuditRow({ entry }: { entry: AuditLogEntry }) {
  const action = ACTIONS[entry.action];

  return (
    <li className="flex items-start gap-3 border-b border-border py-3 last:border-0 last:pb-0">
      <span className={`mt-0.5 shrink-0 ${action?.tone ?? "text-muted-foreground"}`}>
        {action?.icon ?? <PencilLine className="size-4" />}
      </span>

      <div className="min-w-0 flex-1">
        <p className="text-sm text-foreground">
          <span className="font-600">{action?.label ?? entry.action}</span>
          {entry.detail && (
            <>
              {" — "}
              <span className="text-muted-foreground">{entry.detail}</span>
            </>
          )}
        </p>
        <p className="mt-0.5 text-xs text-muted-foreground">
          {entry.actorName} · {formatWhen(entry.performedAt)}
        </p>
      </div>
    </li>
  );
}

/**
 * Backend trả về giờ UTC. Hiển thị theo giờ máy người dùng để "14:30" đúng là 14:30 ở phòng
 * khám — đọc thẳng chuỗi UTC thì mọi mốc đều lệch 7 tiếng.
 */
function formatWhen(iso: string): string {
  // Chuỗi từ .NET không phải lúc nào cũng có hậu tố Z; thiếu nó thì trình duyệt hiểu là giờ
  // địa phương và mốc thời gian sai đúng 7 tiếng.
  const date = new Date(/[Zz]|[+-]\d{2}:\d{2}$/.test(iso) ? iso : `${iso}Z`);
  if (Number.isNaN(date.getTime())) return "—";

  return date.toLocaleString("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
}
