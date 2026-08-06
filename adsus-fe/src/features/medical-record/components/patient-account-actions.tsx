"use client";

import { useState } from "react";

import { getApiErrorMessage } from "@/lib/api-client";
import { useAuthStore } from "@/store/auth-store";

import {
  useResetPatientAccountPassword,
  useUpdatePatientAccountContact,
} from "../hooks/use-patient-account";

const PHONE_PATTERN = /^0\d{9}$/;

interface Props {
  userId: string;
  fullName: string;
  phone: string;
  dateOfBirth: string | null;
  email: string | null;
}

/**
 * UC-06 AF-02 và AF-03 — CHỈ Điều dưỡng (BR-03).
 *
 * Component tự trả về null với vai trò khác, thay vì để nơi gọi phải nhớ bọc điều kiện. Đây
 * là ngoại lệ đầu tiên trong bộ quyền vốn giống hệt nhau giữa Bác sĩ và Điều dưỡng, nên gom
 * luật vào đúng một chỗ để không sót.
 *
 * Lưu ý: đây chỉ là lớp trải nghiệm. Chặn thật nằm ở [Authorize(Roles="NURSE")] phía backend.
 *
 * AF-03 (cấp lại mật khẩu) sửa lại 06/08/2026 (Task C11-ext, sau Task C10-ext đổi luồng tạo
 * tài khoản #28): có email thì vẫn gửi âm thầm như cũ, Điều dưỡng không thấy giá trị. KHÔNG có
 * email thì backend không còn báo lỗi chặn nữa — trả plaintext MỘT LẦN để hiện ngay tại đây.
 */
export function PatientAccountActions({ userId, fullName, phone, dateOfBirth, email }: Props) {
  const isNurse = useAuthStore((state) => state.user?.role) === "NURSE";

  const [editing, setEditing] = useState(false);
  const [confirmingReset, setConfirmingReset] = useState(false);
  // undefined = chưa cấp lại lần nào; null = vừa cấp lại xong, đã gửi email (không có gì để
  // hiện); chuỗi = vừa cấp lại xong, không có email, hiện plaintext. Khởi tạo undefined chứ
  // không phải null: nếu khởi tạo null thì nhánh "đã gửi email" luôn set null → null, React
  // coi là không đổi giá trị nên BỎ QUA render lại — UI không bao giờ cập nhật sau khi xác nhận.
  const [revealedPassword, setRevealedPassword] = useState<string | null | undefined>(undefined);

  const [formFullName, setFormFullName] = useState(fullName);
  const [formPhone, setFormPhone] = useState(phone);
  const [formDateOfBirth, setFormDateOfBirth] = useState(dateOfBirth ?? "");
  const [formEmail, setFormEmail] = useState(email ?? "");
  const [clientError, setClientError] = useState<string | null>(null);

  const updateMutation = useUpdatePatientAccountContact(userId);
  const resetMutation = useResetPatientAccountPassword(userId);

  // Bác sĩ không thấy gì cả — không phải nút mờ, mà là không có nút.
  if (!isNurse) return null;

  const editError =
    clientError ??
    (updateMutation.isError
      ? getApiErrorMessage(updateMutation.error, "Cập nhật thông tin tài khoản thất bại.")
      : null);

  function handleSave() {
    setClientError(null);

    if (!formFullName.trim()) {
      setClientError("Vui lòng nhập họ và tên.");
      return;
    }

    if (!PHONE_PATTERN.test(formPhone.trim())) {
      setClientError("Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.");
      return;
    }

    updateMutation.mutate(
      {
        fullName: formFullName.trim(),
        phoneNumber: formPhone.trim(),
        dateOfBirth: formDateOfBirth || null,
        email: formEmail.trim() || null,
      },
      { onSuccess: () => setEditing(false) },
    );
  }

  return (
    <section className="mt-6 rounded-xl border border-dashed border-primary/50 p-5">
      <div className="mb-3 flex items-center gap-2">
        <h2 className="text-sm font-semibold text-foreground">Thông tin tài khoản</h2>
        <span className="rounded bg-accent px-2 py-0.5 text-xs font-semibold text-accent-foreground">
          Chỉ Điều dưỡng
        </span>
      </div>

      {!editing && !confirmingReset ? (
        <div className="flex flex-wrap gap-3">
          <button
            type="button"
            onClick={() => setEditing(true)}
            className="rounded-lg border border-border px-4 py-2 text-sm font-medium hover:bg-accent"
          >
            Sửa thông tin tài khoản
          </button>
          <button
            type="button"
            onClick={() => setConfirmingReset(true)}
            className="rounded-lg border border-border px-4 py-2 text-sm font-medium hover:bg-accent"
          >
            Cấp lại mật khẩu
          </button>
        </div>
      ) : null}

      {confirmingReset ? (
        <div className="rounded-lg bg-muted/50 p-4">
          {/* Không có đường hoàn tác: mật khẩu cũ chết ngay khi API trả về thành công. */}
          <p className="text-sm text-foreground">
            Cấp lại mật khẩu cho <strong>{fullName}</strong>? Mật khẩu hiện tại sẽ không dùng
            được nữa; mật khẩu tạm mới sẽ gửi qua email nếu tài khoản có email, hoặc hiện trực
            tiếp ở đây nếu chưa có email.
          </p>

          {resetMutation.isError ? (
            <p className="mt-3 rounded-lg bg-destructive/10 p-3 text-sm text-destructive" role="alert">
              {getApiErrorMessage(resetMutation.error, "Cấp lại mật khẩu thất bại.")}
            </p>
          ) : null}

          {revealedPassword ? (
            // Tài khoản không có email — không còn chỗ nào để gửi thư (quyết định ghi đè
            // 06/08/2026), nên backend trả thẳng plaintext để đọc cho bệnh nhân nghe tại chỗ,
            // giống hệt cơ chế đã dùng ở luồng tạo tài khoản mới.
            <div className="mt-3 rounded-lg border border-dashed border-primary bg-primary/5 p-3">
              <div className="text-xs font-semibold uppercase text-muted-foreground">
                Mật khẩu tạm
              </div>
              <div className="mt-1 select-all break-all font-mono text-lg font-bold tracking-wider text-foreground">
                {revealedPassword}
              </div>
              <p className="mt-2 text-xs text-muted-foreground">
                Tài khoản này không có email — đọc mật khẩu trên cho bệnh nhân nghe hoặc ghi
                lại, sẽ không hiện lại được nữa.
              </p>
            </div>
          ) : null}

          {revealedPassword === null ? (
            <p className="mt-3 rounded-lg bg-emerald-50 p-3 text-sm text-emerald-700">
              Đã gửi mật khẩu tạm tới email của bệnh nhân.
            </p>
          ) : null}

          <div className="mt-4 flex justify-end gap-3">
            <button
              type="button"
              onClick={() => setConfirmingReset(false)}
              className="rounded-lg border border-border px-4 py-2 text-sm font-medium hover:bg-accent"
            >
              Đóng
            </button>
            <button
              type="button"
              onClick={() => resetMutation.mutate(undefined, { onSuccess: setRevealedPassword })}
              disabled={resetMutation.isPending || resetMutation.isSuccess}
              className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
            >
              Xác nhận
            </button>
          </div>
        </div>
      ) : null}

      {editing ? (
        // <div>, không phải <form>: khối này luôn render bên trong <form> của
        // PatientProfileForm (cha) — HTML không cho phép <form> lồng trong <form>, và React
        // tự log lỗi hydration nếu làm vậy. Lưu bằng onClick trên nút, không onSubmit.
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div>
            <label htmlFor="acc-fullName" className="mb-1.5 block text-sm font-medium">
              Họ và tên *
            </label>
            <input
              id="acc-fullName"
              value={formFullName}
              onChange={(event) => setFormFullName(event.target.value)}
              className="h-10 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
            />
          </div>

          <div>
            <label htmlFor="acc-phone" className="mb-1.5 block text-sm font-medium">
              Số điện thoại *
            </label>
            <input
              id="acc-phone"
              value={formPhone}
              onChange={(event) => setFormPhone(event.target.value)}
              className="h-10 w-full rounded-lg border border-border bg-background px-3 font-mono text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
            />
          </div>

          <div>
            <label htmlFor="acc-dob" className="mb-1.5 block text-sm font-medium">
              Ngày sinh
            </label>
            <input
              id="acc-dob"
              type="date"
              value={formDateOfBirth}
              onChange={(event) => setFormDateOfBirth(event.target.value)}
              className="h-10 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
            />
          </div>

          <div>
            <label htmlFor="acc-email" className="mb-1.5 block text-sm font-medium">
              Email
            </label>
            <input
              id="acc-email"
              type="email"
              value={formEmail}
              onChange={(event) => setFormEmail(event.target.value)}
              className="h-10 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
            />
          </div>

          {editError ? (
            <p
              className="rounded-lg bg-destructive/10 p-3 text-sm text-destructive sm:col-span-2"
              role="alert"
            >
              {editError}
            </p>
          ) : null}

          <div className="flex justify-end gap-3 sm:col-span-2">
            <button
              type="button"
              onClick={() => {
                setEditing(false);
                setClientError(null);
              }}
              className="rounded-lg border border-border px-4 py-2 text-sm font-medium hover:bg-accent"
            >
              Huỷ
            </button>
            <button
              type="button"
              onClick={handleSave}
              disabled={updateMutation.isPending}
              className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
            >
              Lưu
            </button>
          </div>
        </div>
      ) : null}
    </section>
  );
}
