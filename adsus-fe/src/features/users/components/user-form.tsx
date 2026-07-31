"use client";

import { AlertCircle, ArrowLeft, CheckCircle2, Loader2 } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";

import { getApiErrorMessage } from "@/lib/api-client";

import { useCreateUser, useUpdateUser, useUserDetail } from "../hooks/use-users";
import { ROLE_LABEL } from "../lib/user-labels";
import {
  ASSIGNABLE_ROLES,
  type AssignableRole,
  type UserAccount,
} from "../types/user.types";

interface UserFormProps {
  /** Không truyền là đang tạo mới; có truyền là đang sửa tài khoản đó. */
  userId?: string;
}

/**
 * SCR-07 — tạo và sửa tài khoản (UC-04, FT-07 và FT-09).
 *
 * Lớp ngoài chỉ lo tải dữ liệu. Form thật nằm ở [UserFormFields] và chỉ được dựng khi đã có
 * đủ dữ liệu, nên khởi tạo state một lần từ prop là xong — không cần useEffect đồng bộ lại,
 * mà đó chính là kiểu gây render dây chuyền.
 */
export function UserForm({ userId }: UserFormProps) {
  const detail = useUserDetail(userId);

  if (userId && detail.isLoading) {
    return (
      <div className="flex min-h-96 items-center justify-center">
        <Loader2 className="size-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (userId && detail.isError) {
    return (
      <div className="mx-auto max-w-2xl px-6 py-10">
        <BackLink />
        <div
          role="alert"
          className="mt-6 flex items-start gap-2.5 rounded-2xl border border-destructive/25 bg-destructive/5 px-4 py-3 text-sm text-destructive"
        >
          <AlertCircle aria-hidden className="mt-0.5 size-4 shrink-0" />
          <span>{getApiErrorMessage(detail.error, "Không tải được tài khoản.")}</span>
        </div>
      </div>
    );
  }

  return (
    <UserFormFields
      // Đổi tài khoản là dựng lại form từ đầu, không sót dữ liệu người trước.
      key={detail.data?.userId ?? "new"}
      userId={userId}
      initial={detail.data}
    />
  );
}

function UserFormFields({
  userId,
  initial,
}: {
  userId?: string;
  initial?: UserAccount;
}) {
  const router = useRouter();
  const isEdit = Boolean(userId);

  /**
   * Tài khoản quản trị viên thì KHOÁ ô vai trò lại.
   *
   * UC-04 chỉ cho gán Bác sĩ / Điều dưỡng / Bệnh nhân, nên danh sách không có ADMIN. Trước
   * đây mở một tài khoản Admin ra sửa là ô này rơi về "Bác sĩ" — chỉ cần bấm Lưu để đổi cái
   * tên là tự hạ quyền chính mình, không hề có cảnh báo nào. Backend cũng đã chặn, đây là
   * lớp thứ hai và cũng để giao diện nói đúng sự thật.
   */
  const isAdminAccount = initial?.role === "ADMIN";

  const [phoneNumber, setPhoneNumber] = useState(initial?.phoneNumber ?? "");
  const [fullName, setFullName] = useState(initial?.fullName ?? "");
  const [role, setRole] = useState<AssignableRole>(() =>
    initial && ASSIGNABLE_ROLES.includes(initial.role as AssignableRole)
      ? (initial.role as AssignableRole)
      : "DOCTOR",
  );
  const [email, setEmail] = useState(initial?.email ?? "");
  const [dateOfBirth, setDateOfBirth] = useState(initial?.dateOfBirth ?? "");
  const [clientError, setClientError] = useState<string | null>(null);

  const create = useCreateUser();
  const update = useUpdateUser(userId ?? "");

  const isPatient = role === "PATIENT";
  const isSubmitting = create.isPending || update.isPending;
  const serverError = create.error ?? update.error;

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setClientError(null);

    if (!fullName.trim()) {
      setClientError("Vui lòng nhập họ và tên.");
      return;
    }

    if (!isEdit && !/^0\d{8,10}$/.test(phoneNumber.trim())) {
      setClientError("Số điện thoại phải bắt đầu bằng 0 và có 9 đến 11 chữ số.");
      return;
    }

    // BR-01 — vai trò Bệnh nhân thì KHÔNG gửi ngày sinh lên, kể cả khi người dùng đã gõ
    // trước lúc đổi vai trò. Backend cũng tự loại, đây là lớp chặn thứ hai.
    const payload = {
      fullName: fullName.trim(),
      email: email.trim() || null,
      dateOfBirth: isPatient ? null : dateOfBirth || null,
    };

    if (isEdit) {
      update.mutate(
        {
          ...payload,
          // Gửi lại đúng ADMIN thay vì giá trị trong ô đã bị khoá — nói dối trên đường
          // truyền để đi qua kiểm tra là kiểu về sau không ai lần ra được.
          role: isAdminAccount ? "ADMIN" : role,
        },
        { onSuccess: () => router.push("/admin/users") },
      );
      return;
    }

    // Tạo mới thì không bao giờ có tài khoản Admin (UC-04), nên vai trò luôn nằm trong ba
    // giá trị gán được.
    create.mutate(
      { ...payload, role, phoneNumber: phoneNumber.trim() },
      { onSuccess: () => router.push("/admin/users") },
    );
  }

  const errorMessage =
    clientError ??
    (serverError ? getApiErrorMessage(serverError, "Lưu thất bại. Vui lòng thử lại.") : null);

  return (
    <div className="mx-auto max-w-2xl px-6 py-10">
      <BackLink />

      <h1 className="mt-5 font-heading text-[32px] font-bold tracking-[-0.02em] text-foreground">
        {isEdit ? "Sửa tài khoản" : "Tạo tài khoản"}
      </h1>

      {!isEdit && (
        <p className="mt-2 text-[15px] leading-relaxed text-muted-foreground">
          Hệ thống sẽ tự sinh mật khẩu tạm và gửi tới email của người dùng. Họ phải đổi mật
          khẩu ngay ở lần đăng nhập đầu tiên.
        </p>
      )}

      <form onSubmit={handleSubmit} noValidate className="mt-8 flex flex-col gap-5">
        <Field
          label="Số điện thoại"
          hint={isEdit ? "Không đổi được — đây là tài khoản đăng nhập" : undefined}
        >
          <input
            value={phoneNumber}
            onChange={(e) => setPhoneNumber(e.target.value)}
            /* BR-02 — số điện thoại là định danh đăng nhập, sửa thì khoá lại. */
            disabled={isEdit || isSubmitting}
            inputMode="numeric"
            maxLength={15}
            placeholder="0900000000"
            className={inputClass}
          />
        </Field>

        <Field label="Họ và tên">
          <input
            value={fullName}
            onChange={(e) => setFullName(e.target.value)}
            disabled={isSubmitting}
            maxLength={100}
            className={inputClass}
          />
        </Field>

        <Field
          label="Vai trò"
          hint={
            isAdminAccount
              ? "Không đổi được — tài khoản quản trị viên được cấp lúc dựng hệ thống"
              : undefined
          }
        >
          {isAdminAccount ? (
            /* Ô chữ đọc-được thay vì select bị vô hiệu hoá: select có mũi tên xổ xuống,
               nhìn như bấm được mà bấm lại không ra gì. */
            <p className={`${inputClass} flex items-center bg-secondary/50 text-muted-foreground`}>
              {ROLE_LABEL.ADMIN}
            </p>
          ) : (
            <select
              value={role}
              onChange={(e) => setRole(e.target.value as AssignableRole)}
              disabled={isSubmitting}
              className={inputClass}
            >
              {ASSIGNABLE_ROLES.map((r) => (
                <option key={r} value={r}>
                  {ROLE_LABEL[r]}
                </option>
              ))}
            </select>
          )}
        </Field>

        <Field label="Email" hint="Không bắt buộc, nhưng thiếu thì không gửi được mật khẩu tạm">
          <input
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            disabled={isSubmitting}
            type="email"
            maxLength={255}
            className={inputClass}
          />
        </Field>

        {/* BR-01 — Bệnh nhân thì ẩn HẲN ô ngày sinh, không phải chỉ vô hiệu hoá.
            Ngày sinh của bệnh nhân là dữ liệu y tế, Admin không được xem. */}
        {isPatient ? (
          <p className="rounded-2xl border border-border bg-secondary/40 px-4 py-3 text-sm text-muted-foreground">
            Ngày sinh của bệnh nhân là dữ liệu y tế, do bác sĩ quản lý trong hồ sơ bệnh án.
            Màn hình quản trị không hiển thị và không sửa được.
          </p>
        ) : (
          <Field label="Ngày sinh" hint="Không bắt buộc">
            <input
              value={dateOfBirth}
              onChange={(e) => setDateOfBirth(e.target.value)}
              disabled={isSubmitting}
              type="date"
              max={new Date().toISOString().slice(0, 10)}
              className={inputClass}
            />
          </Field>
        )}

        {errorMessage && (
          <div
            role="alert"
            className="flex items-start gap-2.5 rounded-2xl border border-destructive/25 bg-destructive/5 px-4 py-3 text-sm text-destructive"
          >
            <AlertCircle aria-hidden className="mt-0.5 size-4 shrink-0" />
            <span>{errorMessage}</span>
          </div>
        )}

        <button
          type="submit"
          disabled={isSubmitting}
          className="mt-2 flex h-14 items-center justify-center gap-2 rounded-full bg-accent font-heading text-sm font-600 uppercase tracking-wider text-accent-foreground shadow-lg shadow-accent/25 transition-all hover:bg-accent/90 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {isSubmitting ? (
            <>
              <Loader2 className="size-4 animate-spin" />
              Đang lưu
            </>
          ) : (
            <>
              <CheckCircle2 className="size-4" />
              {isEdit ? "Lưu thay đổi" : "Tạo tài khoản"}
            </>
          )}
        </button>
      </form>
    </div>
  );
}

const inputClass =
  "h-14 w-full rounded-full border border-border bg-background px-5 text-[15px] outline-none transition-colors focus:border-accent disabled:bg-secondary/50 disabled:text-muted-foreground";

function BackLink() {
  return (
    <Link
      href="/admin/users"
      className="inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-primary"
    >
      <ArrowLeft className="size-4" />
      Danh sách tài khoản
    </Link>
  );
}

function Field({
  label,
  hint,
  children,
}: {
  label: string;
  hint?: string;
  children: React.ReactNode;
}) {
  return (
    <label className="flex flex-col gap-2.5">
      <span className="font-heading text-[13px] font-600 uppercase tracking-wider text-foreground">
        {label}
      </span>
      {children}
      {hint && <span className="px-5 text-xs text-muted-foreground">{hint}</span>}
    </label>
  );
}
