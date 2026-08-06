"use client";

import { useState, type FormEvent } from "react";

import { getApiErrorMessage } from "@/lib/api-client";

import { useCreatePatientAccount } from "../hooks/use-patient-account";
import type { PatientAccount } from "../types/medical-record.types";

/** Khớp validator phía backend: 10 chữ số, bắt đầu bằng 0. */
const PHONE_PATTERN = /^0\d{9}$/;

interface Props {
  onCreated: (account: PatientAccount) => void;
}

/**
 * UC-06 AF-01 — Điều dưỡng tạo tài khoản Bệnh nhân mới (quyết định ghi đè 04/08/2026).
 *
 * CHỈ Điều dưỡng. Component cha (`/patients/new`) đã chặn theo vai trò; ở đây không kiểm lại
 * để tránh hai chỗ cùng quyết định một việc.
 */
export function PatientAccountForm({ onCreated }: Props) {
  const [fullName, setFullName] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [dateOfBirth, setDateOfBirth] = useState("");
  const [email, setEmail] = useState("");
  const [clientError, setClientError] = useState<string | null>(null);

  const mutation = useCreatePatientAccount();

  const errorMessage =
    clientError ??
    (mutation.isError ? getApiErrorMessage(mutation.error, "Tạo tài khoản thất bại.") : null);

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setClientError(null);

    if (!fullName.trim()) {
      setClientError("Vui lòng nhập họ và tên.");
      return;
    }

    if (!PHONE_PATTERN.test(phoneNumber.trim())) {
      setClientError("Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.");
      return;
    }

    mutation.mutate(
      {
        phoneNumber: phoneNumber.trim(),
        fullName: fullName.trim(),
        // Gửi null chứ không phải chuỗi rỗng: validator backend sẽ coi "" là email sai định dạng.
        dateOfBirth: dateOfBirth || null,
        email: email.trim() || null,
      },
      { onSuccess: onCreated },
    );
  }

  return (
    <form onSubmit={handleSubmit} className="rounded-xl border border-border p-5">
      <h2 className="font-heading text-lg font-semibold text-foreground">
        Tạo tài khoản bệnh nhân mới
      </h2>
      {/* UC-06 BR-05 — nói rõ ngay trên form, để Điều dưỡng không đi tìm mật khẩu sau khi bấm tạo. */}
      <p className="mt-1 text-sm text-muted-foreground">
        Hệ thống sinh mật khẩu tạm và gửi qua email; bệnh nhân buộc đổi ở lần đăng nhập đầu.
        Điều dưỡng không bao giờ thấy mật khẩu này.
      </p>

      <div className="mt-5 grid grid-cols-1 gap-5 sm:grid-cols-2">
        <div>
          <label htmlFor="fullName" className="mb-1.5 block text-sm font-medium">
            Họ và tên *
          </label>
          <input
            id="fullName"
            value={fullName}
            onChange={(event) => setFullName(event.target.value)}
            className="h-10 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
          />
        </div>

        <div>
          <label htmlFor="phoneNumber" className="mb-1.5 block text-sm font-medium">
            Số điện thoại *
          </label>
          <input
            id="phoneNumber"
            value={phoneNumber}
            onChange={(event) => setPhoneNumber(event.target.value)}
            placeholder="0xxxxxxxxx"
            className="h-10 w-full rounded-lg border border-border bg-background px-3 font-mono text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
          />
        </div>

        <div>
          <label htmlFor="dateOfBirth" className="mb-1.5 block text-sm font-medium">
            Ngày sinh
          </label>
          <input
            id="dateOfBirth"
            type="date"
            value={dateOfBirth}
            onChange={(event) => setDateOfBirth(event.target.value)}
            className="h-10 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
          />
        </div>

        <div>
          <label htmlFor="email" className="mb-1.5 block text-sm font-medium">
            Email
          </label>
          <input
            id="email"
            type="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            placeholder="Để nhận mật khẩu tạm"
            className="h-10 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
          />
        </div>
      </div>

      {errorMessage ? (
        <p className="mt-4 rounded-lg bg-destructive/10 p-3 text-sm text-destructive" role="alert">
          {errorMessage}
        </p>
      ) : null}

      <div className="mt-5 flex justify-end">
        <button
          type="submit"
          disabled={mutation.isPending || mutation.isSuccess}
          className="rounded-lg bg-primary px-5 py-2 text-sm font-semibold text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
        >
          Tạo tài khoản
        </button>
      </div>
    </form>
  );
}
