"use client";

import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";

import { getApiErrorMessage } from "@/lib/api-client";
import { useAuthStore } from "@/store/auth-store";

import { useCreateCase, useCaseList, useCaseDetail } from "../hooks/use-cases";
import { useDoctorList } from "../hooks/use-doctors";
import { useSymptomCategories } from "../hooks/use-symptoms";
import { SymptomSelector } from "./symptom-selector";
import type { CreateCaseSymptomInput } from "../types/medical-record.types";

/**
 * SCR-11 — tạo lần khám mới (UC-07).
 *
 * Sửa 07/08/2026 — ảnh siêu âm KHÔNG còn ở màn này nữa (quyết định ghi đè): không phải lần
 * khám nào cũng chụp siêu âm ngay lúc tiếp nhận. Muốn bổ sung ảnh thì làm sau qua màn chi tiết
 * ca (`#21`, dùng lại `UltrasoundUploadField`).
 *
 * Bác sĩ phụ trách: nếu người tạo chính là Bác sĩ thì ca khám LUÔN gắn với chính họ, không có
 * lựa chọn khác — khớp sát với UCS UC-07 bước 5 ("... or defaults to the signed-in Doctor")
 * hơn bản trước đó (dropdown tự do đổi). Điều dưỡng vẫn phải chọn đúng Bác sĩ chịu trách nhiệm
 * (GB-04) vì Điều dưỡng không thể là người chịu trách nhiệm chẩn đoán.
 */

function PreviousCaseSummary({ caseId }: { caseId: string }) {
  const { data: caseDetail, isLoading } = useCaseDetail(caseId);

  if (isLoading) return <div className="text-sm text-muted-foreground animate-pulse">Đang tải thông tin lần khám trước...</div>;
  if (!caseDetail) return null;

  return (
    <div className="rounded-xl border border-border bg-accent/30 p-5 space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-bold text-foreground">
          Nội dung lần khám gần nhất ({new Date(caseDetail.visitDate).toLocaleDateString('vi-VN')})
        </h3>
      </div>
      
      {caseDetail.finalDiagnosis && (
        <div>
          <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wider block mb-1">Chẩn đoán: </span>
          <span className="text-sm text-foreground">{caseDetail.finalDiagnosis}</span>
        </div>
      )}
      
      {caseDetail.doctorConclusion && (
        <div>
          <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wider block mb-1">Kết luận: </span>
          <span className="text-sm text-foreground">{caseDetail.doctorConclusion}</span>
        </div>
      )}
      
      {caseDetail.symptoms && caseDetail.symptoms.length > 0 && (
        <div>
          <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wider block mb-2">Triệu chứng chi tiết:</span>
          <ul className="list-disc list-inside text-sm text-foreground space-y-1.5 ml-1">
            {caseDetail.symptoms.map((sym, idx) => {
               const text = sym.symptomName || sym.otherNote;
               return text ? (
                 <li key={idx} className="leading-snug">
                   <span className="font-medium">{sym.categoryName}:</span> {text} {sym.symptomName && sym.otherNote ? `(${sym.otherNote})` : ''}
                 </li>
               ) : null;
            })}
          </ul>
        </div>
      )}
    </div>
  );
}

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

  // Fetch the most recent case for this patient
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

    mutation.mutate(
      {
        patientProfileId,
        responsibleDoctorId,
        clinicalInfo: clinicalInfo.trim() || null,
        symptoms: symptoms
          .filter(s => s.categoryId !== "")
          .map(s => ({
            ...s,
            otherNote: s.otherNote?.trim() || null
          }))
          .filter(s => {
             // 1. Lọc bỏ fallback "Khác" hoặc Category "Other" rỗng
             if (s.symptomId === null && s.otherNote === null) return false;
             
             // 2. Lọc bỏ DB "Khác" nếu người dùng tick nhưng không gõ chữ gì
             if (s.symptomId !== null && s.otherNote === null && categoriesQuery.data) {
                const category = categoriesQuery.data.find(c => c.categoryId === s.categoryId);
                if (category) {
                   const sym = category.symptoms.find(x => x.symptomId === s.symptomId);
                   if (sym && (sym.isOther || sym.name.toLowerCase().includes('khác'))) {
                      return false; // Xóa luôn không lưu vào DB
                   }
                }
             }
             return true;
          }),
        images: [],
      },
      { onSuccess: (created) => router.push(`/cases/${created.caseId}`) },
    );
  }

  return (
    <form onSubmit={handleSubmit} className="mx-auto max-w-6xl px-6 py-10">
      <h1 className="font-heading text-[28px] font-bold tracking-[-0.02em] text-foreground">
        Tạo ca khám
      </h1>
      <p className="mt-1 text-sm text-muted-foreground">
        Ca khám mới được lưu ở trạng thái &ldquo;Mới tạo&rdquo;. Ảnh siêu âm (nếu có) bổ sung sau
        ở màn chi tiết ca.
      </p>

      {/* Thông tin lần khám trước (nếu có) */}
      {previousCaseId && (
        <div className="mt-6">
          <PreviousCaseSummary caseId={previousCaseId} />
        </div>
      )}

      <section className="mt-6 space-y-5 rounded-xl border border-border p-5">
        <div>
          <label htmlFor="responsibleDoctorId" className="mb-1.5 block text-sm font-medium">
            Bác sĩ phụ trách *
          </label>

          {isDoctor ? (
            <p
              id="responsibleDoctorId"
              className="flex h-10 items-center rounded-lg border border-border bg-muted/40 px-3 text-sm font-medium"
            >
              {currentUser?.fullName}
            </p>
          ) : (
            <select
              id="responsibleDoctorId"
              value={selectedDoctorId}
              onChange={(event) => setSelectedDoctorId(event.target.value)}
              disabled={doctorsQuery.isLoading}
              className="h-10 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50"
            >
              <option value="">-- Chọn bác sĩ --</option>
              {doctorsQuery.data?.map((doctor) => (
                <option key={doctor.userId} value={doctor.userId}>
                  {doctor.fullName}
                </option>
              ))}
            </select>
          )}

          {doctorsQuery.isError ? (
            <p className="mt-1.5 text-xs text-destructive" role="alert">
              {getApiErrorMessage(doctorsQuery.error, "Không tải được danh sách bác sĩ.")}
            </p>
          ) : (
            <p className="mt-1.5 text-xs text-muted-foreground">
              {isDoctor
                ? "Bạn là người chịu trách nhiệm chẩn đoán cho ca khám này."
                : "Mỗi ca khám phải gắn đúng một bác sĩ chịu trách nhiệm chẩn đoán."}
            </p>
          )}
        </div>

        <div>
          <label className="mb-1.5 block text-sm font-medium">
            Triệu chứng chi tiết
          </label>
          <SymptomSelector value={symptoms} onChange={setSymptoms} />
        </div>

        <div>
          <label htmlFor="clinicalInfo" className="mb-1.5 block text-sm font-medium">
            Lý do khám (Ghi chú chung)
          </label>
          <textarea
            id="clinicalInfo"
            value={clinicalInfo}
            onChange={(event) => setClinicalInfo(event.target.value)}
            rows={6}
            placeholder="Vị trí đau, khối sờ thấy, tiết dịch..."
            className="w-full rounded-lg border border-border bg-background p-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
          />
        </div>
      </section>

      {errorMessage ? (
        <p className="mt-5 rounded-lg bg-destructive/10 p-3 text-sm text-destructive" role="alert">
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
          disabled={mutation.isPending || mutation.isSuccess}
          className="rounded-lg bg-primary px-5 py-2 text-sm font-semibold text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
        >
          {mutation.isPending ? "Đang lưu..." : "Lưu ca khám"}
        </button>
      </div>
    </form>
  );
}
