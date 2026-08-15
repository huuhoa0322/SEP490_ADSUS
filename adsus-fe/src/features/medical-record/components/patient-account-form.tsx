"use client";

import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";

import { getApiErrorMessage } from "@/lib/api-client";

import { useCreatePatientAccount } from "../hooks/use-patient-account";
import { useCreatePatientProfile } from "../hooks/use-patient-profile";
import { genderLabel } from "../lib/medical-record-labels";
import type { Gender, PatientAccountCreated } from "../types/medical-record.types";

/** Khớp validator phía backend: 10 chữ số, bắt đầu bằng 0. */
const PHONE_PATTERN = /^0\d{9}$/;

const GENDERS: Gender[] = ["FEMALE", "MALE", "OTHER"];

/**
 * UC-06 AF-01 gộp #17 — Điều dưỡng tạo tài khoản Bệnh nhân MỚI kèm luôn hồ sơ nền, trong MỘT
 * form (quyết định ghi đè 06/08/2026). Trước đó là hai bước tách rời (tạo tài khoản rồi
 * chuyển màn tạo hồ sơ nền); giờ gộp lại để Điều dưỡng không phải nhập lại và không rời màn
 * giữa chừng.
 *
 * Thứ tự gọi API: tạo tài khoản trước (#28), thành công thì tạo hồ sơ nền ngay (#17) với
 * patientUserId vừa nhận — KHÔNG phải một endpoint gộp cả hai (backend chưa có), nên có khả
 * năng (hiếm) tài khoản tạo xong nhưng hồ sơ nền lỗi. Xử lý bằng cách vẫn hiện mật khẩu tạm
 * (dữ liệu quý, chỉ hiện được một lần) kèm cảnh báo riêng, và nút Tiếp tục vẫn dùng được —
 * hồ sơ nền tạo sau qua "Tạo hồ sơ nền" ở danh sách bệnh nhân, giống hệt luồng tài khoản có
 * sẵn nhưng chưa có hồ sơ.
 *
 * Ba trường hồ sơ nền (Giới tính/Dị ứng/Tiền sử) dùng đúng luật của #17: gender BỎ TRỐNG ĐƯỢC
 * (DB có default) — khác #18 (sửa hồ sơ đã có) vốn bắt buộc. Xem `patient-profile-form.tsx`.
 *
 * Sửa hồ sơ nền SAU KHI đã tạo thì đi qua trang hồ sơ nền riêng
 * (`/patients/[profileId]/profile`, Task C9) — form này chỉ lo lúc tạo mới.
 *
 * CHỈ Điều dưỡng. Component cha (`/patients/new`) đã chặn theo vai trò; ở đây không kiểm lại.
 */
export function PatientAccountForm() {
  const router = useRouter();

  const [fullName, setFullName] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [dateOfBirth, setDateOfBirth] = useState("");
  const [email, setEmail] = useState("");
  const [gender, setGender] = useState<Gender>("FEMALE");
  const [medicalHistory, setMedicalHistory] = useState("");
  const [allergies, setAllergies] = useState("");
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

          // #17 — gender optional, khác #18. Gọi ngay với userId vừa nhận, không bắt Điều
          // dưỡng nhập lại gì.
          profileMutation.mutate(
            {
              patientUserId: account.userId,
              gender: gender || null,
              medicalHistory: medicalHistory.trim() || null,
              allergies: allergies.trim() || null,
            },
            { onSuccess: (profile) => setCreatedProfileId(profile.patientProfileId) },
          );
        },
      },
    );
  }

  // Chặng 2 — tài khoản đã tạo (hồ sơ nền có thể vẫn đang tạo hoặc đã lỗi). Mật khẩu chỉ hiện
  // được đúng lần này, không có ô nhập nào cho nó.
  if (createdAccount) {
    return (
      <div className="rounded-xl border border-border p-5">
        <h2 className="font-heading text-lg font-semibold text-foreground">
          Đã tạo tài khoản cho {createdAccount.fullName}
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Đọc mật khẩu tạm dưới đây cho bệnh nhân nghe hoặc ghi lại — mật khẩu chỉ hiện được
          đúng một lần ở đây, sẽ không hiện lại được nữa. Bệnh nhân bắt buộc đổi mật khẩu ngay
          khi đăng nhập lần đầu.
        </p>

        <div className="mt-4 rounded-lg border border-dashed border-primary bg-primary/5 p-4">
          <div className="text-xs font-semibold uppercase text-muted-foreground">
            Mật khẩu tạm
          </div>
          <div className="mt-1 select-all break-all font-mono text-xl font-bold tracking-wider text-foreground">
            {createdAccount.temporaryPassword}
          </div>
        </div>

        {profileMutation.isPending ? (
          <p className="mt-4 text-sm text-muted-foreground">Đang tạo hồ sơ nền...</p>
        ) : null}

        {profileErrorMessage ? (
          <p className="mt-4 rounded-lg bg-destructive/10 p-3 text-sm text-destructive" role="alert">
            Tài khoản đã tạo thành công, nhưng chưa tạo được hồ sơ nền: {profileErrorMessage} Vào
            lại danh sách bệnh nhân và bấm &quot;Tạo hồ sơ nền&quot; cho {createdAccount.fullName}
            {" "}để thử lại.
          </p>
        ) : null}

        <div className="mt-5 flex justify-end">
          <button
            type="button"
            disabled={profileMutation.isPending}
            onClick={() =>
              router.push(createdProfileId ? `/patients/${createdProfileId}` : "/patients")
            }
            className="rounded-lg bg-primary px-5 py-2 text-sm font-semibold text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
          >
            Đã đọc cho bệnh nhân — Tiếp tục
          </button>
        </div>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="rounded-xl border border-border p-5">
      <h2 className="font-heading text-lg font-semibold text-foreground">
        Tạo tài khoản bệnh nhân mới
      </h2>
      {/* Quyết định ghi đè 06/08/2026 — mật khẩu tạm hiện ngay trên màn hình sau khi tạo,
          không còn gửi qua email nữa. */}
      <p className="mt-1 text-sm text-muted-foreground">
        Hệ thống sinh mật khẩu tạm và hiện ngay sau khi tạo để đọc cho bệnh nhân; bệnh nhân
        buộc đổi ở lần đăng nhập đầu.
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
            placeholder="Dùng khi bệnh nhân quên mật khẩu sau này"
            className="h-10 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
          />
        </div>
      </div>

      {/* Hồ sơ nền (#17) — gộp ngay trong form này (quyết định ghi đè 06/08/2026), gender bỏ
          trống được vì đây là #17 chứ không phải #18. */}
      <div className="mt-6 space-y-5 border-t border-border pt-5">
        <h3 className="text-xs font-semibold uppercase text-muted-foreground">Hồ sơ nền</h3>



        <div>
          <label htmlFor="allergies" className="mb-1.5 block text-sm font-medium">
            Dị ứng đã biết
          </label>
          <input
            id="allergies"
            value={allergies}
            onChange={(event) => setAllergies(event.target.value)}
            placeholder="Không có / liệt kê dị ứng..."
            className="h-10 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
          />
        </div>

        <div>
          <label htmlFor="medicalHistory" className="mb-1.5 block text-sm font-medium">
            Tiền sử bệnh
          </label>
          <textarea
            id="medicalHistory"
            value={medicalHistory}
            onChange={(event) => setMedicalHistory(event.target.value)}
            rows={4}
            placeholder="Không có / liệt kê bệnh mãn tính..."
            className="w-full rounded-lg border border-border bg-background p-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
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
          disabled={accountMutation.isPending || accountMutation.isSuccess}
          className="rounded-lg bg-primary px-5 py-2 text-sm font-semibold text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
        >
          Tạo bệnh nhân mới
        </button>
      </div>
    </form>
  );
}
