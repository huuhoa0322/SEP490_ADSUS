"use client";

import Link from "next/link";
import { useState } from "react";

import { getApiErrorMessage } from "@/lib/api-client";
import { cn } from "@/lib/utils";
import { useAuthStore } from "@/store/auth-store";

import { usePatientList } from "../hooks/use-patients";
import {
  caseStatusLabel,
  formatIsoDate,
  visitStatusLabel,
} from "../lib/medical-record-labels";
import type { CaseStatus, VisitStatusFilter } from "../types/medical-record.types";

const VISIT_FILTERS: VisitStatusFilter[] = ["All", "Pending", "Confirmed"];

/** Màu huy hiệu theo trạng thái lần khám gần nhất. */
function statusBadgeClass(status: CaseStatus): string {
  switch (status) {
    case "CONFIRMED":
      return "bg-emerald-50 text-emerald-700";
    case "ANALYZED":
      return "bg-violet-50 text-violet-700";
    default:
      return "bg-amber-50 text-amber-700";
  }
}

/**
 * SCR-09 — danh sách TOÀN BỘ bệnh nhân trong hệ thống, sắp theo lần khám gần nhất (UC-09).
 *
 * Không phải "hàng chờ khám hôm nay": mỗi dòng là một BỆNH NHÂN, không phải một ca khám.
 * Danh sách lần khám của từng người nằm ở SCR-12.
 */
export function PatientListView() {
  const [search, setSearch] = useState("");
  const [visitStatus, setVisitStatus] = useState<VisitStatusFilter>("All");
  const [page, setPage] = useState(1);

  // UC-06 BR-03 — chỉ Điều dưỡng tạo được tài khoản bệnh nhân. Ẩn hẳn nút khỏi Bác sĩ thay
  // vì để anh ấy bấm rồi nhận 403.
  const isNurse = useAuthStore((state) => state.user?.role) === "NURSE";

  const { data, isLoading, isError, error } = usePatientList({
    search,
    visitStatus,
    page,
    pageSize: 20,
  });

  return (
    <div className="mx-auto max-w-7xl px-6 py-10">
      <header className="mb-6">
        <h1 className="font-heading text-[28px] font-bold tracking-[-0.02em] text-foreground">
          Danh sách Bệnh nhân
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Toàn bộ bệnh nhân trong hệ thống, sắp theo lần khám gần nhất
        </p>
      </header>

      <div className="mb-4 flex flex-wrap items-center gap-3 rounded-xl border border-border bg-card p-4">
        <input
          type="search"
          value={search}
          onChange={(event) => {
            setSearch(event.target.value);
            setPage(1);
          }}
          placeholder="Tìm theo họ tên hoặc số điện thoại..."
          aria-label="Tìm bệnh nhân"
          className="h-10 min-w-[280px] flex-1 rounded-lg border border-border bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
        />

        <select
          value={visitStatus}
          onChange={(event) => {
            setVisitStatus(event.target.value as VisitStatusFilter);
            setPage(1);
          }}
          aria-label="Lọc theo trạng thái lần khám"
          className="h-10 rounded-lg border border-border bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {VISIT_FILTERS.map((filter) => (
            <option key={filter} value={filter}>
              {visitStatusLabel(filter)}
            </option>
          ))}
        </select>

        {isNurse ? (
          <Link
            href="/patients/new"
            className="inline-flex h-10 items-center rounded-lg bg-primary px-4 text-sm font-semibold text-primary-foreground hover:bg-primary/90"
          >
            + Thêm bệnh nhân mới
          </Link>
        ) : null}
      </div>

      {isLoading ? <p className="text-sm text-muted-foreground">Đang tải danh sách...</p> : null}

      {isError ? (
        <p className="rounded-lg bg-destructive/10 p-4 text-sm text-destructive" role="alert">
          {getApiErrorMessage(error, "Không tải được danh sách bệnh nhân.")}
        </p>
      ) : null}

      {data && data.items.length === 0 ? (
        // UC-09 AF-01.
        <div className="rounded-xl border border-dashed border-border p-12 text-center">
          <p className="font-heading text-base font-semibold text-foreground">
            Không tìm thấy bệnh nhân nào
          </p>
          <p className="mt-1 text-sm text-muted-foreground">
            Thử xoá bớt điều kiện lọc hoặc kiểm tra lại từ khoá tìm kiếm.
          </p>
        </div>
      ) : null}

      {data && data.items.length > 0 ? (
        <div className="overflow-x-auto rounded-xl border border-border">
          <table className="w-full min-w-[720px] text-left text-sm">
            <thead className="bg-muted/50 text-xs uppercase text-muted-foreground">
              <tr>
                <th className="px-4 py-3 font-semibold">Họ và tên</th>
                <th className="px-4 py-3 font-semibold">Số điện thoại</th>
                <th className="px-4 py-3 font-semibold">Lần khám gần nhất</th>
                <th className="px-4 py-3 text-right font-semibold">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((patient) => (
                <tr key={patient.patientUserId} className="border-t border-border">
                  <td className="px-4 py-3 font-medium text-foreground">{patient.fullName}</td>
                  <td className="px-4 py-3 font-mono tabular-nums text-muted-foreground">
                    {patient.phone}
                  </td>
                  <td className="px-4 py-3">
                    {patient.latestVisitDate ? (
                      <span className="flex flex-wrap items-center gap-2">
                        {formatIsoDate(patient.latestVisitDate)}
                        {patient.latestVisitStatus ? (
                          <span
                            className={cn(
                              "rounded px-2 py-0.5 text-xs font-semibold",
                              statusBadgeClass(patient.latestVisitStatus),
                            )}
                          >
                            {caseStatusLabel(patient.latestVisitStatus)}
                          </span>
                        ) : null}
                      </span>
                    ) : (
                      <span className="italic text-muted-foreground">Chưa có lần khám nào</span>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex justify-end gap-2">
                      {patient.patientProfileId ? (
                        <>
                          <Link
                            href={`/patients/${patient.patientProfileId}`}
                            className="rounded-lg border border-border px-3 py-1.5 text-xs font-medium hover:bg-accent"
                          >
                            Xem hồ sơ bệnh án
                          </Link>
                          <Link
                            href={`/patients/${patient.patientProfileId}/cases/new`}
                            className="rounded-lg bg-primary px-3 py-1.5 text-xs font-semibold text-primary-foreground hover:bg-primary/90"
                          >
                            Tạo ca khám
                          </Link>
                        </>
                      ) : (
                        // Chưa có hồ sơ nền: không xem được (chưa có gì để xem) và không tạo
                        // được ca khám (UC-07 Depends On: UC-06).
                        <Link
                          href={`/patients/new?patientUserId=${patient.patientUserId}`}
                          className="rounded-lg bg-primary px-3 py-1.5 text-xs font-semibold text-primary-foreground hover:bg-primary/90"
                        >
                          Tạo hồ sơ nền
                        </Link>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}

      {data && data.totalPages > 1 ? (
        <div className="mt-4 flex items-center justify-between">
          <p className="font-mono text-xs tabular-nums text-muted-foreground">
            Trang {data.page} / {data.totalPages} · {data.totalItems} bệnh nhân
          </p>
          <div className="flex gap-2">
            <button
              type="button"
              onClick={() => setPage((current) => Math.max(1, current - 1))}
              disabled={data.page <= 1}
              className="rounded-lg border border-border px-3 py-1.5 text-xs font-medium disabled:opacity-50"
            >
              Trước
            </button>
            <button
              type="button"
              onClick={() => setPage((current) => current + 1)}
              disabled={data.page >= data.totalPages}
              className="rounded-lg border border-border px-3 py-1.5 text-xs font-medium disabled:opacity-50"
            >
              Sau
            </button>
          </div>
        </div>
      ) : null}
    </div>
  );
}
