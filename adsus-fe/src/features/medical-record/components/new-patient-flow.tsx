"use client";

import { useRouter } from "next/navigation";

import { getApiErrorMessage } from "@/lib/api-client";
import { useAuthStore } from "@/store/auth-store";

import { usePatientList } from "../hooks/use-patients";

import { PatientAccountForm } from "./patient-account-form";
import { PatientProfileForm } from "./patient-profile-form";

interface Props {
  /** Có nghĩa là tài khoản đã tồn tại — chỉ còn thiếu hồ sơ nền. */
  patientUserId?: string;
}

/**
 * Điều phối hai luồng tạo bệnh nhân.
 *
 * Vào từ SCR-09:
 *   nút "Tạo hồ sơ nền" trên dòng chưa có hồ sơ  → kèm patientUserId, chỉ còn thiếu hồ sơ nền
 *   nút "+ Thêm bệnh nhân mới" (chỉ Điều dưỡng)  → không kèm gì, PatientAccountForm tự lo cả
 *     tài khoản lẫn hồ sơ nền trong một form (quyết định ghi đè 06/08/2026)
 */
export function NewPatientFlow({ patientUserId }: Props) {
  const router = useRouter();
  const isNurse = useAuthStore((state) => state.user?.role) === "NURSE";

  // Chỉ nạp khi cần đọc thông tin định danh của một tài khoản đã có.
  const accountsQuery = usePatientList({ hasProfile: false, pageSize: 100 });

  // Luồng A — tài khoản đã có sẵn, chỉ còn thiếu hồ sơ nền.
  if (patientUserId) {
    if (accountsQuery.isLoading) {
      return <p className="p-10 text-sm text-muted-foreground">Đang tải thông tin tài khoản...</p>;
    }

    if (accountsQuery.isError) {
      return (
        <p className="m-10 rounded-lg bg-destructive/10 p-4 text-sm text-destructive" role="alert">
          {getApiErrorMessage(accountsQuery.error, "Không tải được thông tin tài khoản.")}
        </p>
      );
    }

    const account = accountsQuery.data?.items.find((item) => item.patientUserId === patientUserId);

    if (!account) {
      // Hết hạn giữa chừng: người khác vừa lập hồ sơ nền cho bệnh nhân này ở tab khác.
      return (
        <div className="m-10 rounded-xl border border-dashed border-border p-8 text-center">
          <p className="font-semibold text-foreground">Tài khoản này đã có hồ sơ nền</p>
          <p className="mt-1 text-sm text-muted-foreground">
            Có thể ai đó vừa lập hồ sơ. Quay lại danh sách để mở hồ sơ hiện có.
          </p>
          <button
            type="button"
            onClick={() => router.push("/patients")}
            className="mt-4 rounded-lg border border-border px-4 py-2 text-sm font-medium hover:bg-accent"
          >
            Về danh sách bệnh nhân
          </button>
        </div>
      );
    }

    return (
      <PatientProfileForm
        mode="create"
        patientUserId={account.patientUserId}
        identity={{ fullName: account.fullName, phone: account.phone, dateOfBirth: null }}
      />
    );
  }

  // Luồng B — chưa có tài khoản. UC-06 BR-03: chỉ Điều dưỡng.
  if (!isNurse) {
    return (
      <div className="m-10 rounded-xl border border-dashed border-border p-8 text-center">
        <p className="font-semibold text-foreground">Chỉ Điều dưỡng tạo được tài khoản bệnh nhân</p>
        <p className="mt-1 text-sm text-muted-foreground">
          Bệnh nhân chưa có tài khoản thì đề nghị Quản trị viên tạo trước (UC-04), hoặc nhờ
          Điều dưỡng thực hiện.
        </p>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-3xl px-6 py-10">
      <h1 className="mb-6 font-heading text-[28px] font-bold tracking-[-0.02em] text-foreground">
        Thêm bệnh nhân mới
      </h1>
      <PatientAccountForm />
    </div>
  );
}
