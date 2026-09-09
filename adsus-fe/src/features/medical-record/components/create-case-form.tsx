"use client";

import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import {
  Activity,
  AlertCircle,
  ArrowLeft,
  CalendarPlus,
  History,
  ShieldAlert,
  Stethoscope,
  User,
} from "lucide-react";

import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { getApiErrorMessage } from "@/lib/api-client";
import { useAuthStore } from "@/store/auth-store";

import { useCreateCase, useCaseList, useCaseDetail } from "../hooks/use-cases";
import { useDoctorList } from "../hooks/use-doctors";
import { usePatientProfile } from "../hooks/use-patient-profile";
import { useSymptomCategories } from "../hooks/use-symptoms";
import { formatIsoDate } from "../lib/medical-record-labels";
import type { CaseSymptomDetail, CreateCaseSymptomInput } from "../types/medical-record.types";
import { SymptomSelector } from "./symptom-selector";

/** Tạo initials từ họ tên bệnh nhân. */
function getInitials(fullName: string | null | undefined): string {
  if (!fullName) return "PT";
  const words = fullName.trim().split(/\s+/).filter(Boolean);
  if (words.length === 0) return "PT";
  if (words.length === 1) return words[0].slice(0, 2).toUpperCase();
  return (words[0][0] + words[words.length - 1][0]).toUpperCase();
}


/** Khối tóm tắt thông tin lần khám trước (Preclinic appointment history). */
/** Khối tóm tắt thông tin lần khám trước (Preclinic appointment history). */
function PreviousCaseSummary({
  caseId,
  onApplySymptoms,
}: {
  caseId: string;
  onApplySymptoms?: (symptoms: CaseSymptomDetail[]) => void;
}) {
  const { data: caseDetail, isLoading, isError } = useCaseDetail(caseId);

  if (isLoading) {
    return (
      <div className="rounded-lg border border-gray-300 dark:border-gray-700 bg-white p-4 text-xs font-semibold text-foreground animate-pulse">
        Đang tải thông tin lần khám trước...
      </div>
    );
  }
  if (isError) {
    return (
      <div className="rounded-lg border border-gray-300 dark:border-gray-700 bg-white p-4 text-xs font-semibold text-foreground">
        Không tải được thông tin lần khám trước.
      </div>
    );
  }
  if (!caseDetail) return null;

  // Nhóm các triệu chứng theo danh mục để các triệu chứng cùng nhóm luôn nằm liền nhau
  const groupedSymptoms: { categoryId: string; categoryName: string; items: string[] }[] = [];
  if (caseDetail.symptoms) {
    for (const sym of caseDetail.symptoms) {
      const text = sym.symptomName || sym.otherNote;
      if (!text) continue;
      const fullText =
        sym.symptomName && sym.otherNote ? `${sym.symptomName} (${sym.otherNote})` : text;
      const existing = groupedSymptoms.find((g) => g.categoryId === sym.categoryId);
      if (existing) {
        existing.items.push(fullText);
      } else {
        groupedSymptoms.push({
          categoryId: sym.categoryId,
          categoryName: sym.categoryName,
          items: [fullText],
        });
      }
    }
  }

  const hasSymptoms = Boolean(caseDetail.symptoms && caseDetail.symptoms.length > 0);

  const hasContent =
    Boolean(caseDetail.clinicalInfo) ||
    Boolean(caseDetail.finalDiagnosis) ||
    Boolean(caseDetail.doctorConclusion) ||
    hasSymptoms;

  return (
    <div className="rounded-lg border border-gray-300 dark:border-gray-700 bg-white p-5 shadow-xs">
      <div className="mb-3.5 flex items-center justify-between border-b border-gray-200 pb-3">
        <div className="flex items-center gap-2">
          <History className="size-5 text-[#2E37A4]" />
          <h3 className="font-heading text-base font-bold text-foreground">
            Nội dung lần khám gần nhất ({formatIsoDate(caseDetail.visitDate)})
          </h3>
        </div>
        {hasSymptoms && onApplySymptoms && (
          <button
            type="button"
            onClick={() => onApplySymptoms(caseDetail.symptoms)}
            className="rounded border border-[#2E37A4]/40 bg-[#ECEDF7] px-2.5 py-1 text-xs font-semibold text-[#2E37A4] transition-colors hover:bg-[#2E37A4] hover:text-white"
            title="Sao chép triệu chứng lần trước sang ca khám này"
          >
            + Dùng lại triệu chứng này
          </button>
        )}
      </div>

      <div className="space-y-3.5">
        {caseDetail.clinicalInfo && (
          <div>
            <span className="mb-1 block text-sm font-bold uppercase tracking-wider text-foreground">
              Lý do khám / Lâm sàng trước:
            </span>
            <p className="text-sm font-medium text-foreground">{caseDetail.clinicalInfo}</p>
          </div>
        )}

        {caseDetail.finalDiagnosis && (
          <div>
            <span className="mb-1 block text-sm font-bold uppercase tracking-wider text-foreground">
              Chẩn đoán:
            </span>
            <p className="text-base font-medium text-foreground">{caseDetail.finalDiagnosis}</p>
          </div>
        )}

        {caseDetail.doctorConclusion && (
          <div>
            <span className="mb-1 block text-sm font-bold uppercase tracking-wider text-foreground">
              Kết luận:
            </span>
            <p className="text-base text-foreground">{caseDetail.doctorConclusion}</p>
          </div>
        )}

        {groupedSymptoms.length > 0 && (
          <div>
            <span className="mb-2 block text-sm font-bold uppercase tracking-wider text-foreground">
              Triệu chứng lần khám trước:
            </span>
            <div className="space-y-2">
              {groupedSymptoms.map((group) => (
                <div
                  key={group.categoryId}
                  className="rounded-md border border-gray-300 bg-[#F8F9FA] p-2.5 shadow-2xs"
                >
                  <div className="mb-1.5 flex items-center gap-1.5">
                    <span className="size-2 rounded-full bg-[#2E37A4]" />
                    <span className="text-sm font-bold text-[#2E37A4]">
                      {group.categoryName}
                    </span>
                  </div>
                  <div className="flex flex-wrap gap-1.5">
                    {group.items.map((item, idx) => (
                      <span
                        key={idx}
                        className="inline-flex items-center rounded border border-gray-300 bg-white px-2.5 py-1 text-sm font-medium text-foreground"
                      >
                        {item}
                      </span>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {!hasContent && (
          <p className="text-sm font-medium italic text-foreground/80">
            Không có ghi chú lâm sàng từ lần khám trước.
          </p>
        )}
      </div>
    </div>
  );
}

/** Khối tóm tắt hồ sơ nền bệnh nhân chuẩn Preclinic. */
function PatientProfileSummary({ profileId }: { profileId: string }) {
  const { data: profile, isLoading, isError } = usePatientProfile(profileId);

  if (isLoading) {
    return (
      <div className="rounded-lg border border-[#E7E8EB] bg-white p-5 text-sm font-semibold text-foreground animate-pulse">
        Đang tải thông tin bệnh nhân...
      </div>
    );
  }
  if (isError) {
    return (
      <div
        className="rounded-lg border border-destructive/20 bg-destructive/5 p-5 text-sm text-destructive"
        role="alert"
      >
        <div className="flex items-center gap-2 font-medium">
          <AlertCircle className="size-4 shrink-0" />
          <span>Không tải được thông tin bệnh nhân</span>
        </div>
      </div>
    );
  }
  if (!profile) return null;

  const initials = getInitials(profile.fullName);

  return (
    <div className="rounded-lg border border-gray-300 dark:border-gray-700 bg-white p-5 shadow-xs">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between border-b border-gray-200 pb-4">
        <div className="flex items-center gap-3">
          <Avatar size="md" className="size-11 rounded-full border border-[#E7E8EB]">
            <AvatarFallback className="bg-[#ECEDF7] font-semibold text-[#2E37A4]">
              {initials}
            </AvatarFallback>
          </Avatar>
          <div>
            <div className="flex items-center gap-2">
              <h3 className="font-heading text-lg font-bold text-foreground">
                Bệnh nhân: {profile.fullName?.trim() || "Chưa cập nhật"}
              </h3>
            </div>
            {profile.dateOfBirth && (
              <span className="text-sm font-semibold text-foreground">
                Ngày sinh: {formatIsoDate(profile.dateOfBirth)}
              </span>
            )}
          </div>
        </div>
      </div>

      <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div>
          <div className="mb-2 flex items-center gap-1.5">
            <ShieldAlert className="size-4 text-rose-600" />
            <span className="text-sm font-bold uppercase tracking-wider text-foreground">
              Dị ứng:
            </span>
          </div>
          <div className="text-sm font-semibold text-foreground">
            {profile.allergies && profile.allergies.length > 0 ? (
              <div className="flex flex-wrap gap-2">
                {profile.allergies.map((a, idx) => {
                  const text = a.isOther
                    ? (a.note || a.allergyName)
                    : a.note
                      ? `${a.allergyName}: ${a.note}`
                      : a.allergyName;
                  return (
                    <Badge
                      key={`${a.allergyTypeId}-${idx}`}
                      variant="soft-danger"
                      className="h-auto max-w-full text-left text-sm px-3 py-1 font-medium leading-normal whitespace-normal break-words"
                    >
                      {text}
                    </Badge>
                  );
                })}
              </div>
            ) : (
              <span className="text-sm font-semibold italic text-foreground/90">Không có</span>
            )}
          </div>
        </div>

        <div>
          <div className="mb-2 flex items-center gap-1.5">
            <Activity className="size-4 text-amber-600" />
            <span className="text-sm font-bold uppercase tracking-wider text-foreground">
              Tiền sử bệnh:
            </span>
          </div>
          <div className="text-sm font-semibold text-foreground">
            {profile.diseases && profile.diseases.length > 0 ? (
              <div className="flex flex-wrap gap-2">
                {profile.diseases.map((d, idx) => {
                  const text = d.isOther
                    ? (d.note || d.diseaseName)
                    : d.note
                      ? `${d.diseaseName}: ${d.note}`
                      : d.diseaseName;
                  return (
                    <Badge
                      key={`${d.diseaseId}-${idx}`}
                      variant="soft-warning"
                      className="h-auto max-w-full text-left text-sm px-3 py-1 font-medium leading-normal whitespace-normal break-words"
                    >
                      {text}
                    </Badge>
                  );
                })}
              </div>
            ) : (
              <span className="text-sm font-semibold italic text-foreground/90">Không có</span>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

/**
 * SCR-11 — Tạo lần khám mới theo chuẩn Preclinic (new-appointment.html card structure).
 */
export function CreateCaseForm({ patientProfileId }: { patientProfileId: string }) {
  const router = useRouter();

  const currentUser = useAuthStore((state) => state.user);
  const isDoctor = currentUser?.role === "DOCTOR";

  const [selectedDoctorId, setSelectedDoctorId] = useState("");
  const [symptoms, setSymptoms] = useState<CreateCaseSymptomInput[]>([]);
  const [clientError, setClientError] = useState<string | null>(null);

  // Bác sĩ không cần danh sách đồng nghiệp — chỉ Điều dưỡng mới phải chọn.
  const doctorsQuery = useDoctorList(!isDoctor);
  const categoriesQuery = useSymptomCategories();
  const mutation = useCreateCase();

  // Lấy ca khám gần nhất của bệnh nhân
  const previousCasesQuery = useCaseList({
    patientProfileId,
    sortOrder: "desc",
    page: 1,
    pageSize: 1,
  });
  const previousCaseId = previousCasesQuery.data?.items?.[0]?.caseId;

  const responsibleDoctorId = isDoctor ? (currentUser?.userId ?? "") : selectedDoctorId;

  const errorMessage =
    clientError ??
    (mutation.isError ? getApiErrorMessage(mutation.error, "Tạo ca khám thất bại.") : null);

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setClientError(null);

    if (!responsibleDoctorId) {
      setClientError("Vui lòng chọn bác sĩ phụ trách.");
      return;
    }

    const filteredSymptoms = symptoms
      .filter((s) => s.categoryId !== "")
      .map((s) => ({
        ...s,
        otherNote: s.otherNote?.trim() || null,
      }))
      .filter((s) => {
        if (s.symptomId === null && s.otherNote === null) return false;

        if (s.symptomId !== null && s.otherNote === null && categoriesQuery.data) {
          const category = categoriesQuery.data.find((c) => c.categoryId === s.categoryId);
          if (category) {
            const sym = category.symptoms.find((x) => x.symptomId === s.symptomId);
            if (sym && (sym.isOther || sym.name.toLowerCase().includes("khác"))) {
              return false;
            }
          }
        }
        return true;
      });

    // Nếu không có triệu chứng nào được chọn thì mặc định là "Khám định kì"
    const finalClinicalInfo = filteredSymptoms.length === 0 ? "Khám định kì" : null;

    mutation.mutate(
      {
        patientProfileId,
        responsibleDoctorId,
        clinicalInfo: finalClinicalInfo,
        symptoms: filteredSymptoms,
        images: [],
      },
      { onSuccess: (created) => router.push(`/cases/${created.caseId}`) },
    );
  }

  return (
    <div className="mx-auto w-[90%] max-w-[90%] py-8">
      {/* Breadcrumb quay lại */}
      <div className="mb-4">
        <button
          type="button"
          onClick={() => router.back()}
          className="inline-flex items-center gap-1.5 text-sm font-bold text-foreground transition-colors hover:text-[#2E37A4]"
        >
          <ArrowLeft className="size-4" />
          Quay lại
        </button>
      </div>

      <form onSubmit={handleSubmit} className="space-y-6">
        {/* Header card: Nằm ở phía TRÊN của CẢ 2 PHẦN (Full-width) */}
        <div className="rounded-lg border border-gray-300 dark:border-gray-700 bg-white p-6 shadow-xs">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex items-center gap-3">
              <div className="flex size-10 items-center justify-center rounded-full bg-[#ECEDF7] text-[#2E37A4]">
                <CalendarPlus className="size-5" />
              </div>
              <div>
                <h1 className="font-heading text-xl font-bold text-foreground">
                  Tạo ca khám
                </h1>
                <p className="mt-0.5 text-sm font-medium text-foreground">
                  Tiếp nhận ca khám mới, chỉ định bác sĩ và ghi nhận triệu chứng ban đầu
                </p>
              </div>
            </div>

            {/* Doctor Selection / Static Display */}
            <div className="w-full sm:w-[320px]">
              {isDoctor ? (
                <div
                  className="flex h-10 items-center justify-start sm:justify-end rounded-md bg-[#F8F9FA] px-3 text-sm font-semibold text-foreground"
                >
                  <User className="mr-1.5 size-4 text-[#2E37A4]" />
                  Bác sĩ: <strong className="ml-1 text-foreground">{currentUser?.fullName}</strong>
                </div>
              ) : (
                <div>
                  <label htmlFor="responsibleDoctorId" className="sr-only">
                    Bác sĩ phụ trách
                  </label>
                  <select
                    id="responsibleDoctorId"
                    value={selectedDoctorId}
                    onChange={(event) => setSelectedDoctorId(event.target.value)}
                    disabled={doctorsQuery.isLoading}
                    className="h-10 w-full rounded-md border border-[#E7E8EB] bg-background px-3 text-sm outline-none transition-colors focus:border-[#2E37A4] focus-visible:ring-2 focus-visible:ring-[#2E37A4]/20 disabled:opacity-50"
                  >
                    <option value="">-- Chọn bác sĩ phụ trách --</option>
                    {doctorsQuery.data?.map((doctor) => (
                      <option key={doctor.userId} value={doctor.userId}>
                        {doctor.fullName}
                      </option>
                    ))}
                  </select>
                  {doctorsQuery.isError && (
                    <p className="mt-1 text-right text-xs text-destructive" role="alert">
                      Không tải được danh sách bác sĩ
                    </p>
                  )}
                </div>
              )}
            </div>
          </div>
        </div>

        {/* 2 BÊN CHIA ĐÔI VỚI HỘP VIỀN ĐEN RÕ RÀNG */}
        <div className="grid grid-cols-1 items-start gap-6 lg:grid-cols-2">
          {/* CỘT TRÁI: THÔNG TIN THAM KHẢO Y TẾ (BỆNH NHÂN & LẦN KHÁM TRƯỚC) */}
          <div className="space-y-6">
            {/* Thông tin bệnh nhân */}
            <PatientProfileSummary profileId={patientProfileId} />

            {/* Lịch sử lần khám trước (nếu có) */}
            {previousCasesQuery.isLoading ? (
              <div className="rounded-lg border border-gray-300 dark:border-gray-700 bg-white p-4 text-xs font-semibold text-foreground animate-pulse">
                Đang tải lịch sử khám...
              </div>
            ) : previousCaseId ? (
              <PreviousCaseSummary
                caseId={previousCaseId}
                onApplySymptoms={(prevSymptoms) => {
                  // Gom tất cả các triệu chứng có cùng categoryId lại liền kề nhau để SymptomSelector chỉ hiển thị đúng 1 block cho mỗi category
                  const categoryOrder: string[] = [];
                  const groupedMap = new Map<string, CreateCaseSymptomInput[]>();

                  for (const s of prevSymptoms) {
                    if (!s.categoryId) continue;
                    if (!groupedMap.has(s.categoryId)) {
                      categoryOrder.push(s.categoryId);
                      groupedMap.set(s.categoryId, []);
                    }
                    const list = groupedMap.get(s.categoryId)!;
                    const isDuplicate = list.some(
                      (item) => item.symptomId === s.symptomId && item.otherNote === s.otherNote,
                    );
                    if (!isDuplicate) {
                      list.push({
                        categoryId: s.categoryId,
                        symptomId: s.symptomId,
                        otherNote: s.otherNote,
                      });
                    }
                  }

                  const consolidated: CreateCaseSymptomInput[] = [];
                  for (const catId of categoryOrder) {
                    consolidated.push(...groupedMap.get(catId)!);
                  }

                  setSymptoms(consolidated);
                }}
              />
            ) : (
              <div className="rounded-lg border border-gray-300 dark:border-gray-700 bg-white p-5 shadow-xs">
                <div className="mb-2 flex items-center gap-2 border-b border-gray-200 pb-2.5">
                  <History className="size-4 text-[#2E37A4]" />
                  <h3 className="font-heading text-sm font-bold text-foreground">
                    Lần khám gần nhất
                  </h3>
                </div>
                <p className="text-xs font-medium italic text-foreground/80">
                  Bệnh nhân chưa có lịch sử khám trước đây.
                </p>
              </div>
            )}
          </div>

          {/* CỘT PHẢI: TIẾP NHẬN CA KHÁM MỚI (LÂM SÀNG & NÚT HÀNH ĐỘNG) */}
          <div className="space-y-6">
            <div className="overflow-hidden rounded-lg border border-gray-300 dark:border-gray-700 bg-white shadow-xs">
              <div className="p-6">
                <div className="mb-4 flex items-center gap-2 border-b border-gray-200 pb-3">
                  <Stethoscope className="size-4 text-[#2E37A4]" />
                  <h2 className="font-heading text-sm font-bold uppercase tracking-wider text-foreground">
                    Thông tin lâm sàng ban đầu
                  </h2>
                </div>

                <div className="space-y-4">
                  <fieldset className="m-0 border-0 p-0">
                    <legend className="mb-2 block text-sm font-bold text-foreground">
                      Triệu chứng chi tiết
                    </legend>
                    <SymptomSelector value={symptoms} onChange={setSymptoms} />
                  </fieldset>
                </div>
              </div>

              {errorMessage ? (
                <div className="px-6 pb-4">
                  <div
                    className="flex items-center gap-2 rounded-md bg-destructive/10 p-3 text-sm text-destructive"
                    role="alert"
                  >
                    <AlertCircle className="size-4 shrink-0" />
                    <span>{errorMessage}</span>
                  </div>
                </div>
              ) : null}

              {/* Khối nút hành động */}
              <div className="flex items-center justify-end gap-3 border-t border-gray-200 bg-[#F8F9FA] px-6 py-4">
                <button
                  type="button"
                  onClick={() => router.back()}
                  className="rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-semibold text-foreground shadow-2xs transition-colors hover:bg-[var(--success)] hover:text-white"
                >
                  Huỷ bỏ
                </button>
                <button
                  type="submit"
                  disabled={mutation.isPending || mutation.isSuccess}
                  className="rounded-md bg-[#2E37A4] px-5 py-2 text-sm font-semibold text-white shadow-xs transition-colors hover:bg-[#2E37A4]/90 disabled:opacity-50"
                >
                  {mutation.isPending ? "Đang lưu..." : "Lưu ca khám"}
                </button>
              </div>
            </div>
          </div>
        </div>
      </form>
    </div>
  );
}
