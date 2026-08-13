"use client";

import Link from "next/link";
import { useState } from "react";

import { getApiErrorMessage } from "@/lib/api-client";
import { cn } from "@/lib/utils";

import { useCaseList } from "../hooks/use-cases";
import { usePatientProfile } from "../hooks/use-patient-profile";
import {
  EMPTY_VALUE,
  caseStatusLabel,
  formatIsoDate,
  formatIsoDateTime,
  genderLabel,
} from "../lib/medical-record-labels";
import type { CaseStatus } from "../types/medical-record.types";

function statusBadgeClass(status: CaseStatus): string {
  switch (status) {
    case "CONFIRMED":
      return "bg-violet-50 text-violet-700";
    case "END":
      return "bg-emerald-50 text-emerald-700";
    default:
      return "bg-amber-50 text-amber-700";
  }
}

/**
 * SCR-12 — hồ sơ bệnh án và danh sách lần khám của một bệnh nhân (UC-08).
 *
 * CỐ Ý không có khối kết quả AI và đơn thuốc: chúng thuộc Module 05 và 07, chưa có backend.
 * `#23` có trả `aiResults`/`prescription`, nhưng hiện một huy hiệu "3 phát hiện AI" mà bấm
 * không được cũng là giao diện chết — cùng lý do đã bỏ mọi trường không có dữ liệu thật.
 */
export function PatientRecordView({ profileId }: { profileId: string }) {
  const [page, setPage] = useState(1);

  const profileQuery = usePatientProfile(profileId);
  const caseListQuery = useCaseList({ patientProfileId: profileId, page, pageSize: 20 });

  if (profileQuery.isLoading) {
    return <p className="p-10 text-sm text-muted-foreground">Đang tải hồ sơ bệnh nhân...</p>;
  }

  if (profileQuery.isError || !profileQuery.data) {
    return (
      <p className="m-10 rounded-lg bg-destructive/10 p-4 text-sm text-destructive" role="alert">
        {getApiErrorMessage(profileQuery.error, "Không tải được hồ sơ bệnh nhân.")}
      </p>
    );
  }

  const profile = profileQuery.data;
  const cases = caseListQuery.data;

  return (
    <div className="mx-auto w-full max-w-screen-xl px-6 py-10">
      <header className="rounded-xl border border-border bg-card p-6">
        <h1 className="font-heading text-[28px] font-bold tracking-[-0.02em] text-foreground">
          {profile.fullName}
        </h1>
        <dl className="mt-4 grid grid-cols-2 gap-4 sm:grid-cols-4">
          <div>
            <dt className="text-xs text-muted-foreground">Ngày sinh</dt>
            <dd className="mt-0.5 font-semibold">{formatIsoDate(profile.dateOfBirth)}</dd>
          </div>
          <div>
            <dt className="text-xs text-muted-foreground">Giới tính</dt>
            <dd className="mt-0.5 font-semibold">{genderLabel(profile.gender)}</dd>
          </div>
          <div>
            <dt className="text-xs text-muted-foreground">Số điện thoại</dt>
            <dd className="mt-0.5 font-mono font-semibold tabular-nums">
              {profile.phone || EMPTY_VALUE}
            </dd>
          </div>
        </dl>
      </header>

      <section className="mt-5 rounded-xl border border-border p-6">
        <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
          <h2 className="font-heading text-lg font-semibold text-foreground">Hồ sơ nền</h2>
          <Link
            href={`/patients/${profileId}/profile`}
            className="rounded-lg border border-border px-4 py-2 text-sm font-medium hover:bg-accent"
          >
            Sửa hồ sơ nền
          </Link>
        </div>
        <dl className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div>
            <dt className="text-xs text-muted-foreground">Dị ứng</dt>
            <dd className="mt-0.5 font-semibold text-destructive">
              {profile.allergies || EMPTY_VALUE}
            </dd>
          </div>
          <div>
            <dt className="text-xs text-muted-foreground">Tiền sử bệnh</dt>
            <dd className="mt-0.5">{profile.medicalHistory || EMPTY_VALUE}</dd>
          </div>
        </dl>
      </section>

      <div className="mt-5">
        <Link
          href={`/patients/${profileId}/cases/new`}
          className="inline-flex items-center rounded-lg bg-primary px-5 py-2.5 text-sm font-semibold text-primary-foreground hover:bg-primary/90"
        >
          Tạo ca khám mới
        </Link>
      </div>

      <section className="mt-5 rounded-xl border border-border p-6">
        <h2 className="mb-4 font-heading text-lg font-semibold text-foreground">
          Danh sách lần khám
        </h2>

        {caseListQuery.isLoading ? (
          <p className="text-sm text-muted-foreground">Đang tải danh sách lần khám...</p>
        ) : null}

        {caseListQuery.isError ? (
          <p className="rounded-lg bg-destructive/10 p-3 text-sm text-destructive" role="alert">
            {getApiErrorMessage(caseListQuery.error, "Không tải được danh sách lần khám.")}
          </p>
        ) : null}

        {cases && cases.items.length === 0 ? (
          <div className="rounded-lg border border-dashed border-border p-8 text-center">
            <p className="font-semibold text-foreground">Chưa có lần khám nào</p>
            <p className="mt-1 text-sm text-muted-foreground">
              Bấm &ldquo;Tạo ca khám mới&rdquo; để bắt đầu lần khám đầu tiên.
            </p>
          </div>
        ) : null}

        <ul className="space-y-3">
          {cases?.items.map((visit) => (
            <li
              key={visit.caseId}
              className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-border p-4"
            >
              <div>
                <p className="font-semibold text-foreground">
                  Lần khám ngày {formatIsoDate(visit.visitDate)}
                </p>
                {/* visitDate là ngày khám thật (DateOnly, không giờ); createdAt là lúc tạo bản
                    ghi — hai khái niệm khác nhau, cố tình không gộp làm một (xem type
                    CaseSummary). Thay UUID thô bằng mốc giờ tạo, hữu ích hơn cho người đọc. */}
                <p className="mt-0.5 text-xs text-muted-foreground">
                  Tạo lúc {formatIsoDateTime(visit.createdAt)}
                </p>
              </div>
              <div className="flex items-center gap-3">
                <span
                  className={cn(
                    "rounded px-2 py-0.5 text-xs font-semibold",
                    statusBadgeClass(visit.status),
                  )}
                >
                  {caseStatusLabel(visit.status)}
                </span>
                <Link
                  href={`/cases/${visit.caseId}`}
                  className="rounded-lg border border-border px-4 py-2 text-sm font-medium hover:bg-accent"
                >
                  Xem chi tiết ca
                </Link>
              </div>
            </li>
          ))}
        </ul>

        {cases && cases.totalPages > 1 ? (
          <div className="mt-4 flex items-center justify-between border-t border-border pt-4">
            <p className="font-mono text-xs tabular-nums text-muted-foreground">
              Trang {cases.page} / {cases.totalPages} · {cases.totalItems} lần khám
            </p>
            <div className="flex gap-2">
              <button
                type="button"
                onClick={() => setPage((current) => Math.max(1, current - 1))}
                disabled={cases.page <= 1}
                className="rounded-lg border border-border px-3 py-1.5 text-xs font-medium disabled:opacity-50"
              >
                Trước
              </button>
              <button
                type="button"
                onClick={() => setPage((current) => current + 1)}
                disabled={cases.page >= cases.totalPages}
                className="rounded-lg border border-border px-3 py-1.5 text-xs font-medium disabled:opacity-50"
              >
                Sau
              </button>
            </div>
          </div>
        ) : null}
      </section>
    </div>
  );
}
