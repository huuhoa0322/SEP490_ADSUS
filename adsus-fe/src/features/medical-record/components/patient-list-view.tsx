"use client";

import Link from "next/link";
import { useState } from "react";
import {
  CalendarPlus,
  Eye,
  FileEdit,
  FilePlus,
  Filter,
  MoreVertical,
  Plus,
  Search,
  UserCheck,
} from "lucide-react";

import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { PaginationNumbered } from "@/components/ui/pagination-numbered";
import { getApiErrorMessage } from "@/lib/api-client";
import { useAuthStore } from "@/store/auth-store";

import { usePatientList } from "../hooks/use-patients";
import {
  caseStatusLabel,
  formatIsoDate,
  visitStatusLabel,
} from "../lib/medical-record-labels";
import type { CaseStatus, PatientSummary, VisitStatusFilter } from "../types/medical-record.types";

const VISIT_FILTERS: VisitStatusFilter[] = ["All", "Pending", "Confirmed"];

/** Tạo initials từ họ tên bệnh nhân (ví dụ: Trần Thị Mai -> TM). */
function getInitials(fullName: string): string {
  if (!fullName) return "PT";
  const words = fullName.trim().split(/\s+/).filter(Boolean);
  if (words.length === 1) return words[0].slice(0, 2).toUpperCase();
  return (words[0][0] + words[words.length - 1][0]).toUpperCase();
}

/**
 * Trả về subtext nếu bệnh nhân chưa lập hồ sơ nền.
 * Không tự sinh tuổi hay giới tính giả khi API không cung cấp.
 */
function getPatientSubtext(patient: PatientSummary): string | null {
  if (!patient.patientProfileId) return "Chưa lập hồ sơ nền";
  return null;
}

/** Render soft badge trạng thái ca khám chuẩn màu Preclinic. */
function renderStatusBadge(status: CaseStatus | null) {
  if (!status) {
    return <span className="text-xs text-muted-foreground">—</span>;
  }
  switch (status) {
    case "CONFIRMED":
      return (
        <Badge variant="soft-success" className="font-medium text-xs px-2.5 py-0.5">
          {caseStatusLabel(status)}
        </Badge>
      );
    case "END":
      return (
        <Badge variant="soft-teal" className="font-medium text-xs px-2.5 py-0.5">
          {caseStatusLabel(status)}
        </Badge>
      );
    default:
      return (
        <Badge variant="soft-warning" className="font-medium text-xs px-2.5 py-0.5">
          {caseStatusLabel(status)}
        </Badge>
      );
  }
}

/**
 * SCR-09 — danh sách TOÀN BỘ bệnh nhân theo chuẩn thiết kế Preclinic Medical (patients.html).
 *
 * Chuẩn y tế Preclinic:
 * - Avatar tròn (avatar-md 40px) kèm initials và tên in đậm.
 * - Soft status badges.
 * - Nút thao tác nhanh và 3-dot dropdown menu.
 */
export function PatientListView() {
  const [search, setSearch] = useState("");
  const [visitStatus, setVisitStatus] = useState<VisitStatusFilter>("All");
  const [page, setPage] = useState(1);

  // UC-06 BR-03 — chỉ Điều dưỡng tạo được tài khoản bệnh nhân mới.
  const isNurse = useAuthStore((state) => state.user?.role) === "NURSE";

  const { data, isLoading, isError, error } = usePatientList({
    search,
    visitStatus,
    page,
    pageSize: 20,
  });

  return (
    <div className="mx-auto w-[90%] max-w-[90%] py-8">
      {/* Preclinic Header */}
      <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="font-heading text-2xl font-bold tracking-tight text-[#0A1B39]">
              Danh sách Bệnh nhân
            </h1>
            {data ? (
              <Badge variant="soft-primary" className="rounded-full px-2.5 py-0.5 text-xs font-semibold">
                Tổng: {data.totalItems} bệnh nhân
              </Badge>
            ) : null}
          </div>
          <p className="mt-1 text-sm text-[#6C7688]">
            Toàn bộ bệnh nhân trong hệ thống, sắp theo lần khám gần nhất
          </p>
        </div>

        {isNurse ? (
          <Link
            href="/patients/new"
            className="inline-flex h-10 items-center gap-2 rounded-md bg-[#2E37A4] px-4 text-sm font-semibold text-white shadow-xs transition-colors hover:bg-[#2E37A4]/90"
          >
            <Plus className="size-4" />
            + Thêm bệnh nhân mới
          </Link>
        ) : null}
      </div>

      {/* Preclinic Filter Bar */}
      <div className="mb-6 flex flex-wrap items-center gap-3 rounded-lg border border-[#E7E8EB] bg-white p-4 shadow-xs">
        <div className="relative min-w-[280px] flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <input
            type="search"
            value={search}
            onChange={(event) => {
              setSearch(event.target.value);
              setPage(1);
            }}
            placeholder="Tìm theo họ tên hoặc số điện thoại..."
            aria-label="Tìm bệnh nhân"
            className="h-10 w-full rounded-md border border-[#E7E8EB] bg-background pl-9 pr-3 text-sm outline-none transition-colors focus:border-[#2E37A4] focus-visible:ring-2 focus-visible:ring-[#2E37A4]/20"
          />
        </div>

        <div className="relative">
          <Filter className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <select
            value={visitStatus}
            onChange={(event) => {
              setVisitStatus(event.target.value as VisitStatusFilter);
              setPage(1);
            }}
            aria-label="Lọc theo trạng thái lần khám"
            className="h-10 appearance-none rounded-md border border-[#E7E8EB] bg-background pl-9 pr-8 text-sm outline-none transition-colors focus:border-[#2E37A4] focus-visible:ring-2 focus-visible:ring-[#2E37A4]/20"
          >
            {VISIT_FILTERS.map((filter) => (
              <option key={filter} value={filter}>
                {visitStatusLabel(filter)}
              </option>
            ))}
          </select>
        </div>
      </div>

      {isLoading ? (
        <div className="rounded-lg border border-[#E7E8EB] bg-white p-10 text-center text-sm text-muted-foreground">
          Đang tải danh sách...
        </div>
      ) : null}

      {isError ? (
        <p className="rounded-lg bg-destructive/10 p-4 text-sm text-destructive" role="alert">
          {getApiErrorMessage(error, "Không tải được danh sách bệnh nhân.")}
        </p>
      ) : null}

      {data && data.items.length === 0 ? (
        // UC-09 AF-01.
        <div className="rounded-lg border border-dashed border-[#E7E8EB] bg-white p-12 text-center">
          <UserCheck className="mx-auto size-10 text-muted-foreground/50" />
          <p className="mt-3 font-heading text-base font-semibold text-[#0A1B39]">
            Không tìm thấy bệnh nhân nào
          </p>
          <p className="mt-1 text-sm text-[#6C7688]">
            Thử xoá bớt điều kiện lọc hoặc kiểm tra lại từ khoá tìm kiếm.
          </p>
        </div>
      ) : null}

      {data && data.items.length > 0 ? (
        <div className="overflow-hidden rounded-lg border border-[#E7E8EB] bg-white shadow-xs">
          <div className="overflow-x-auto">
            <table className="w-full min-w-[760px] table-nowrap text-left text-sm align-middle">
              <thead className="border-b border-[#E7E8EB] bg-[#F8F9FA] text-xs font-semibold uppercase text-muted-foreground">
                <tr>
                  <th className="px-4 py-3.5">Bệnh nhân</th>
                  <th className="px-4 py-3.5">Số điện thoại</th>
                  <th className="px-4 py-3.5">Lần khám gần nhất</th>
                  <th className="px-4 py-3.5">Trạng thái</th>
                  <th className="px-4 py-3.5 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#E7E8EB]">
                {data.items.map((patient) => {
                  const subtext = getPatientSubtext(patient);
                  const initials = getInitials(patient.fullName);

                  return (
                    <tr key={patient.patientUserId} className="transition-colors hover:bg-[#F5F6F8]/60">
                      {/* Cột Bệnh nhân với Avatar tròn Preclinic (avatar-md 40px) */}
                      <td className="px-4 py-3.5">
                        <div className="flex items-center gap-3">
                          <Avatar size="md" className="size-10 rounded-full border border-[#E7E8EB]">
                            <AvatarFallback className="bg-[#ECEDF7] text-xs font-semibold text-[#2E37A4]">
                              {initials}
                            </AvatarFallback>
                          </Avatar>
                          <div>
                            {patient.patientProfileId ? (
                              <Link
                                href={`/patients/${patient.patientProfileId}`}
                                className="font-semibold text-[#0A1B39] transition-colors hover:text-[#2E37A4]"
                              >
                                {patient.fullName}
                              </Link>
                            ) : (
                              <span className="font-semibold text-[#0A1B39]">
                                {patient.fullName}
                              </span>
                            )}
                            {subtext ? (
                              <span className="block text-xs text-[#6C7688]">{subtext}</span>
                            ) : null}
                          </div>
                        </div>
                      </td>

                      {/* Cột Số điện thoại */}
                      <td className="px-4 py-3.5 font-mono text-xs tabular-nums text-[#6C7688]">
                        {patient.phone}
                      </td>

                      {/* Cột Lần khám gần nhất */}
                      <td className="px-4 py-3.5 text-xs text-[#0A1B39]">
                        {patient.latestVisitDate ? (
                          formatIsoDate(patient.latestVisitDate)
                        ) : (
                          <span className="italic text-muted-foreground">Chưa có lần khám nào</span>
                        )}
                      </td>

                      {/* Cột Trạng thái với soft badge */}
                      <td className="px-4 py-3.5">
                        {renderStatusBadge(patient.latestVisitStatus)}
                      </td>

                      {/* Cột Thao tác */}
                      <td className="px-4 py-3.5 text-right">
                        <div className="flex items-center justify-end gap-1.5">
                          {patient.patientProfileId ? (
                            <>
                              <Link
                                href={`/patients/${patient.patientProfileId}`}
                                className="inline-flex items-center gap-1 rounded border border-[#E7E8EB] bg-white px-2.5 py-1 text-xs font-medium text-[#0A1B39] shadow-2xs transition-colors hover:bg-[#F5F6F8] hover:text-[#2E37A4]"
                              >
                                <Eye className="size-3.5" />
                                Xem hồ sơ bệnh án
                              </Link>
                              <Link
                                href={`/patients/${patient.patientProfileId}/cases/new`}
                                className="inline-flex items-center gap-1 rounded bg-[#2E37A4] px-2.5 py-1 text-xs font-semibold text-white shadow-2xs transition-colors hover:bg-[#2E37A4]/90"
                              >
                                <CalendarPlus className="size-3.5" />
                                Tạo ca khám
                              </Link>
                              <DropdownMenu>
                                <DropdownMenuTrigger asChild>
                                  <button
                                    type="button"
                                    aria-label="Tùy chọn thao tác"
                                    className="inline-flex size-7 items-center justify-center rounded border border-[#E7E8EB] bg-white text-muted-foreground shadow-2xs transition-colors hover:bg-[#F5F6F8] hover:text-[#0A1B39]"
                                  >
                                    <MoreVertical className="size-4" />
                                  </button>
                                </DropdownMenuTrigger>
                                <DropdownMenuContent align="end" className="w-48">
                                  <DropdownMenuItem asChild>
                                    <Link href={`/patients/${patient.patientProfileId}`}>
                                      <Eye className="size-4 text-muted-foreground" />
                                      Xem hồ sơ bệnh án
                                    </Link>
                                  </DropdownMenuItem>
                                  <DropdownMenuItem asChild>
                                    <Link href={`/patients/${patient.patientProfileId}/cases/new`}>
                                      <CalendarPlus className="size-4 text-muted-foreground" />
                                      Tạo ca khám mới
                                    </Link>
                                  </DropdownMenuItem>
                                  <DropdownMenuItem asChild>
                                    <Link href={`/patients/${patient.patientProfileId}/profile`}>
                                      <FileEdit className="size-4 text-muted-foreground" />
                                      Chỉnh sửa thông tin
                                    </Link>
                                  </DropdownMenuItem>
                                </DropdownMenuContent>
                              </DropdownMenu>
                            </>
                          ) : (
                            <>
                              <Link
                                href={`/patients/new?patientUserId=${patient.patientUserId}`}
                                className="inline-flex items-center gap-1 rounded bg-[#2E37A4] px-3 py-1 text-xs font-semibold text-white shadow-2xs transition-colors hover:bg-[#2E37A4]/90"
                              >
                                <FilePlus className="size-3.5" />
                                Tạo hồ sơ nền
                              </Link>
                              <DropdownMenu>
                                <DropdownMenuTrigger asChild>
                                  <button
                                    type="button"
                                    aria-label="Tùy chọn thao tác"
                                    className="inline-flex size-7 items-center justify-center rounded border border-[#E7E8EB] bg-white text-muted-foreground shadow-2xs transition-colors hover:bg-[#F5F6F8] hover:text-[#0A1B39]"
                                  >
                                    <MoreVertical className="size-4" />
                                  </button>
                                </DropdownMenuTrigger>
                                <DropdownMenuContent align="end" className="w-48">
                                  <DropdownMenuItem asChild>
                                    <Link href={`/patients/new?patientUserId=${patient.patientUserId}`}>
                                      <FilePlus className="size-4 text-muted-foreground" />
                                      Tạo hồ sơ nền
                                    </Link>
                                  </DropdownMenuItem>
                                </DropdownMenuContent>
                              </DropdownMenu>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      ) : null}

      {/* Preclinic Pagination */}
      {data && data.totalPages > 1 ? (
        <div className="mt-4 flex items-center justify-between">
          <p className="font-mono text-xs tabular-nums text-[#6C7688]">
            Trang {data.page} / {data.totalPages} · {data.totalItems} bệnh nhân
          </p>
          <PaginationNumbered
            currentPage={data.page}
            totalPages={data.totalPages}
            setPage={setPage}
          />
        </div>
      ) : null}
    </div>
  );
}
