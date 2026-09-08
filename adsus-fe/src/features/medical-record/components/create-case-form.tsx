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
import type { CreateCaseSymptomInput } from "../types/medical-record.types";
import { SymptomSelector } from "./symptom-selector";

/** Tạo initials từ họ tên bệnh nhân. */
function getInitials(fullName: string): string {
  if (!fullName) return "PT";
  const words = fullName.trim().split(/\s+/).filter(Boolean);
  if (words.length === 1) return words[0].slice(0, 2).toUpperCase();
  return (words[0][0] + words[words.length - 1][0]).toUpperCase();
}


/** Khối tóm tắt thông tin lần khám trước (Preclinic appointment history). */
function PreviousCaseSummary({ caseId }: { caseId: string }) {
  const { data: caseDetail, isLoading } = useCaseDetail(caseId);

  if (isLoading) {
    return (
      <div className="rounded-lg border border-[#E7E8EB] bg-white p-4 text-xs text-muted-foreground animate-pulse">
        Đang tải thông tin lần khám trước...
      </div>
    );
  }
  if (!caseDetail) return null;

  return (
    <div className="rounded-lg border border-[#E7E8EB] bg-white p-5 shadow-xs">
      <div className="mb-3 flex items-center gap-2 border-b border-[#E7E8EB] pb-2.5">
        <History className="size-4 text-[#2E37A4]" />
        <h3 className="font-heading text-sm font-bold text-[#0A1B39]">
          Nội dung lần khám gần nhất ({new Date(caseDetail.visitDate).toLocaleDateString("vi-VN")})
        </h3>
      </div>

      <div className="space-y-3">
        {caseDetail.finalDiagnosis && (
          <div>
            <span className="mb-1 block text-xs font-semibold uppercase tracking-wider text-[#6C7688]">
              Chẩn đoán:{" "}
            </span>
            <span className="text-sm font-medium text-[#0A1B39]">{caseDetail.finalDiagnosis}</span>
          </div>
        )}

        {caseDetail.doctorConclusion && (
          <div>
            <span className="mb-1 block text-xs font-semibold uppercase tracking-wider text-[#6C7688]">
              Kết luận:{" "}
            </span>
            <span className="text-sm text-[#0A1B39]">{caseDetail.doctorConclusion}</span>
          </div>
        )}

        {caseDetail.symptoms && caseDetail.symptoms.length > 0 && (
          <div>
            <span className="mb-1.5 block text-xs font-semibold uppercase tracking-wider text-[#6C7688]">
              Triệu chứng chi tiết:
            </span>
            <ul className="ml-1 list-inside list-disc space-y-1 text-sm text-[#0A1B39]">
              {caseDetail.symptoms.map((sym) => {
                const text = sym.symptomName || sym.otherNote;
                return text ? (
                  <li key={`${sym.categoryId}-${sym.symptomId ?? "other"}`} className="leading-snug">
                    <span className="font-medium">{sym.categoryName}:</span> {text}{" "}
                    {sym.symptomName && sym.otherNote ? `(${sym.otherNote})` : ""}
                  </li>
                ) : null;
              })}
            </ul>
          </div>
        )}
      </div>
    </div>
  );
}

/** Khối tóm tắt hồ sơ nền bệnh nhân chuẩn Preclinic. */
function PatientProfileSummary({ profileId }: { profileId: string }) {
  const { data: profile, isLoading } = usePatientProfile(profileId);

  if (isLoading) {
    return (
      <div className="rounded-lg border border-[#E7E8EB] bg-white p-5 text-sm text-muted-foreground animate-pulse">
        Đang tải thông tin bệnh nhân...
      </div>
    );
  }
  if (!profile) return null;

  const initials = getInitials(profile.fullName);

  return (
    <div className="rounded-lg border border-[#E7E8EB] bg-white p-5 shadow-xs">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between border-b border-[#E7E8EB] pb-4">
        <div className="flex items-center gap-3">
          <Avatar size="md" className="size-11 rounded-full border border-[#E7E8EB]">
            <AvatarFallback className="bg-[#ECEDF7] font-semibold text-[#2E37A4]">
              {initials}
            </AvatarFallback>
          </Avatar>
          <div>
            <div className="flex items-center gap-2">
              <h3 className="font-heading text-base font-bold text-[#0A1B39]">
                Bệnh nhân: {profile.fullName}
              </h3>
            </div>
            {profile.dateOfBirth && (
              <span className="text-xs text-[#6C7688]">
                Ngày sinh: {new Date(profile.dateOfBirth).toLocaleDateString("vi-VN")}
              </span>
            )}
          </div>
        </div>
      </div>

      <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div>
          <div className="mb-1.5 flex items-center gap-1.5">
            <ShieldAlert className="size-3.5 text-rose-500" />
            <span className="text-xs font-semibold uppercase tracking-wider text-[#6C7688]">
              Dị ứng:
            </span>
          </div>
          <div className="text-sm font-semibold text-foreground">
            {profile.allergies && profile.allergies.length > 0 ? (
              <div className="flex flex-wrap gap-1.5">
                {profile.allergies.map((a) => {
                  const text = a.isOther
                    ? (a.note || a.allergyName)
                    : a.note
                      ? `${a.allergyName}: ${a.note}`
                      : a.allergyName;
                  return (
                    <Badge key={a.allergyTypeId} variant="soft-danger" className="text-xs px-2 py-0.5">
                      {text}
                    </Badge>
                  );
                })}
              </div>
            ) : (
              <span className="text-sm font-normal italic text-muted-foreground">Không có</span>
            )}
          </div>
        </div>

        <div>
          <div className="mb-1.5 flex items-center gap-1.5">
            <Activity className="size-3.5 text-amber-500" />
            <span className="text-xs font-semibold uppercase tracking-wider text-[#6C7688]">
              Tiền sử bệnh:
            </span>
          </div>
          <div className="text-sm font-semibold text-foreground">
            {profile.diseases && profile.diseases.length > 0 ? (
              <div className="flex flex-wrap gap-1.5">
                {profile.diseases.map((d) => {
                  const text = d.isOther
                    ? (d.note || d.diseaseName)
                    : d.note
                      ? `${d.diseaseName}: ${d.note}`
                      : d.diseaseName;
                  return (
                    <Badge key={d.diseaseId} variant="soft-warning" className="text-xs px-2 py-0.5">
                      {text}
                    </Badge>
                  );
                })}
              </div>
            ) : (
              <span className="text-sm font-normal italic text-muted-foreground">Không có</span>
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
  const [clinicalInfo, setClinicalInfo] = useState("");
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

    let finalClinicalInfo = clinicalInfo.trim() || null;
    if (filteredSymptoms.length === 0 && !finalClinicalInfo) {
      finalClinicalInfo = "Khám định kì";
    }

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
    <div className="mx-auto w-full max-w-screen-2xl px-6 py-8">
      {/* Breadcrumb quay lại */}
      <div className="mb-4">
        <button
          type="button"
          onClick={() => router.back()}
          className="inline-flex items-center gap-1.5 text-xs font-semibold text-[#6C7688] transition-colors hover:text-[#2E37A4]"
        >
          <ArrowLeft className="size-3.5" />
          Quay lại
        </button>
      </div>

      <form onSubmit={handleSubmit}>
        <div className="grid grid-cols-1 items-start gap-6 lg:grid-cols-12">
          {/* Cột trái (lg:col-span-5) — Thông tin tham khảo y tế */}
          <div className="space-y-6 lg:col-span-5">
            {/* Thông tin bệnh nhân */}
            <PatientProfileSummary profileId={patientProfileId} />

            {/* Lịch sử lần khám trước (nếu có) */}
            {previousCaseId && (
              <PreviousCaseSummary caseId={previousCaseId} />
            )}
          </div>

          {/* Cột phải (lg:col-span-7) — Tiếp nhận ca khám mới */}
          <div className="space-y-6 lg:col-span-7">
            {/* Header card: Tiêu đề "Tạo ca khám" + Chọn bác sĩ phụ trách */}
            <div className="rounded-lg border border-[#E7E8EB] bg-white p-6 shadow-xs">
              <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex items-center gap-3">
                  <div className="flex size-10 items-center justify-center rounded-full bg-[#ECEDF7] text-[#2E37A4]">
                    <CalendarPlus className="size-5" />
                  </div>
                  <div>
                    <h1 className="font-heading text-xl font-bold text-[#0A1B39]">
                      Tạo ca khám
                    </h1>
                    <p className="mt-0.5 text-xs text-[#6C7688]">
                      Tiếp nhận ca khám mới, chỉ định bác sĩ và ghi nhận triệu chứng ban đầu
                    </p>
                  </div>
                </div>

                {/* Doctor Selection / Static Display */}
                <div className="w-full sm:w-[320px]">
                  <label htmlFor="responsibleDoctorId" className="sr-only">
                    Bác sĩ phụ trách
                  </label>
                  {isDoctor ? (
                    <div
                      id="responsibleDoctorId"
                      className="flex h-10 items-center justify-start sm:justify-end rounded-md bg-[#F8F9FA] px-3 text-sm font-medium text-[#6C7688]"
                    >
                      <User className="mr-1.5 size-4 text-[#2E37A4]" />
                      Bác sĩ: <strong className="ml-1 text-[#0A1B39]">{currentUser?.fullName}</strong>
                    </div>
                  ) : (
                    <div>
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

            {/* Khối Thông tin lâm sàng ban đầu & Khối nút hành động */}
            <div className="overflow-hidden rounded-lg border border-[#E7E8EB] bg-white shadow-xs">
              <div className="p-6">
                <div className="mb-4 flex items-center gap-2 border-b border-[#E7E8EB] pb-3">
                  <Stethoscope className="size-4 text-[#2E37A4]" />
                  <h2 className="font-heading text-sm font-bold uppercase tracking-wider text-[#0A1B39]">
                    Thông tin lâm sàng ban đầu
                  </h2>
                </div>

                <div className="space-y-4">
                  <div>
                    <label htmlFor="clinicalInfo" className="mb-1.5 block text-xs font-semibold text-[#0A1B39]">
                      Lý do khám / Ghi chú ban đầu
                    </label>
                    <input
                      id="clinicalInfo"
                      value={clinicalInfo}
                      onChange={(e) => setClinicalInfo(e.target.value)}
                      placeholder="Ví dụ: Đau tức hạ vị âm ỉ 3 ngày nay, trễ kinh..."
                      className="h-10 w-full rounded-md border border-[#E7E8EB] bg-background px-3 text-sm outline-none transition-colors focus:border-[#2E37A4] focus-visible:ring-2 focus-visible:ring-[#2E37A4]/20"
                    />
                  </div>

                  <fieldset className="m-0 border-0 p-0">
                    <legend className="mb-2 block text-xs font-semibold text-[#0A1B39]">
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
