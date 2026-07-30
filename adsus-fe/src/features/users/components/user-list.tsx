"use client";

import {
  AlertCircle,
  Ban,
  Loader2,
  Lock,
  Pencil,
  Search,
  UnlockKeyhole,
  UserPlus,
} from "lucide-react";
import Link from "next/link";
import { useState } from "react";

import { getApiErrorMessage } from "@/lib/api-client";
import type { Role } from "@/types/api.types";

import {
  useDeactivateUser,
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
  | { kind: "lock" | "unlock" | "deactivate"; user: UserAccount }
  | null;

/**
 * SCR-06 — danh sách tài khoản (UC-04).
 *
 * Admin tìm kiếm, lọc, và thực hiện FT-08 (khoá / mở khoá / vô hiệu hoá) ngay tại đây.
 * Tạo mới và sửa nằm ở SCR-07.
 */
export function UserList() {
  const [keyword, setKeyword] = useState("");
  const [role, setRole] = useState<Role | "">("");
  const [status, setStatus] = useState<AccountStatus | "">("");
  const [page, setPage] = useState(1);
  const [pending, setPending] = useState<PendingAction>(null);

  const query = { keyword, role, status, page, pageSize: 20 };
  const { data, isLoading, isError, error } = useUserList(query);

  const setLocked = useSetUserLocked();
  const deactivate = useDeactivateUser();

  const actionError = setLocked.error ?? deactivate.error;

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

    setLocked.mutate(
      { userId: pending.user.userId, locked: pending.kind === "lock" },
      { onSettled: () => setPending(null) },
    );
  }

  return (
    <div className="mx-auto max-w-7xl px-6 py-10">
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
                <td className="px-5 py-4 font-600 text-foreground">{user.fullName}</td>
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

                    {/* Tài khoản đã vô hiệu hoá thì không còn thao tác nào — BR-05, một chiều. */}
                    {user.status !== "DEACTIVATED" && (
                      <>
                        <button
                          type="button"
                          title={user.status === "LOCKED" ? "Mở khoá" : "Khoá tài khoản"}
                          onClick={() =>
                            setPending({
                              kind: user.status === "LOCKED" ? "unlock" : "lock",
                              user,
                            })
                          }
                          className="rounded-full p-2 text-muted-foreground transition-colors hover:bg-secondary hover:text-primary"
                        >
                          {user.status === "LOCKED" ? (
                            <UnlockKeyhole className="size-4" />
                          ) : (
                            <Lock className="size-4" />
                          )}
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
        destructive={pending?.kind === "deactivate"}
        isPending={setLocked.isPending || deactivate.isPending}
        title={
          pending?.kind === "deactivate"
            ? "Vô hiệu hoá tài khoản?"
            : pending?.kind === "lock"
              ? "Khoá tài khoản?"
              : "Mở khoá tài khoản?"
        }
        message={
          pending?.kind === "deactivate"
            ? `Tài khoản "${pending.user.fullName}" sẽ không bao giờ đăng nhập lại được. Đây là hành động MỘT CHIỀU, không có cách hoàn tác. Dữ liệu cũ vẫn được giữ nguyên, không bị xoá.`
            : pending?.kind === "lock"
              ? `Tài khoản "${pending.user.fullName}" sẽ không đăng nhập được cho tới khi bạn tự mở khoá. Hệ thống không tự mở khoá.`
              : `Tài khoản "${pending?.user.fullName}" sẽ đăng nhập lại được ngay.`
        }
        confirmLabel={
          pending?.kind === "deactivate"
            ? "Vô hiệu hoá"
            : pending?.kind === "lock"
              ? "Khoá"
              : "Mở khoá"
        }
        onConfirm={runPendingAction}
        onCancel={() => setPending(null)}
      />
    </div>
  );
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
