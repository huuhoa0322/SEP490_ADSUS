"use client";

import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";

import { getApiErrorMessage } from "@/lib/api-client";

import { PatientAccountActions } from "./patient-account-actions";
import {
  useCreatePatientProfile,
  usePatientProfile,
  useUpdatePatientProfile,
} from "../hooks/use-patient-profile";
import { EMPTY_VALUE, formatIsoDate, genderLabel } from "../lib/medical-record-labels";
import type { Gender } from "../types/medical-record.types";

const GENDERS: Gender[] = ["FEMALE", "MALE", "OTHER"];

interface Identity {
  fullName: string;
  phone: string;
  dateOfBirth: string | null;
}

type Props =
  | { mode: "edit"; profileId: string }
  | { mode: "create"; patientUserId: string; identity: Identity };

/**
 * SCR-10 — hồ sơ y tế nền (UC-06).
 *
 * Dùng chung cho hai chế độ:
 *   edit   — #19 nạp, #18 lưu. gender BẮT BUỘC vì #18 thay toàn bộ hồ sơ.
 *   create — #17 lưu. gender BỎ TRỐNG ĐƯỢC (DB có default).
 *
 * Hai luật validate khác nhau nên KHÔNG dùng chung một hàm kiểm — đó là bẫy dễ mắc nhất ở
 * màn này.
 */
export function PatientProfileForm(props: Props) {
  const router = useRouter();

  const profileQuery = usePatientProfile(props.mode === "edit" ? props.profileId : undefined);
  const updateMutation = useUpdatePatientProfile(props.mode === "edit" ? props.profileId : "");
  const createMutation = useCreatePatientProfile();

  const loaded = profileQuery.data;

  const [gender, setGender] = useState<Gender | "">("");
  const [medicalHistory, setMedicalHistory] = useState("");
  const [allergies, setAllergies] = useState("");
  const [clientError, setClientError] = useState<string | null>(null);

  // Đổ dữ liệu vừa nạp vào form MỘT LẦN, ngay trong lúc render.
  //
  // useState(loaded?.gender ?? "") không dùng được: lần render đầu chạy trước khi query
  // resolve, nên giá trị khởi tạo luôn là rỗng và không bao giờ được cập nhật — form sẽ
  // hiện trống dù hồ sơ có dữ liệu, và bấm Lưu là ghi đè sạch tiền sử bệnh của bệnh nhân
  // (#18 thay TOÀN BỘ hồ sơ).
  //
  // Dùng mẫu "đồng bộ state khi prop đổi" của React thay vì useEffect: cách này chạy ngay
  // trong render nên không có một nhịp hiển thị giá trị sai ở giữa.
  const [syncedProfileId, setSyncedProfileId] = useState<string | null>(null);
  if (loaded && loaded.patientProfileId !== syncedProfileId) {
    setSyncedProfileId(loaded.patientProfileId);
    setGender(loaded.gender);
    setMedicalHistory(loaded.medicalHistory ?? "");
    setAllergies(loaded.allergies ?? "");
  }

  const identity: Identity | undefined =
    props.mode === "create"
      ? props.identity
      : loaded
        ? { fullName: loaded.fullName, phone: loaded.phone, dateOfBirth: loaded.dateOfBirth }
        : undefined;

  const mutation = props.mode === "edit" ? updateMutation : createMutation;

  const errorMessage =
    clientError ??
    (mutation.isError ? getApiErrorMessage(mutation.error, "Lưu hồ sơ nền thất bại.") : null);

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setClientError(null);

    if (props.mode === "edit") {
      // #18 thay toàn bộ hồ sơ nên phải gửi lại cả giá trị không đổi — bỏ trống là xoá dữ liệu.
      if (!gender) {
        setClientError("Vui lòng chọn giới tính.");
        return;
      }

      updateMutation.mutate(
        {
          gender,
          medicalHistory: medicalHistory.trim() || null,
          allergies: allergies.trim() || null,
        },
        { onSuccess: () => router.push(`/patients/${props.profileId}`) },
      );
      return;
    }

    // #17 — gender optional.
    createMutation.mutate(
      {
        patientUserId: props.patientUserId,
        gender: gender || null,
        medicalHistory: medicalHistory.trim() || null,
        allergies: allergies.trim() || null,
      },
      { onSuccess: (created) => router.push(`/patients/${created.patientProfileId}`) },
    );
  }

  if (props.mode === "edit" && profileQuery.isLoading) {
    return <p className="p-10 text-sm text-muted-foreground">Đang tải hồ sơ nền...</p>;
  }

  if (props.mode === "edit" && profileQuery.isError) {
    return (
      <p className="m-10 rounded-lg bg-destructive/10 p-4 text-sm text-destructive" role="alert">
        {getApiErrorMessage(profileQuery.error, "Không tải được hồ sơ nền.")}
      </p>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="mx-auto max-w-3xl px-6 py-10">
      <h1 className="font-heading text-[28px] font-bold tracking-[-0.02em] text-foreground">
        {props.mode === "edit" ? "Hồ sơ Bệnh nhân Nền tảng" : "Tạo hồ sơ nền"}
      </h1>
      <p className="mt-1 text-sm text-muted-foreground">
        Thông tin lâm sàng nền tảng, dùng làm dữ liệu đầu vào phụ trợ cho phân tích AI (UC-06).
      </p>

      {identity ? (
        <section className="mt-6 rounded-xl border border-border bg-muted/40 p-5">
          <h2 className="mb-3 text-xs font-semibold uppercase text-muted-foreground">
            Thông tin định danh
          </h2>
          {/* Ba trường này lấy từ bảng users và chỉ đọc ở màn này (UC-06 bước 2); #18 cũng
              không nhận chúng. Muốn sửa thì dùng khối "Thông tin tài khoản" bên dưới, và chỉ
              Điều dưỡng mới có khối đó. */}
          <dl className="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <div>
              <dt className="text-xs text-muted-foreground">Họ và tên</dt>
              <dd className="mt-0.5 font-semibold text-foreground">{identity.fullName}</dd>
            </div>
            <div>
              <dt className="text-xs text-muted-foreground">Ngày sinh</dt>
              <dd className="mt-0.5 font-semibold text-foreground">
                {formatIsoDate(identity.dateOfBirth)}
              </dd>
            </div>
            <div>
              <dt className="text-xs text-muted-foreground">Số điện thoại</dt>
              <dd className="mt-0.5 font-mono font-semibold tabular-nums text-foreground">
                {identity.phone || EMPTY_VALUE}
              </dd>
            </div>
          </dl>
        </section>
      ) : null}

      {/* PatientProfile không có email — đó là dữ liệu tài khoản, không phải hồ sơ nền.
          PatientAccountActions tự gọi usePatientAccount để lấy email thật (sửa 06/08/2026,
          review Task C11) thay vì nhận null cứng, vốn khiến lưu là xoá mất email đang có. */}
      {props.mode === "edit" && loaded ? (
        <PatientAccountActions
          userId={loaded.patientUserId}
          fullName={loaded.fullName}
          phone={loaded.phone}
          dateOfBirth={loaded.dateOfBirth}
        />
      ) : null}

      <section className="mt-6 space-y-5 rounded-xl border border-border p-5">
        <div>
          <label htmlFor="gender" className="mb-1.5 block text-sm font-medium">
            Giới tính {props.mode === "edit" ? "*" : ""}
          </label>
          <select
            id="gender"
            value={gender}
            onChange={(event) => setGender(event.target.value as Gender | "")}
            className="h-10 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            <option value="">-- Chưa xác định --</option>
            {GENDERS.map((value) => (
              <option key={value} value={value}>
                {genderLabel(value)}
              </option>
            ))}
          </select>
        </div>

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
      </section>

      {errorMessage ? (
        <p className="mt-4 rounded-lg bg-destructive/10 p-3 text-sm text-destructive" role="alert">
          {errorMessage}
        </p>
      ) : null}

      <div className="mt-6 flex justify-end gap-3">
        <button
          type="button"
          onClick={() => router.back()}
          className="rounded-lg border border-border px-4 py-2 text-sm font-medium hover:bg-accent"
        >
          Huỷ bỏ
        </button>
        <button
          type="submit"
          // Disable cả khi isSuccess, không chỉ isPending — tránh bấm lần hai trong khoảng
          // chờ điều hướng sau khi lưu thành công.
          disabled={mutation.isPending || mutation.isSuccess}
          className="rounded-lg bg-primary px-5 py-2 text-sm font-semibold text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
        >
          {props.mode === "edit" ? "Lưu hồ sơ nền" : "Tạo hồ sơ nền"}
        </button>
      </div>
    </form>
  );
}
