"use client";

import { useState } from "react";

import { getApiErrorMessage } from "@/lib/api-client";
import { useAuthStore } from "@/store/auth-store";

import {
  usePatientAccount,
  useResetPatientAccountPassword,
  useUpdatePatientAccountContact,
} from "../hooks/use-patient-account";

const PHONE_PATTERN = /^0\d{9}$/;

interface Props {
  userId: string;
  fullName: string;
  phone: string;
  dateOfBirth: string | null;
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
 * AF-03 (cấp lại mật khẩu) sửa lại 06/08/2026, mở rộng lần 2 (sau Task C11-ext, sau Task
 * C10-ext đổi luồng tạo tài khoản #28): không còn phân biệt có/không có email nữa — LUÔN hiện
 * mật khẩu tạm ngay tại đây, KHÔNG BAO GIỜ gửi email ở đường này nữa.
 *
 * email KHÔNG nhận qua prop (sửa 06/08/2026, review Task C11) — PatientProfile (#19) không có
 * trường này, nên tự gọi `usePatientAccount` để lấy giá trị thật. Nhận `email: null` cứng từ
 * nơi gọi từng khiến "Sửa thông tin tài khoản" luôn khởi tạo ô email rỗng, và vì
 * UpdateContactAsync thay TOÀN BỘ 4 trường (BR-04), bấm Lưu là xoá mất email đang có.
 */
export function PatientAccountActions({ userId, fullName, phone, dateOfBirth }: Props) {
  const isNurse = useAuthStore((state) => state.user?.role) === "NURSE";

  const [editing, setEditing] = useState(false);
  const [confirmingReset, setConfirmingReset] = useState(false);
  // undefined = chưa cấp lại lần nào; chuỗi = vừa cấp lại xong, hiện plaintext. Không còn
  // nhánh "đã gửi email" (mở rộng lần 2, 06/08/2026) nên không còn rủi ro bail-out của React
  // từng buộc phải dùng undefined làm sentinel — giữ lại kiểu này chỉ vì vẫn cần phân biệt
  // "chưa cấp lại" khỏi "cấp lại rồi".
  const [revealedPassword, setRevealedPassword] = useState<string | undefined>(undefined);

  const [formFullName, setFormFullName] = useState(fullName);
  const [formPhone, setFormPhone] = useState(phone);
  const [formDateOfBirth, setFormDateOfBirth] = useState(dateOfBirth ?? "");
  const [formEmail, setFormEmail] = useState("");
  const [clientError, setClientError] = useState<string | null>(null);

  const accountQuery = usePatientAccount(userId, isNurse);
  const updateMutation = useUpdatePatientAccountContact(userId);
  const resetMutation = useResetPatientAccountPassword(userId);

  // Đồng bộ email thật vào form ngay khi tải xong, theo mẫu "đồng bộ state khi prop đổi" của
  // React (giống patient-profile-form.tsx) — không dùng useEffect vì sẽ có một nhịp hiển thị
  // rỗng trước khi effect chạy, và Điều dưỡng bấm Lưu đúng lúc đó là xoá mất email thật.
  const [syncedAccountUserId, setSyncedAccountUserId] = useState<string | null>(null);
  if (accountQuery.data && accountQuery.data.userId !== syncedAccountUserId) {
    setSyncedAccountUserId(accountQuery.data.userId);
    setFormEmail(accountQuery.data.email ?? "");
  }

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
            // Chờ email thật nạp xong mới cho sửa — mở sớm hơn là có khả năng (dù nhỏ) bấm Lưu
            // trước khi formEmail kịp đồng bộ, xoá mất email đang có.
            disabled={accountQuery.isLoading}
            className="rounded-lg border border-border px-4 py-2 text-sm font-medium hover:bg-accent disabled:opacity-50"
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
            được nữa; mật khẩu tạm mới sẽ hiện ngay tại đây để đọc cho bệnh nhân nghe hoặc ghi
            lại — không gửi qua email.
          </p>

          {resetMutation.isError ? (
            <p className="mt-3 rounded-lg bg-destructive/10 p-3 text-sm text-destructive" role="alert">
              {getApiErrorMessage(resetMutation.error, "Cấp lại mật khẩu thất bại.")}
            </p>
          ) : null}

          {revealedPassword ? (
            <div className="mt-3 rounded-lg border border-dashed border-primary bg-primary/5 p-3">
              <div className="text-xs font-semibold uppercase text-muted-foreground">
                Mật khẩu tạm
              </div>
              <div className="mt-1 select-all break-all font-mono text-lg font-bold tracking-wider text-foreground">
                {revealedPassword}
              </div>
              <p className="mt-2 text-xs text-muted-foreground">
                Đọc mật khẩu trên cho bệnh nhân nghe hoặc ghi lại — sẽ không hiện lại được nữa.
              </p>
            </div>
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
