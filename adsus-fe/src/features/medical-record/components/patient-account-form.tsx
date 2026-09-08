"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import {
  AlertCircle,
  ArrowLeft,
  CheckCircle2,
  FileText,
  KeyRound,
  User,
  UserPlus,
} from "lucide-react";

import { DatePicker } from "@/components/ui/date-picker";
import { getApiErrorMessage } from "@/lib/api-client";

import { useCreatePatientAccount } from "../hooks/use-patient-account";
import { useCreatePatientProfile } from "../hooks/use-patient-profile";
import type {
  PatientAccountCreated,
  PatientAllergyInput,
  PatientDiseaseInput,
} from "../types/medical-record.types";
import { AllergySelector } from "./allergy-selector";
import { MedicalHistorySelector } from "./medical-history-selector";

/** Khớp validator phía backend: 10 chữ số, bắt đầu bằng 0. */
const PHONE_PATTERN = /^0\d{9}$/;

/**
 * UC-06 AF-01 gộp #17 — Điều dưỡng tạo tài khoản Bệnh nhân MỚI kèm luôn hồ sơ nền (Preclinic create-patient.html card).
 *
 * Cấu trúc:
 * - Thẻ Card chuẩn Preclinic với viền #E7E8EB, bo góc 5-8px, phân khu rõ ràng.
 * - Khu vực 1: Thông tin cá nhân & liên hệ (Họ tên, SĐT, Ngày sinh, Email).
 * - Khu vực 2: Hồ sơ y tế nền tảng (Tiền sử bệnh, Dị ứng).
 * - Màn hình xác nhận: Hộp thông tin bảo mật mật khẩu tạm (Preclinic Credentials Box).
 */
export function PatientAccountForm() {
  const router = useRouter();

  const [fullName, setFullName] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [dateOfBirth, setDateOfBirth] = useState("");
  const [email, setEmail] = useState("");
  const [diseases, setDiseases] = useState<PatientDiseaseInput[]>([]);
  const [allergies, setAllergies] = useState<PatientAllergyInput[]>([]);
  const [clientError, setClientError] = useState<string | null>(null);
  const [createdAccount, setCreatedAccount] = useState<PatientAccountCreated | null>(null);
  const [createdProfileId, setCreatedProfileId] = useState<string | null>(null);

  const accountMutation = useCreatePatientAccount();
  const profileMutation = useCreatePatientProfile();

  const errorMessage =
    clientError ??
    (accountMutation.isError
      ? getApiErrorMessage(accountMutation.error, "Tạo tài khoản thất bại.")
      : null);

  const profileErrorMessage = profileMutation.isError
    ? getApiErrorMessage(profileMutation.error, "Tạo hồ sơ nền thất bại.")
    : null;

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

    const validDiseases = diseases.filter((d) => d.note === null || d.note.trim() !== "");
    const validAllergies = allergies.filter((a) => a.note === null || a.note.trim() !== "");

    accountMutation.mutate(
      {
        phoneNumber: phoneNumber.trim(),
        fullName: fullName.trim(),
        // Gửi null chứ không phải chuỗi rỗng: validator backend sẽ coi "" là email sai định dạng.
        dateOfBirth: dateOfBirth || null,
        email: email.trim() || null,
      },
      {
        onSuccess: (account) => {
          setCreatedAccount(account);

          // #17 — gender optional, khác #18. Gọi ngay với userId vừa nhận.
          profileMutation.mutate(
            {
              patientUserId: account.userId,
              gender: "FEMALE", // Luôn là Nữ cho hệ thống sản phụ khoa
              diseases: validDiseases,
              allergies: validAllergies,
            },
            { onSuccess: (profile) => setCreatedProfileId(profile.patientProfileId) },
          );
        },
      },
    );
  }

  // Chặng 2 — Tài khoản đã tạo xong. Hộp thông tin mật khẩu tạm chuẩn Preclinic.
  if (createdAccount) {
    return (
      <div className="mx-auto w-full max-w-2xl rounded-lg border border-[#E7E8EB] bg-white p-6 shadow-xs">
        <div className="flex items-center gap-3 border-b border-[#E7E8EB] pb-4">
          <div className="flex size-10 items-center justify-center rounded-full bg-[#F4FBF7] text-[#27AE60]">
            <CheckCircle2 className="size-6" />
          </div>
          <div>
            <h2 className="font-heading text-lg font-bold text-[#0A1B39]">
              Đã tạo tài khoản cho {createdAccount.fullName}
            </h2>
            <p className="text-xs text-[#6C7688]">
              Tài khoản bệnh nhân đã sẵn sàng để truy cập hệ thống
            </p>
          </div>
        </div>

        <p className="mt-4 text-sm text-[#6C7688]">
          Đọc mật khẩu tạm dưới đây cho bệnh nhân nghe hoặc ghi lại — mật khẩu chỉ hiện được
          đúng một lần ở đây, sẽ không hiện lại được nữa. Bệnh nhân bắt buộc đổi mật khẩu ngay
          khi đăng nhập lần đầu.
        </p>

        {/* Credentials Box */}
        <div className="mt-5 rounded-lg border-2 border-dashed border-[#2E37A4]/30 bg-[#ECEDF7]/50 p-5 text-center">
          <div className="flex items-center justify-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-[#6C7688]">
            <KeyRound className="size-4 text-[#2E37A4]" />
            Mật khẩu tạm
          </div>
          <div className="mt-2 select-all break-all font-mono text-2xl font-bold tracking-widest text-[#2E37A4]">
            {createdAccount.temporaryPassword}
          </div>
          <p className="mt-1 text-xs text-muted-foreground">
            (Nhấp đúp chuột để bôi đen toàn bộ mật khẩu)
          </p>
        </div>

        {profileMutation.isPending ? (
          <div className="mt-4 flex items-center justify-center gap-2 text-sm text-[#6C7688]">
            <span className="size-2 animate-ping rounded-full bg-[#2E37A4]" />
            Đang tạo hồ sơ nền...
          </div>
        ) : null}

        {profileErrorMessage ? (
          <div
            className="mt-4 flex items-start gap-2.5 rounded-md bg-destructive/10 p-3.5 text-sm text-destructive"
            role="alert"
          >
            <AlertCircle className="mt-0.5 size-4 shrink-0" />
            <div>
              Tài khoản đã tạo thành công, nhưng chưa tạo được hồ sơ nền: {profileErrorMessage} Vào
              lại danh sách bệnh nhân và bấm &quot;Tạo hồ sơ nền&quot; cho {createdAccount.fullName}
              {" "}để thử lại.
            </div>
          </div>
        ) : null}

        <div className="mt-6 flex justify-end border-t border-[#E7E8EB] pt-4">
          <button
            type="button"
            disabled={profileMutation.isPending}
            onClick={() =>
              router.push(createdProfileId ? `/patients/${createdProfileId}` : "/patients")
            }
            className="rounded-md bg-[#2E37A4] px-5 py-2.5 text-sm font-semibold text-white shadow-xs transition-colors hover:bg-[#2E37A4]/90 disabled:opacity-50"
          >
            Đã đọc cho bệnh nhân — Tiếp tục
          </button>
        </div>
      </div>
    );
  }

  // Chặng 1 — Form đăng ký bệnh nhân mới (Preclinic create-patient card)
  return (
    <div className="mx-auto w-full max-w-4xl">
      <div className="mb-4">
        <Link
          href="/patients"
          className="inline-flex items-center gap-1.5 text-xs font-semibold text-[#6C7688] transition-colors hover:text-[#2E37A4]"
        >
          <ArrowLeft className="size-3.5" />
          Quay lại danh sách bệnh nhân
        </Link>
      </div>

      <form
        onSubmit={handleSubmit}
        className="overflow-hidden rounded-lg border border-[#E7E8EB] bg-white shadow-xs"
      >
        {/* Card Header */}
        <div className="border-b border-[#E7E8EB] bg-white p-6">
          <div className="flex items-center gap-3">
            <div className="flex size-10 items-center justify-center rounded-full bg-[#ECEDF7] text-[#2E37A4]">
              <UserPlus className="size-5" />
            </div>
            <div>
              <h2 className="font-heading text-xl font-bold text-[#0A1B39]">
                Tạo tài khoản bệnh nhân mới
              </h2>
              <p className="mt-0.5 text-xs text-[#6C7688]">
                Hệ thống sinh mật khẩu tạm và hiện ngay sau khi tạo để đọc cho bệnh nhân; bệnh nhân
                buộc đổi ở lần đăng nhập đầu.
              </p>
            </div>
          </div>
        </div>

        <div className="space-y-6 p-6">
          {/* Section 1: Thông tin cá nhân & Liên hệ */}
          <div className="rounded-lg border border-[#E7E8EB] bg-white p-5 shadow-2xs">
            <div className="mb-4 flex items-center gap-2 border-b border-[#E7E8EB] pb-3">
              <User className="size-4 text-[#2E37A4]" />
              <h3 className="font-heading text-sm font-bold uppercase tracking-wider text-[#0A1B39]">
                Thông tin cá nhân & Liên hệ
              </h3>
            </div>

            <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
              <div>
                <label htmlFor="fullName" className="mb-1.5 block text-xs font-semibold text-[#0A1B39]">
                  Họ và tên <span className="text-destructive">*</span>
                </label>
                <input
                  id="fullName"
                  value={fullName}
                  onChange={(event) => setFullName(event.target.value)}
                  placeholder="Ví dụ: Nguyễn Thị Hoa"
                  className="h-10 w-full rounded-md border border-[#E7E8EB] bg-background px-3 text-sm outline-none transition-colors focus:border-[#2E37A4] focus-visible:ring-2 focus-visible:ring-[#2E37A4]/20"
                />
              </div>

              <div>
                <label htmlFor="phoneNumber" className="mb-1.5 block text-xs font-semibold text-[#0A1B39]">
                  Số điện thoại <span className="text-destructive">*</span>
                </label>
                <input
                  id="phoneNumber"
                  value={phoneNumber}
                  onChange={(event) => setPhoneNumber(event.target.value)}
                  placeholder="0xxxxxxxxx"
                  className="h-10 w-full rounded-md border border-[#E7E8EB] bg-background px-3 font-mono text-sm outline-none transition-colors focus:border-[#2E37A4] focus-visible:ring-2 focus-visible:ring-[#2E37A4]/20"
                />
              </div>

              <div>
                <label htmlFor="dateOfBirth" className="mb-1.5 block text-xs font-semibold text-[#0A1B39]">
                  Ngày sinh
                </label>
                <DatePicker
                  value={dateOfBirth}
                  onChange={(val) => setDateOfBirth(val)}
                  className="h-10 w-full rounded-md border border-[#E7E8EB] bg-background px-3 text-sm outline-none transition-colors focus:border-[#2E37A4] focus-visible:ring-2 focus-visible:ring-[#2E37A4]/20"
                />
              </div>

              <div>
                <label htmlFor="email" className="mb-1.5 block text-xs font-semibold text-[#0A1B39]">
                  Email
                </label>
                <input
                  id="email"
                  type="email"
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                  placeholder="Dùng khi bệnh nhân quên mật khẩu sau này"
                  className="h-10 w-full rounded-md border border-[#E7E8EB] bg-background px-3 text-sm outline-none transition-colors focus:border-[#2E37A4] focus-visible:ring-2 focus-visible:ring-[#2E37A4]/20"
                />
              </div>
            </div>
          </div>

          {/* Section 2: Hồ sơ y tế nền tảng */}
          <div className="rounded-lg border border-[#E7E8EB] bg-white p-5 shadow-2xs">
            <div className="mb-4 flex items-center gap-2 border-b border-[#E7E8EB] pb-3">
              <FileText className="size-4 text-[#2E37A4]" />
              <h3 className="font-heading text-sm font-bold uppercase tracking-wider text-[#0A1B39]">
                Hồ sơ y tế nền tảng
              </h3>
            </div>

            <div className="space-y-5">
              <fieldset className="m-0 border-0 p-0">
                <legend className="mb-2 block text-xs font-semibold text-[#0A1B39]">
                  Tiền sử bệnh
                </legend>
                <MedicalHistorySelector value={diseases} onChange={setDiseases} />
              </fieldset>

              <fieldset className="m-0 border-0 p-0">
                <legend className="mb-2 block text-xs font-semibold text-[#0A1B39]">
                  Dị ứng đã biết
                </legend>
                <AllergySelector value={allergies} onChange={setAllergies} />
              </fieldset>
            </div>
          </div>
        </div>

        {errorMessage ? (
          <div className="px-6 pb-2">
            <div
              className="flex items-center gap-2 rounded-md bg-destructive/10 p-3 text-sm text-destructive"
              role="alert"
            >
              <AlertCircle className="size-4 shrink-0" />
              <span>{errorMessage}</span>
            </div>
          </div>
        ) : null}

        {/* Card Footer */}
        <div className="flex items-center justify-end gap-3 border-t border-[#E7E8EB] bg-[#F8F9FA] px-6 py-4">
          <button
            type="button"
            onClick={() => router.back()}
            className="rounded-md border border-[#E7E8EB] bg-white px-4 py-2 text-sm font-medium text-[#0A1B39] shadow-2xs transition-colors hover:bg-muted"
          >
            Huỷ bỏ
          </button>
          <button
            type="submit"
            disabled={accountMutation.isPending || accountMutation.isSuccess}
            className="rounded-md bg-[#2E37A4] px-5 py-2 text-sm font-semibold text-white shadow-xs transition-colors hover:bg-[#2E37A4]/90 disabled:opacity-50"
          >
            {accountMutation.isPending ? "Đang xử lý..." : "Tạo bệnh nhân mới"}
          </button>
        </div>
      </form>
    </div>
  );
}
