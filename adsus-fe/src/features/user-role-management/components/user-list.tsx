"use client";

import {
  AlertCircle,
  Ban,
  CheckCircle2,
  KeyRound,
  Loader2,
  Pencil,
  RefreshCcw,
  Search,
  UserPlus,
} from "lucide-react";
import Link from "next/link";
import { useState } from "react";

import { getApiErrorMessage } from "@/lib/api-client";
import type { Role } from "@/types/api.types";

import {
  useDeactivateUser,
  useReactivateUser,
  useResetUserPassword,
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
type PendingAction = { kind: "deactivate" | "reset" | "reactivate"; user: UserAccount } | null;

/**
 * SCR-06 — danh sách tài khoản (UC-04).
 *
 * Admin tìm kiếm, lọc, và thực hiện FT-08 (vô hiệu hoá) ngay tại đây — Lock/Unlock đã bỏ khỏi
 * hệ thống (quyết định 13/08/2026), chỉ còn Active/Deactivated.
 * Tạo mới và sửa nằm ở SCR-07. Thông báo "đã tạo" không còn đi qua trang này nữa — SCR-07 tự
 * hiện mật khẩu tạm ngay tại chỗ (sửa 12/08/2026, xem UserForm).
 */

export function UserList() {
  const [keyword, setKeyword] = useState("");
  const [role, setRole] = useState<Role | "">("");
  const [status, setStatus] = useState<AccountStatus | "">("");
  const [page, setPage] = useState(1);
  const [pending, setPending] = useState<PendingAction>(null);
  /**
   * Mật khẩu tạm vừa cấp lại — hiện được đúng một lần ở đây (sửa 12/08/2026, không còn gửi
   * qua email, thống nhất với UC-04/UC-06).
   */
  const [resetResult, setResetResult] = useState<{
    fullName: string;
    temporaryPassword: string;
  } | null>(null);

  const query = { keyword, role, status, page, pageSize: 20 };
  const { data, isLoading, isError, error } = useUserList(query);

  const deactivate = useDeactivateUser();
  const reactivate = useReactivateUser();
  const resetPassword = useResetUserPassword();

  const actionError = deactivate.error ?? reactivate.error ?? resetPassword.error;

  /** Đổi bộ lọc thì phải quay về trang 1, không thì đang ở trang 5 mà kết quả chỉ có 1 trang. */
  function changeFilter(apply: () => void) {
    apply();
    setPage(1);
  }

  function runPendingAction() {
    if (!pending) return;

    if (pending.kind === "deactivate") {
      deactivate.mutate(pending.user.userId, { onSettled: () => setPending(null) });
      return;
    }

    if (pending.kind === "reactivate") {
      reactivate.mutate(pending.user.userId, { onSettled: () => setPending(null) });
      return;
    }

    resetPassword.mutate(pending.user.userId, {
      onSuccess: (temporaryPassword) =>
        setResetResult({ fullName: pending.user.fullName, temporaryPassword }),
      onSettled: () => setPending(null),
    });
  }

  return (
    <div className="mx-auto w-full max-w-screen-2xl px-6 py-8">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-heading text-[32px] font-bold tracking-[-0.02em] text-foreground">
            Quản lý tài khoản
          </h1>
          <p className="mt-1.5 text-[15px] text-muted-foreground">
            Tạo tài khoản, phân quyền, vô hiệu hoá.
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
                        Vẫn giữ nút sửa, vì Admin đổi được tên và email của chính mình.

                        Tài khoản đã vô hiệu hoá có thể được khôi phục. */}
                    {user.status !== "DEACTIVATED" && !user.isCurrentUser && (
                      <>
                        {/* UC-03 AF-02 — cấp lại mật khẩu hộ, hiện ngay trên màn hình sau
                            khi xác nhận (sửa 12/08/2026, không còn phụ thuộc tài khoản có
                            khai email hay không). */}
                        <button
                          type="button"
                          title="Cấp lại mật khẩu"
                          onClick={() => setPending({ kind: "reset", user })}
                          className="rounded-full p-2 text-muted-foreground transition-colors hover:bg-secondary hover:text-primary"
                        >
                          <KeyRound className="size-4" />
                        </button>

                        <button
                          type="button"
                          title="Vô hiệu hoá vĩnh viễn"
                          onClick={() => setPending({ kind: "deactivate", user })}
                          className="rounded-full p-2 text-muted-foreground transition-colors hover:bg-destructive/10 hover:text-destructive"
                        >
                          <Ban className="size-4" />
                        </button>
                      </>
                    )}

                    {user.status === "DEACTIVATED" && (
                      <button
                        type="button"
                        title="Khôi phục tài khoản"
                        onClick={() => setPending({ kind: "reactivate", user })}
                        className="rounded-full p-2 text-muted-foreground transition-colors hover:bg-emerald-500/10 hover:text-emerald-500"
                      >
                        <RefreshCcw className="size-4" />
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* ---- Phân trang ---- */}
      {data && data.totalPages > 1 && (
        <div className="mt-5 flex items-center justify-between text-sm text-muted-foreground">
          <span>
            Đang xem {data.items.length} / {data.totalCount} kết quả
          </span>
          <div className="flex gap-2">
            <PagerButton disabled={data.page <= 1} onClick={() => setPage((p) => p - 1)}>
              Trước
            </PagerButton>
            
            <div className="flex gap-1.5 items-center mx-2">
              {(() => {
                const total = data.totalPages;
                const current = data.page;
                let pages: number[] = [];
                if (total <= 5) {
                  pages = Array.from({ length: total }, (_, i) => i + 1);
                } else if (current <= 3) {
                  pages = [1, 2, 3, 4, 5];
                } else if (current >= total - 2) {
                  pages = [total - 4, total - 3, total - 2, total - 1, total];
                } else {
                  pages = [current - 2, current - 1, current, current + 1, current + 2];
                }

                return pages.map((p) => {
                  const active = p === current;
                  return (
                    <button
                      key={p}
                      onClick={() => setPage(p)}
                      className={`flex h-10 min-w-10 items-center justify-center rounded-full border px-3 text-sm transition-colors ${
                        active
                          ? "border-accent bg-accent font-bold text-white shadow-sm"
                          : "border-border hover:bg-secondary text-foreground"
                      }`}
                    >
                      {p}
                    </button>
                  );
                });
              })()}
            </div>

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
        destructive={pending?.kind === "deactivate"}
        isPending={deactivate.isPending || reactivate.isPending || resetPassword.isPending}
        title={CONFIRM_TITLE[pending?.kind ?? "deactivate"]}
        message={pending ? buildConfirmMessage(pending) : ""}
        confirmLabel={CONFIRM_LABEL[pending?.kind ?? "deactivate"]}
        onConfirm={runPendingAction}
        onCancel={() => setPending(null)}
      />

      {/* Mật khẩu tạm hiện được đúng một lần ở đây (sửa 12/08/2026, không còn gửi email). */}
      {resetResult && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-foreground/40 p-4 backdrop-blur-sm"
          role="dialog"
          aria-modal="true"
          aria-labelledby="reset-result-title"
        >
          <div className="w-full max-w-md rounded-3xl bg-background p-7 shadow-2xl">
            <h2
              id="reset-result-title"
              className="font-heading text-lg font-bold text-foreground"
            >
              Đã cấp lại mật khẩu cho {resetResult.fullName}
            </h2>
            <p className="mt-2 text-sm leading-relaxed text-muted-foreground">
              Đọc mật khẩu tạm dưới đây cho họ nghe hoặc ghi lại — mật khẩu chỉ hiện được đúng
              một lần ở đây, sẽ không hiện lại được nữa. Họ bắt buộc phải đổi mật khẩu ngay khi
              đăng nhập lần đầu.
            </p>

            <div className="mt-5 rounded-2xl border border-dashed border-accent bg-accent/5 px-4 py-3">
              <div className="font-heading text-xs font-600 uppercase tracking-wider text-muted-foreground">
                Mật khẩu tạm
              </div>
              <div className="mt-1 select-all break-all font-mono text-xl font-bold tracking-wider text-foreground">
                {resetResult.temporaryPassword}
              </div>
            </div>

            <div className="mt-6 flex justify-end">
              <button
                type="button"
                onClick={() => setResetResult(null)}
                className="flex items-center gap-2 rounded-full bg-accent px-5 py-2.5 font-heading text-sm font-600 uppercase tracking-wider text-accent-foreground transition-colors hover:bg-accent/90"
              >
                <CheckCircle2 className="size-4" />
                Đã đọc cho họ — Xong
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

const CONFIRM_TITLE: Record<NonNullable<PendingAction>["kind"], string> = {
  deactivate: "Vô hiệu hoá tài khoản?",
  reset: "Cấp lại mật khẩu?",
  reactivate: "Khôi phục tài khoản?",
};

const CONFIRM_LABEL: Record<NonNullable<PendingAction>["kind"], string> = {
  deactivate: "Vô hiệu hoá",
  reset: "Cấp lại",
  reactivate: "Khôi phục",
};

function buildConfirmMessage(pending: NonNullable<PendingAction>): string {
  const name = pending.user.fullName;

  switch (pending.kind) {
    case "deactivate":
      // Cảnh báo trước khi vô hiệu hóa.
      return `Tài khoản "${name}" sẽ không bao giờ đăng nhập lại được. Dữ liệu cũ vẫn được giữ nguyên, không bị xoá.`;
    case "reset":
      // Sửa 12/08/2026 — không còn gửi email, mật khẩu hiện ngay trên màn hình sau khi xác nhận.
      return `Hệ thống sẽ sinh mật khẩu mới và hiện ngay tại đây để bạn đọc trực tiếp cho "${name}". Mật khẩu cũ sẽ hết hiệu lực ngay.`;
    case "reactivate":
      return `Tài khoản "${name}" sẽ được khôi phục hoạt động trở lại và có thể đăng nhập bình thường.`;
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
