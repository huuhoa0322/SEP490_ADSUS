"use client";

import Link from "next/link";
import { useState } from "react";
import {
  Activity,
  ArrowLeft,
  Calendar,
  CalendarPlus,
  Clock,
  Edit,
  Eye,
  FileText,
  Phone,
  ShieldAlert,
  User,
} from "lucide-react";

import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { PaginationNumbered } from "@/components/ui/pagination-numbered";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { getApiErrorMessage } from "@/lib/api-client";

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

/** Tạo initials từ họ tên bệnh nhân (ví dụ: Trần Thị Mai -> TM). */
function getInitials(fullName: string): string {
  if (!fullName) return "PT";
  const words = fullName.trim().split(/\s+/).filter(Boolean);
  if (words.length === 0) return "PT";
  if (words.length === 1) return words[0].slice(0, 2).toUpperCase();
  return (words[0][0] + words[words.length - 1][0]).toUpperCase();
}

/** Tính tuổi dựa trên ngày sinh. */
function calculateAge(dateOfBirth: string | null | undefined): string {
  if (!dateOfBirth) return EMPTY_VALUE;
  const birthYear = parseInt(dateOfBirth.slice(0, 4), 10);
  if (Number.isNaN(birthYear)) return EMPTY_VALUE;
  const currentYear = new Date().getFullYear();
  return `${currentYear - birthYear} tuổi`;
}

/** Render soft badge trạng thái ca khám chuẩn Preclinic. */
function renderStatusBadge(status: CaseStatus) {
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
    case "IN_PROGRESS":
      return (
        <Badge variant="soft-primary" className="font-medium text-xs px-2.5 py-0.5">
          {caseStatusLabel(status)}
        </Badge>
      );
    case "CANCELLED":
      return (
        <Badge variant="soft-danger" className="font-medium text-xs px-2.5 py-0.5">
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
 * SCR-12 — Chi tiết Hồ sơ Bệnh án & Lịch sử Ca khám chuẩn Preclinic (patient-details.html).
 *
 * Bao gồm:
 * - Header Profile Card: Avatar lớn (avatar-xxxl 96px), tên, tuổi, giới tính, SĐT, nút hành động chính.
 * - Tabbed navigation (Tabs):
 *   - Tab 1: "Lịch sử Ca khám" (Danh sách/bảng các ca khám lâm sàng với ngày khám, giờ tạo, trạng thái, nút xem chi tiết).
 *   - Tab 2: "Hồ sơ Tiền sử & Lâm sàng" (Dị ứng với soft-danger badges, bệnh mãn tính với soft-warning badges, metadata thời điểm tạo/cập nhật).
 */
export function PatientRecordView({ profileId }: { profileId: string }) {
  const [page, setPage] = useState(1);

  const profileQuery = usePatientProfile(profileId);
  const caseListQuery = useCaseList({ patientProfileId: profileId, page, pageSize: 20 });

  if (profileQuery.isLoading) {
    return (
      <div className="mx-auto w-[90%] max-w-[90%] py-10">
        <div className="rounded-lg border border-[#E7E8EB] bg-white p-12 text-center text-sm font-semibold text-foreground">
          Đang tải hồ sơ bệnh nhân...
        </div>
      </div>
    );
  }

  if (profileQuery.isError || !profileQuery.data) {
    return (
      <div className="mx-auto w-[90%] max-w-[90%] py-10">
        <p className="rounded-lg bg-destructive/10 p-4 text-sm text-destructive" role="alert">
          {getApiErrorMessage(profileQuery.error, "Không tải được hồ sơ bệnh nhân.")}
        </p>
      </div>
    );
  }

  const profile = profileQuery.data;
  const cases = caseListQuery.data;
  const ageStr = calculateAge(profile.dateOfBirth);

  return (
    <div className="mx-auto w-[90%] max-w-[90%] py-8">
      {/* Breadcrumb quay lại danh sách bệnh nhân */}
      <div className="mb-4">
        <Link
          href="/patients"
          className="inline-flex items-center gap-1.5 text-xs font-bold text-foreground transition-colors hover:text-[#2E37A4]"
        >
          <ArrowLeft className="size-3.5" />
          Quay lại danh sách bệnh nhân
        </Link>
      </div>

      {/* Preclinic Header Profile Card (patient-details.html standard) */}
      <div className="overflow-hidden rounded-lg border border-[#E7E8EB] bg-white p-6 shadow-xs">
        <div className="flex flex-col gap-6 md:flex-row md:items-center md:justify-between">
          <div className="flex flex-col gap-5 sm:flex-row sm:items-center">
            {/* Avatar và Thông tin bệnh nhân */}
            <Avatar className="size-20 shrink-0 rounded-xl">
              <AvatarFallback className="rounded-xl bg-primary/10 text-2xl font-bold text-primary">
                {getInitials(profile.fullName)}
              </AvatarFallback>
            </Avatar>

            <div className="space-y-1.5">
              <div className="flex flex-wrap items-center gap-2.5">
                <h1 className="font-heading text-2xl font-bold tracking-tight text-foreground">
                  {profile.fullName}
                </h1>
              </div>

              {/* Thông tin nhanh nhân khẩu học */}
              <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm font-semibold text-foreground">
                <span className="flex items-center gap-1">
                  <User className="size-3.5 text-foreground/70" />
                  {genderLabel(profile.gender)} {ageStr !== EMPTY_VALUE ? `· ${ageStr}` : ""}
                </span>
                <span className="flex items-center gap-1">
                  <Calendar className="size-3.5 text-foreground/70" />
                  {formatIsoDate(profile.dateOfBirth)}
                </span>
                <span className="flex items-center gap-1 font-mono">
                  <Phone className="size-3.5 text-foreground/70" />
                  {profile.phone || EMPTY_VALUE}
                </span>
              </div>
            </div>
          </div>

          {/* Primary Action Buttons */}
          <div className="flex flex-wrap items-center gap-2.5">
            <Link
              href={`/patients/${profileId}/cases/new`}
              className="inline-flex items-center gap-1.5 rounded-md bg-[#2E37A4] px-4 py-2 text-sm font-semibold text-white shadow-xs transition-colors hover:bg-[#2E37A4]/90"
            >
              <CalendarPlus className="size-4" />
              Tạo ca khám mới
            </Link>
            <Link
              href={`/patients/${profileId}/profile`}
              className="inline-flex items-center gap-1.5 rounded-md border border-[#E7E8EB] bg-white px-3.5 py-2 text-sm font-semibold text-foreground shadow-2xs transition-colors hover:bg-[#F5F6F8] hover:text-[#2E37A4]"
            >
              <Edit className="size-4" />
              Sửa hồ sơ nền
            </Link>
          </div>
        </div>
      </div>

      {/* Preclinic Tab Navigation (nav nav-tabs nav-bordered) */}
      <div className="mt-6">
        <Tabs defaultValue="cases" className="w-full">
          <TabsList className="mb-6 flex w-full justify-start border-b border-[#E7E8EB] bg-transparent p-0">
            <TabsTrigger
              value="cases"
              className="relative flex items-center gap-2 rounded-none border-b-2 border-transparent px-5 py-3 text-sm font-bold text-foreground/70 transition-all hover:text-foreground data-[state=active]:border-[#2E37A4] data-[state=active]:bg-transparent data-[state=active]:text-[#2E37A4] data-[state=active]:shadow-none"
            >
              <Calendar className="size-4" />
              Lịch sử Ca khám
              {cases ? (
                <Badge variant="soft-primary" className="ml-1 text-[11px] px-1.5 py-0.2">
                  {cases.totalItems}
                </Badge>
              ) : null}
            </TabsTrigger>
            <TabsTrigger
              value="history"
              className="relative flex items-center gap-2 rounded-none border-b-2 border-transparent px-5 py-3 text-sm font-bold text-foreground/70 transition-all hover:text-foreground data-[state=active]:border-[#2E37A4] data-[state=active]:bg-transparent data-[state=active]:text-[#2E37A4] data-[state=active]:shadow-none"
            >
              <FileText className="size-4" />
              Hồ sơ Tiền sử & Lâm sàng
            </TabsTrigger>
          </TabsList>

          {/* TAB 1: Lịch sử Ca khám */}
          <TabsContent value="cases" className="space-y-4 outline-none">
            {caseListQuery.isLoading ? (
              <div className="rounded-lg border border-[#E7E8EB] bg-white p-8 text-center text-sm font-semibold text-foreground">
                Đang tải danh sách lần khám...
              </div>
            ) : null}

            {caseListQuery.isError ? (
              <p className="rounded-lg bg-destructive/10 p-3 text-sm text-destructive" role="alert">
                {getApiErrorMessage(caseListQuery.error, "Không tải được danh sách lần khám.")}
              </p>
            ) : null}

            {cases && cases.items.length === 0 ? (
              <div className="rounded-lg border border-dashed border-[#E7E8EB] bg-white p-12 text-center">
                <Calendar className="mx-auto size-10 text-foreground/30" />
                <p className="mt-3 font-semibold text-foreground">Chưa có lần khám nào</p>
                <p className="mt-1 text-sm font-medium text-foreground">
                  Bấm &ldquo;Tạo ca khám mới&rdquo; để bắt đầu lần khám đầu tiên.
                </p>
                <div className="mt-4">
                  <Link
                    href={`/patients/${profileId}/cases/new`}
                    className="inline-flex items-center gap-1.5 rounded-md bg-[#2E37A4] px-4 py-2 text-xs font-semibold text-white hover:bg-[#2E37A4]/90"
                  >
                    <CalendarPlus className="size-3.5" />
                    Tạo ca khám mới
                  </Link>
                </div>
              </div>
            ) : null}

            {cases && cases.items.length > 0 ? (
              <div className="overflow-hidden rounded-lg border border-[#E7E8EB] bg-white shadow-xs">
                <div className="overflow-x-auto">
                  <table className="w-full table-nowrap text-left text-sm align-middle">
                    <thead className="border-b border-[#E7E8EB] bg-[#F8F9FA] text-xs font-bold uppercase tracking-wider text-foreground">
                      <tr className="[&>th:first-child]:pl-6 [&>td:first-child]:pl-6">
                        <th className="px-5 py-3.5">Lần khám</th>
                        <th className="px-5 py-3.5">Thời điểm tạo</th>
                        <th className="px-5 py-3.5">Trạng thái</th>
                        <th className="px-5 py-3.5 text-right">Thao tác</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-[#E7E8EB]">
                      {cases.items.map((visit) => (
                        <tr key={visit.caseId} className="transition-colors hover:bg-[#F5F6F8]/60 [&>th:first-child]:pl-6 [&>td:first-child]:pl-6">
                          <td className="px-5 py-4">
                            <span className="font-semibold text-foreground">
                              Lần khám ngày {formatIsoDate(visit.visitDate)}
                            </span>
                          </td>
                          <td className="px-5 py-4 text-xs font-medium text-foreground">
                            <span className="inline-flex items-center gap-1.5">
                              <Clock className="size-3.5 text-foreground/70" />
                              Tạo lúc {formatIsoDateTime(visit.createdAt)}
                            </span>
                          </td>
                          <td className="px-5 py-4">
                            {renderStatusBadge(visit.status)}
                          </td>
                          <td className="px-5 py-4 text-right">
                            <Link
                              href={`/cases/${visit.caseId}`}
                              className="inline-flex items-center gap-1 rounded border border-[#E7E8EB] bg-white px-3 py-1.5 text-xs font-semibold text-foreground shadow-2xs transition-colors hover:bg-[#F5F6F8] hover:text-primary"
                            >
                              <Eye className="size-3.5" />
                              Xem chi tiết ca
                            </Link>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                {cases.totalPages > 1 ? (
                  <div className="flex items-center justify-between border-t border-[#E7E8EB] px-5 py-3.5">
                    <p className="font-mono text-xs font-semibold tabular-nums text-foreground">
                      Trang {cases.page} / {cases.totalPages} · {cases.totalItems} lần khám
                    </p>
                    <PaginationNumbered
                      currentPage={cases.page}
                      totalPages={cases.totalPages}
                      setPage={setPage}
                    />
                  </div>
                ) : null}
              </div>
            ) : null}
          </TabsContent>

          {/* TAB 2: Hồ sơ Tiền sử & Lâm sàng (forceMount để các test DOM luôn truy cập được) */}
          <TabsContent value="history" forceMount className="space-y-6 outline-none data-[state=inactive]:hidden">
            <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
              {/* Card Dị ứng với Preclinic soft-danger badges */}
              <div className="rounded-lg border border-[#E7E8EB] bg-white p-5 shadow-xs">
                <div className="mb-4 flex items-center gap-2 border-b border-[#E7E8EB] pb-3">
                  <ShieldAlert className="size-4 text-rose-500" />
                  <h3 className="font-heading text-base font-bold text-foreground">
                    Dị ứng
                  </h3>
                </div>
                {profile.allergies && profile.allergies.length > 0 ? (
                  <div className="flex flex-wrap gap-2">
                    {profile.allergies.map((a) => {
                      const text = a.isOther
                        ? (a.note || a.allergyName)
                        : a.note
                          ? `${a.allergyName}: ${a.note}`
                          : a.allergyName;
                      return (
                        <span
                          key={a.allergyTypeId}
                          className="inline-flex items-center rounded-md bg-rose-50 px-2.5 py-1 text-xs font-medium text-rose-800 border border-rose-200"
                        >
                          {text}
                        </span>
                      );
                    })}
                  </div>
                ) : (
                  <p className="text-sm font-semibold italic text-foreground/90">Không có</p>
                )}
              </div>

              {/* Card Tiền sử bệnh với Preclinic soft-warning badges */}
              <div className="rounded-lg border border-[#E7E8EB] bg-white p-5 shadow-xs">
                <div className="mb-4 flex items-center gap-2 border-b border-[#E7E8EB] pb-3">
                  <Activity className="size-4 text-amber-500" />
                  <h3 className="font-heading text-base font-bold text-foreground">
                    Tiền sử bệnh
                  </h3>
                </div>
                {profile.diseases && profile.diseases.length > 0 ? (
                  <div className="flex flex-wrap gap-2">
                    {profile.diseases.map((d) => {
                      const text = d.isOther
                        ? (d.note || d.diseaseName)
                        : d.note
                          ? `${d.diseaseName}: ${d.note}`
                          : d.diseaseName;
                      return (
                        <span
                          key={d.diseaseId}
                          className="inline-flex items-center rounded-md bg-amber-50 px-2.5 py-1 text-xs font-medium text-amber-800 border border-amber-200"
                        >
                          {text}
                        </span>
                      );
                    })}
                  </div>
                ) : (
                  <p className="text-sm font-semibold italic text-foreground/90">Không có</p>
                )}
              </div>
            </div>

            {/* Metadata dòng thời gian thực từ API */}
            <div className="flex flex-wrap items-center justify-between gap-2 text-xs font-semibold text-foreground pt-1 px-1">
              <span>
                Hồ sơ được lập lúc: {formatIsoDateTime(profile.createdAt)} · Cập nhật lần cuối: {formatIsoDateTime(profile.updatedAt)}
              </span>
            </div>
          </TabsContent>
        </Tabs>
      </div>
    </div>
  );
}
