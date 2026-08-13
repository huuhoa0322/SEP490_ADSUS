"use client";

import {
  AlertCircle,
  Ban,
  CheckCircle2,
  KeyRound,
  Loader2,
  Lock,
  Pencil,
  Search,
  UnlockKeyhole,
  UserPlus,
} from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

import { getApiErrorMessage } from "@/lib/api-client";
import type { Role } from "@/types/api.types";

import {
  useResetUserPassword,
  useSetUserLocked,
  useUserList,
} from "../hooks/use-users";
import {
  formatDateTime,
  ROLE_LABEL,
  STATUS_CLASS,
  STATUS_LABEL,
} from "../lib/user-labels";
import type { AccountStatus, UserAccount } from "../types/user.types";

import { ConfirmDialog } from "./confirm-dialog";

/** Hành động đang chờ người dùng xác nhận. */
type PendingAction =
  | { kind: "lock" | "unlock" | "reset"; user: UserAccount }
  | null;

/**
 * SCR-06 — danh sách tài khoản (UC-04).
 *
 * Admin tìm kiếm, lọc, và thực hiện vô hiệu hoá / mở khoá ngay tại đây.
 * Tạo mới và sửa nằm ở SCR-07.
 */
interface UserListProps {
  /** Thông báo tạo tài khoản do API trả về, được trang chuyển tiếp sang để không mất khi điều hướng. */
  initialCreateNotice?: string;
}

export function UserList({ initialCreateNotice }: UserListProps) {
  const router = useRouter();
  const [keyword, setKeyword] = useState("");
  const [role, setRole] = useState<Role | "">("");
  const [status, setStatus] = useState<AccountStatus | "">("");
  const [page, setPage] = useState(1);
  const [pending, setPending] = useState<PendingAction>(null);
  /** Tên tài khoản vừa được cấp lại mật khẩu, để hiện lời xác nhận. */
  const [resetSentTo, setResetSentTo] = useState<string | null>(null);
  const [createNotice] = useState(initialCreateNotice);

  useEffect(() => {
    if (initialCreateNotice) {
      // Xoá thông báo khỏi URL để tải lại hoặc mở lại trang không hiện cảnh báo cũ lần thứ hai.
      router.replace("/admin/users", { scroll: false });
    }
  }, [initialCreateNotice, router]);

  const query = { keyword, role, status, page, pageSize: 20 };
  const { data, isLoading, isError, error } = useUserList(query);

  const setLocked = useSetUserLocked();
  const resetPassword = useResetUserPassword();

  const actionError = setLocked.error ?? resetPassword.error;

  /** Đổi bộ lọc thì phải quay về trang 1, không thì đang ở trang 5 mà kết quả chỉ có 1 trang. */
  function changeFilter(apply: () => void) {
    apply();
    setPage(1);
  }

  function runPendingAction() {
    if (!pending) return;

    if (pending.kind === "reset") {
      resetPassword.mutate(pending.user.userId, {
        onSuccess: () => setResetSentTo(pending.user.fullName),
        onSettled: () => setPending(null),
      });
      return;
    }

    setLocked.mutate(
      { userId: pending.user.userId, locked: pending.kind === "lock" },
      { onSettled: () => setPending(null) },
    );
  }

  return (
    <div className="mx-auto w-full max-w-screen-2xl px-6 py-10">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-heading text-[32px] font-bold tracking-[-0.02em] text-foreground">
            Quản lý tài khoản
          </h1>
          <p className="mt-1.5 text-[15px] text-muted-foreground">
            Tạo tài khoản, phân quyền, khoá hoặc vô hiệu hoá.
          </p>
        </div>

        <Link
          href="/admin/users/new"
          className="flex h-12 items-center gap-2 rounded-full bg-accent px-6 font-heading text-sm font-600 uppercase tracking-wider text-accent-foreground shadow-lg shadow-accent/25 transition-all hover:bg-accent/90"
        >
          <UserPlus className="size-4" />
          Tạo tài khoản
        </Link>
      </div>

      {createNotice && (
        <div
          role="status"
          className="mt-6 flex items-start gap-2.5 rounded-2xl border border-accent/30 bg-accent/10 px-4 py-3 text-sm text-foreground"
        >
          <CheckCircle2 aria-hidden className="mt-0.5 size-4 shrink-0 text-accent" />
          <span>{createNotice}</span>
        </div>
      )}

      {/* ---- Bộ lọc ---- */}
      <div className="mt-8 flex flex-wrap gap-3">
        <div className="relative min-w-64 flex-1">
          <Search
            aria-hidden
            className="pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
          />
          <input
            value={keyword}
            onChange={(e) => changeFilter(() => setKeyword(e.target.value))}
            placeholder="Tìm theo họ tên hoặc số điện thoại"
            aria-label="Tìm kiếm tài khoản"
            className="h-12 w-full rounded-full border border-border bg-background pl-11 pr-4 text-[15px] outline-none transition-colors focus:border-accent"
          />
        </div>

        <select
          value={role}
          onChange={(e) => changeFilter(() => setRole(e.target.value as Role | ""))}
          aria-label="Lọc theo vai trò"
          className="h-12 rounded-full border border-border bg-background px-5 text-[15px] outline-none focus:border-accent"
        >
          <option value="">Tất cả vai trò</option>
          <option value="DOCTOR">Bác sĩ</option>
          <option value="NURSE">Điều dưỡng</option>
          <option value="PATIENT">Bệnh nhân</option>
          <option value="ADMIN">Quản trị viên</option>
        </select>

        <select
          value={status}
          onChange={(e) => changeFilter(() => setStatus(e.target.value as AccountStatus | ""))}
          aria-label="Lọc theo trạng thái"
          className="h-12 rounded-full border border-border bg-background px-5 text-[15px] outline-none focus:border-accent"
        >
          <option value="">Tất cả trạng thái</option>
          <option value="ACTIVE">Đang hoạt động</option>
          <option value="LOCKED">Đã khoá</option>
          <option value="DEACTIVATED">Đã vô hiệu hoá</option>
        </select>
      </div>

      {(isError || actionError) && (
        <div
          role="alert"
          className="mt-5 flex items-start gap-2.5 rounded-2xl border border-destructive/25 bg-destructive/5 px-4 py-3 text-sm text-destructive"
        >
          <AlertCircle aria-hidden className="mt-0.5 size-4 shrink-0" />
          <span>
            {getApiErrorMessage(
              isError ? error : actionError,
              "Thao tác thất bại. Vui lòng thử lại.",
            )}
          </span>
        </div>
      )}

      {/* ---- Bảng ---- */}
      <div className="mt-6 overflow-x-auto rounded-3xl border border-border bg-background">
        <table className="w-full min-w-4xl border-collapse text-left text-sm">
          <thead>
            <tr className="border-b border-border bg-secondary/40">
              <Th>Họ và tên</Th>
              <Th>Số điện thoại</Th>
              <Th>Email</Th>
              <Th>Vai trò</Th>
              <Th>Trạng thái</Th>
              <Th>Ngày tạo</Th>
              <Th className="text-right">Thao tác</Th>
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr>
                <td colSpan={7} className="px-5 py-14 text-center text-muted-foreground">
                  <Loader2 className="mx-auto size-5 animate-spin" />
                </td>
              </tr>
            )}

            {!isLoading && data?.items.length === 0 && (
              <tr>
                <td colSpan={7} className="px-5 py-14 text-center text-muted-foreground">
                  Không có tài khoản nào khớp với điều kiện lọc.
                </td>
              </tr>
            )}

            {data?.items.map((user) => (
              <tr key={user.userId} className="border-b border-border last:border-0">
                <td className="px-5 py-4 font-600 text-foreground">
                  {user.fullName}
                  {user.isCurrentUser && (
                    <span className="ml-2 rounded-full bg-secondary px-2 py-0.5 text-xs font-500 text-muted-foreground">
                      Bạn
                    </span>
                  )}
                </td>
                <td className="px-5 py-4 tabular-nums">{user.phoneNumber}</td>
                <td className="px-5 py-4 text-muted-foreground">{user.email ?? "—"}</td>
                <td className="px-5 py-4">{ROLE_LABEL[user.role]}</td>
                <td className="px-5 py-4">
                  <span
                    className={`inline-flex rounded-full px-3 py-1 text-xs font-600 ${STATUS_CLASS[user.status]}`}
                  >
                    {STATUS_LABEL[user.status]}
                  </span>
                </td>
                <td className="px-5 py-4 text-muted-foreground">
                  {formatDateTime(user.createdAt)}
                </td>
                <td className="px-5 py-4">
                  <div className="flex items-center justify-end gap-1">
                    <Link
                      href={`/admin/users/${user.userId}`}
                      title="Sửa thông tin và phân quyền"
                      className="rounded-full p-2 text-muted-foreground transition-colors hover:bg-secondary hover:text-primary"
                    >
                      <Pencil className="size-4" />
                    </Link>

                    {/* Không bày nút vô hiệu hoá / cấp lại mật khẩu trên dòng của
                        chính mình: backend chặn hết (UC-04 AF-04), bấm vào chỉ nhận lỗi.
                        Vẫn giữ nút sửa, vì Admin đổi được tên và email của chính mình. */}
                    {!user.isCurrentUser && (
                      <>
                        {/* UC-03 AF-02 — cấp lại mật khẩu hộ. Chỉ hiện khi tài khoản có
                            email, vì mật khẩu tạm chỉ giao qua email (BR-03). */}
                        {user.email && (
                          <button
                            type="button"
                            title="Cấp lại mật khẩu (gửi qua email)"
                            onClick={() => setPending({ kind: "reset", user })}
                            className="rounded-full p-2 text-muted-foreground transition-colors hover:bg-secondary hover:text-primary"
                          >
                            <KeyRound className="size-4" />
                          </button>
                        )}

                        <button
                          type="button"
                          title={user.status === "DEACTIVATED" ? "Mở khoá" : "Vô hiệu hoá"}
                          onClick={() =>
                            setPending({
                              kind: user.status === "DEACTIVATED" ? "unlock" : "lock",
                              user,
                            })
                          }
                          className="rounded-full p-2 text-muted-foreground transition-colors hover:bg-secondary hover:text-primary"
                        >
                          {user.status === "DEACTIVATED" ? (
                            <UnlockKeyhole className="size-4" />
                          ) : (
                            <Ban className="size-4" />
                          )}
                        </button>
                      </>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* ---- Phân trang ---- */}
      {data && data.totalCount > 0 && (
        <div className="mt-5 flex items-center justify-between text-sm text-muted-foreground">
          <span>
            {data.totalCount} tài khoản · trang {data.page}/{data.totalPages}
          </span>
          <div className="flex gap-2">
            <PagerButton
              disabled={data.page <= 1}
              onClick={() => setPage((p) => p - 1)}
            >
              Trước
            </PagerButton>
            <PagerButton
              disabled={data.page >= data.totalPages}
              onClick={() => setPage((p) => p + 1)}
            >
              Sau
            </PagerButton>
          </div>
        </div>
      )}

      <ConfirmDialog
        open={pending !== null}
        destructive={pending?.kind === "lock"}
        isPending={setLocked.isPending || resetPassword.isPending}
        title={CONFIRM_TITLE[pending?.kind ?? "lock"]}
        message={pending ? buildConfirmMessage(pending) : ""}
        confirmLabel={CONFIRM_LABEL[pending?.kind ?? "lock"]}
        onConfirm={runPendingAction}
        onCancel={() => setPending(null)}
      />

      {/* Xác nhận đã gửi. Cố ý KHÔNG hiện mật khẩu tạm — nó chỉ đi qua email (BR-03). */}
      {resetSentTo && (
        <div className="fixed bottom-6 left-1/2 z-50 flex -translate-x-1/2 items-center gap-2.5 rounded-full border border-accent/25 bg-background px-5 py-3 text-sm shadow-xl">
          <CheckCircle2 className="size-4 shrink-0 text-accent" />
          <span>
            Đã gửi mật khẩu mới tới email của {resetSentTo}.
          </span>
          <button
            type="button"
            onClick={() => setResetSentTo(null)}
            className="ml-1 text-muted-foreground transition-colors hover:text-primary"
          >
            Đóng
          </button>
        </div>
      )}
    </div>
  );
}

const CONFIRM_TITLE: Record<NonNullable<PendingAction>["kind"], string> = {
  lock: "Vô hiệu hoá tài khoản?",
  unlock: "Mở khoá tài khoản?",
  reset: "Cấp lại mật khẩu?",
};

const CONFIRM_LABEL: Record<NonNullable<PendingAction>["kind"], string> = {
  lock: "Vô hiệu hoá",
  unlock: "Mở khoá",
  reset: "Cấp lại",
};

function buildConfirmMessage(pending: NonNullable<PendingAction>): string {
  const name = pending.user.fullName;

  switch (pending.kind) {
    case "lock":
      return `Tài khoản "${name}" sẽ bị vô hiệu hoá và không thể đăng nhập. Hệ thống sẽ không tự mở khoá, bạn có thể tự mở khoá lại sau này.`;
    case "unlock":
      return `Tài khoản "${name}" sẽ đăng nhập lại được ngay.`;
    case "reset":
      // BR-03 — nói trước rằng mật khẩu chỉ đi qua email, để Admin không chờ nó hiện ra.
      return `Hệ thống sẽ sinh mật khẩu mới và gửi tới email ${pending.user.email}. Mật khẩu KHÔNG hiển thị ở đây. Mật khẩu cũ của "${name}" sẽ hết hiệu lực ngay.`;
  }
}

function Th({ children, className = "" }: { children: React.ReactNode; className?: string }) {
  return (
    <th
      className={`px-5 py-3.5 font-heading text-xs font-600 uppercase tracking-wider text-muted-foreground ${className}`}
    >
      {children}
    </th>
  );
}

function PagerButton({
  children,
  disabled,
  onClick,
}: {
  children: React.ReactNode;
  disabled: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      className="rounded-full border border-border px-4 py-2 transition-colors hover:bg-secondary disabled:cursor-not-allowed disabled:opacity-40"
    >
      {children}
    </button>
  );
}
