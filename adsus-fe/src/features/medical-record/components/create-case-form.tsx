"use client";

import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";

import { getApiErrorMessage } from "@/lib/api-client";
import { useAuthStore } from "@/store/auth-store";

import { useCreateCase } from "../hooks/use-cases";
import { useDoctorList } from "../hooks/use-doctors";

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
export function CreateCaseForm({ patientProfileId }: { patientProfileId: string }) {
  const router = useRouter();

  const currentUser = useAuthStore((state) => state.user);
  const isDoctor = currentUser?.role === "DOCTOR";

  const [selectedDoctorId, setSelectedDoctorId] = useState("");
  const [clinicalInfo, setClinicalInfo] = useState("");
  const [clientError, setClientError] = useState<string | null>(null);

  // Bác sĩ không cần danh sách đồng nghiệp — chỉ Điều dưỡng mới phải chọn.
  const doctorsQuery = useDoctorList(!isDoctor);
  const mutation = useCreateCase();

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
        images: [],
      },
      { onSuccess: (created) => router.push(`/cases/${created.caseId}`) },
    );
  }

  return (
    <form onSubmit={handleSubmit} className="mx-auto max-w-2xl px-6 py-10">
      <h1 className="font-heading text-[28px] font-bold tracking-[-0.02em] text-foreground">
        Tạo ca khám
      </h1>
      <p className="mt-1 text-sm text-muted-foreground">
        Ca khám mới được lưu ở trạng thái &ldquo;Mới tạo&rdquo;. Ảnh siêu âm (nếu có) bổ sung sau
        ở màn chi tiết ca.
      </p>

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
          <label htmlFor="clinicalInfo" className="mb-1.5 block text-sm font-medium">
            Triệu chứng / Lý do khám
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
