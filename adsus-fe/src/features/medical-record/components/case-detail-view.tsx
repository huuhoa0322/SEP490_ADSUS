"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";

import { getApiErrorMessage } from "@/lib/api-client";
import { cn } from "@/lib/utils";
import { useAuthStore } from "@/store/auth-store";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";

import {
  useCaseDetail,
  useConfirmCase,
  useEndCaseWithoutPrescription,
  useSaveCaseConclusion,
} from "../hooks/use-cases";
import { useExportCaseReport } from "../hooks/use-case-report";
import {
  EMPTY_VALUE,
  caseStatusLabel,
  formatIsoDate,
  formatIsoDateTime,
  genderLabel,
} from "../lib/medical-record-labels";
import type { CaseStatus } from "../types/medical-record.types";
import { useDiagnosticStore } from "../stores/use-diagnostic-store";

import { UltrasoundImageGallery } from "./ultrasound-image-gallery";
import { UltrasoundUploadField } from "./ultrasound-upload-field";
import { PrescriptionSection } from "@/features/prescriptions/components/prescription-section";

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
 * SCR-30 — chi tiết một lần khám (UC-08, UC-12).
 *
 * Ba luật nghiệp vụ được encode thẳng vào trạng thái nút:
 *   GB-01       — ca đã CONFIRMED thì không nhận thêm ảnh.
 *   UC-12 BR-01 — chỉ xuất được báo cáo PDF của ca đã CONFIRMED.
 *   GB-04       — chỉ đúng Bác sĩ phụ trách ca này mới chốt được kết luận.
 *
 * Backend cũng chặn cả ba (422), nhưng bày ra nút chắc chắn báo lỗi thì chỉ tổ làm người
 * dùng bối rối.
 *
 * Chốt kết luận (thêm 07/08/2026, sửa lại cùng ngày — tách Lưu/Kết thúc): làm ngay tại đây
 * thay vì đợi màn duyệt kết quả AI riêng (UC-19, đang được xây song song bởi một luồng công
 * việc khác — xem CaseConclusionRequest phía backend). Hai hành động RIÊNG BIỆT:
 *   "Lưu kết luận"     — chỉ lưu nội dung, KHÔNG đổi trạng thái, sửa lại được nhiều lần.
 *   "Kết thúc ca khám" — lưu VÀ khoá ca (CONFIRMED) — trạng thái cuối, không có đường lùi
 *                        (GB-01/P2), nên phần kết luận biến thành chỉ đọc vĩnh viễn sau đó.
 *
 * `isLocked` (thêm 07/08/2026) là khoá TẠM, thuần client, không liên quan CaseStatus: ngay
 * sau khi "Lưu kết luận" thành công, 2 ô nhập + nút "Bổ sung ảnh siêu âm" tạm khoá lại để
 * tránh sửa nhầm; bấm "Sửa" mới mở lại được. "Kết thúc ca khám" không bị ảnh hưởng bởi khoá
 * này — vẫn bấm được ngay cả khi đang khoá, vì nó chỉ gửi lại đúng nội dung vừa lưu.
 */
export function CaseDetailView({ caseId }: { caseId: string }) {
  const router = useRouter();
  const { data: medicalCase, isLoading, isError, error } = useCaseDetail(caseId);
  const [showUpload, setShowUpload] = useState(false);
  const [pendingImages, setPendingImages] = useState<File[]>([]);
  const [note, setNote] = useState("");
  const [finalDiagnosis, setFinalDiagnosis] = useState("");
  const [doctorConclusion, setDoctorConclusion] = useState("");
  const [conclusionError, setConclusionError] = useState<string | null>(null);
  // Khoá tạm sau khi "Lưu kết luận" thành công — xem chú thích đầu file.
  const [isLocked, setIsLocked] = useState(false);
  // Dùng cho mẫu "đồng bộ state khi prop đổi" bên dưới — khai ở đây (KHÔNG phải sau early
  // return) để không vi phạm Rules of Hooks: mọi useState phải gọi đúng số lần, đúng thứ tự
  // ở mọi lượt render, kể cả lượt render sớm bị chặn bởi isLoading/isError.
  const [syncedCaseId, setSyncedCaseId] = useState<string | null>(null);

  const currentUser = useAuthStore((state) => state.user);
  const saveConclusionMutation = useSaveCaseConclusion(caseId);
  const confirmMutation = useConfirmCase(caseId);
  const endCaseMutation = useEndCaseWithoutPrescription(caseId);
  const report = useExportCaseReport(caseId);

  const [isEndCaseModalOpen, setIsEndCaseModalOpen] = useState(false);

  if (isLoading) {
    return <p className="p-10 text-sm text-muted-foreground">Đang tải ca khám...</p>;
  }

  if (isError || !medicalCase) {
    return (
      <p className="m-10 rounded-lg bg-destructive/10 p-4 text-sm text-destructive" role="alert">
        {getApiErrorMessage(error, "Không tìm thấy ca khám.")}
      </p>
    );
  }

  // CONFIRMED hoặc END đều cho phép xem (END = ca đã kê đơn, không bổ sung được nữa).
  const isConfirmedOrEnd = medicalCase.status === "CONFIRMED" || medicalCase.status === "END";
  // GB-04 — chỉ hiện form kết luận cho ĐÚNG Bác sĩ phụ trách ca này, không phải Bác sĩ bất kỳ
  // hay Điều dưỡng. Đây chỉ là lớp trải nghiệm; backend chặn thật ở SaveConclusionAsync/ConfirmAsync.
  const isResponsibleDoctor =
    currentUser?.role === "DOCTOR" && currentUser.userId === medicalCase.doctorId;

  // Đổ kết luận đã lưu trước đó (nếu có, từ lần "Lưu kết luận" trước) vào form ngay trong lúc
  // render — cùng mẫu "đồng bộ state khi prop đổi" đã dùng ở PatientProfileForm (Task C9).
  // Không dùng useEffect: sẽ có một nhịp hiển thị rỗng trước khi effect chạy, và tải lại trang
  // ngay lúc đó là hiện nhầm form trống dù ca đã có nháp.
  if (medicalCase.caseId !== syncedCaseId) {
    setSyncedCaseId(medicalCase.caseId);
    setFinalDiagnosis(medicalCase.finalDiagnosis ?? "");
    setDoctorConclusion(medicalCase.doctorConclusion ?? "");
    setIsLocked(false);
  }

  function validateConclusionFields(): boolean {
    setConclusionError(null);

    if (!finalDiagnosis.trim() || !doctorConclusion.trim()) {
      setConclusionError("Vui lòng nhập đầy đủ chẩn đoán và kết luận.");
      return false;
    }

    return true;
  }

  function handleSaveConclusion() {
    if (!validateConclusionFields()) return;

    saveConclusionMutation.mutate(
      { finalDiagnosis: finalDiagnosis.trim(), doctorConclusion: doctorConclusion.trim() },
      { onSuccess: () => setIsLocked(true) },
    );
  }

  function handleEditConclusion() {
    setIsLocked(false);
  }

  function handleConfirm() {
    if (!validateConclusionFields()) return;

    confirmMutation.mutate({
      finalDiagnosis: finalDiagnosis.trim(),
      doctorConclusion: doctorConclusion.trim(),
    });
  }

  function handleAddImages() {
    if (pendingImages.length === 0) return;

    // Use diagnostic store and redirect instead of mutating directly
    useDiagnosticStore.getState().setDiagnosticSession(caseId, pendingImages);
    router.push(`/cases/${caseId}/diagnostic`);
  }

  return (
    <div className="mx-auto w-full max-w-screen-xl px-6 py-10">
      <header className="flex flex-wrap items-start justify-between gap-4 rounded-xl border border-border bg-card p-6">
        <div>
          <h1 className="font-heading text-[26px] font-bold tracking-[-0.02em] text-foreground">
            Lần khám ngày {formatIsoDate(medicalCase.visitDate)}
          </h1>
          <p className="mt-2 text-sm text-muted-foreground">
            Bác sĩ phụ trách:{" "}
            <strong className="text-foreground">{medicalCase.doctorName}</strong>
          </p>
        </div>

        <div className="flex flex-col items-end gap-3">
          <span
            className={cn(
              "rounded px-3 py-1 text-sm font-semibold",
              statusBadgeClass(medicalCase.status),
            )}
          >
            {caseStatusLabel(medicalCase.status)}
          </span>

          {medicalCase.status === "END" ? (
            <div className="flex flex-col items-end gap-1">
              <button
                type="button"
                onClick={report.exportReport}
                // UC-12 BR-01.
                disabled={report.isPending}
                className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground hover:bg-primary/90 disabled:bg-muted disabled:text-muted-foreground"
              >
                {report.isPending ? "Đang tạo file..." : "Xuất báo cáo PDF"}
              </button>
            </div>
          ) : null}

          {/* Module 7 — Kê đơn thuốc: chỉ hiện khi ca CONFIRMED (chưa kê) và đúng Bác sĩ phụ trách.
              END thì ẩn vì đã kê đơn rồi, chỉ hiện prescription section. */}
          {medicalCase.status === "CONFIRMED" && isResponsibleDoctor ? (
            <div className="mt-2 flex flex-col items-end gap-2">
              <Link
                href={`/prescriptions/new?caseId=${caseId}`}
                className="w-full text-center rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground hover:bg-primary/90"
              >
                Kê đơn thuốc
              </Link>
              <Dialog open={isEndCaseModalOpen} onOpenChange={setIsEndCaseModalOpen}>
                <DialogTrigger asChild>
                  <button
                    type="button"
                    disabled={endCaseMutation.isPending}
                    className="w-full rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
                  >
                    {endCaseMutation.isPending ? "Đang xử lý..." : "Kết thúc ca bệnh"}
                  </button>
                </DialogTrigger>
                <DialogContent>
                  <DialogHeader>
                    <DialogTitle className="text-xl">Xác nhận kết thúc ca bệnh</DialogTitle>
                    <DialogDescription className="text-base text-foreground mt-2">
                      Chắc chắn muốn kết thúc ca bệnh mà không có đơn thuốc?
                    </DialogDescription>
                  </DialogHeader>
                  <DialogFooter className="mt-4">
                    <button
                      type="button"
                      onClick={() => setIsEndCaseModalOpen(false)}
                      className="rounded-lg border border-border px-6 py-2 text-base font-medium hover:bg-accent"
                    >
                      Hủy
                    </button>
                    <button
                      type="button"
                      onClick={() => {
                        endCaseMutation.mutate(undefined, {
                          onSuccess: () => setIsEndCaseModalOpen(false),
                        });
                      }}
                      disabled={endCaseMutation.isPending}
                      className="rounded-lg bg-primary px-6 py-2 text-base font-semibold text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
                    >
                      {endCaseMutation.isPending ? "Đang xử lý..." : "Kết thúc"}
                    </button>
                  </DialogFooter>
                </DialogContent>
              </Dialog>
            </div>
          ) : null}
        </div>
      </header>

      {report.error ? (
        <p className="mt-3 rounded-lg bg-destructive/10 p-3 text-sm text-destructive" role="alert">
          {getApiErrorMessage(report.error, "Ca bệnh chưa kết luận, không thể xuất báo cáo PDF.")}
        </p>
      ) : null}

      {medicalCase.patientProfile ? (
        <section className="mt-5 rounded-xl border border-l-4 border-border border-l-primary p-5 space-y-4">
          <div className="flex flex-col gap-1">
            <h3 className="text-base font-bold text-foreground">
              Bệnh nhân: {medicalCase.patientProfile.fullName}
            </h3>
            {medicalCase.patientProfile.dateOfBirth && (
              <span className="text-sm text-muted-foreground">
                Ngày sinh: {formatIsoDate(medicalCase.patientProfile.dateOfBirth)}
              </span>
            )}
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wider block mb-1">Tiền sử bệnh</p>
              <div className="text-sm font-semibold text-foreground">
                {medicalCase.patientProfile.diseases && medicalCase.patientProfile.diseases.length > 0 ? (
                  <div className="flex flex-col gap-1">
                    {medicalCase.patientProfile.diseases.map(d => (
                      <div key={d.diseaseId}>
                        {d.isOther ? (d.note || d.diseaseName) : d.note ? `${d.diseaseName}: ${d.note}` : d.diseaseName}
                      </div>
                    ))}
                  </div>
                ) : (
                  <span className="text-muted-foreground italic font-normal">Không có</span>
                )}
              </div>
            </div>
            <div>
              <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wider block mb-1">Dị ứng</p>
              <div className="text-sm font-semibold text-foreground">
                {medicalCase.patientProfile.allergies && medicalCase.patientProfile.allergies.length > 0 ? (
                  <div className="flex flex-col gap-1">
                    {medicalCase.patientProfile.allergies.map(a => (
                      <div key={a.allergyTypeId}>
                        {a.isOther ? (a.note || a.allergyName) : a.note ? `${a.allergyName}: ${a.note}` : a.allergyName}
                      </div>
                    ))}
                  </div>
                ) : (
                  <span className="text-muted-foreground italic font-normal">Không có</span>
                )}
              </div>
            </div>
          </div>

          <div className="flex justify-end pt-2">
            <Link
              href={`/patients/${medicalCase.patientProfileId}`}
              className="rounded-lg border border-border px-4 py-2 text-sm font-medium hover:bg-accent"
            >
              Mở hồ sơ bệnh nhân
            </Link>
          </div>
        </section>
      ) : null}

      {/* Module 7 — Đơn thuốc: hiện khi ca ở trạng thái END (đã kê đơn) */}
      {medicalCase.status === "END" ? (
        <PrescriptionSection caseId={caseId} />
      ) : null}

      <div className="mt-5 grid grid-cols-1 gap-5 lg:grid-cols-[1.7fr_1fr]">
        <section className="rounded-xl border border-border p-6">
          <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
            <h2 className="font-heading text-lg font-semibold text-foreground">
              Ảnh siêu âm{" "}
              <span className="font-mono text-sm font-normal text-muted-foreground">
                {medicalCase.ultrasoundImages.length} ảnh
              </span>
            </h2>
            <div className="flex flex-col items-end gap-1">
              {isResponsibleDoctor ? (
                <>
                  <button
                    type="button"
                    onClick={() => setShowUpload((open) => !open)}
                    // GB-01 — ca đã chốt không nhận thêm ảnh. isLocked — khoá tạm sau "Lưu kết luận".
                    disabled={isConfirmedOrEnd || isLocked}
                    className="rounded-lg border border-border px-4 py-2 text-sm font-medium hover:bg-accent disabled:opacity-50"
                  >
                    Bổ sung ảnh siêu âm
                  </button>
                  {isConfirmedOrEnd ? (
                    <span className="text-xs italic text-muted-foreground">
                      Ca đã kết luận nên không nhận thêm ảnh
                    </span>
                  ) : isLocked ? (
                    <span className="text-xs italic text-muted-foreground">
                      Bấm &ldquo;Sửa&rdquo; ở mục kết luận để mở lại
                    </span>
                  ) : null}
                </>
              ) : null}
            </div>
          </div>

          {showUpload && isResponsibleDoctor && !isConfirmedOrEnd && !isLocked ? (
            <div className="mb-5 space-y-4 rounded-lg border border-dashed border-border p-4">
              <UltrasoundUploadField
                files={pendingImages}
                onChange={setPendingImages}
              />

              <div>
                <label htmlFor="batch-note" className="mb-1.5 block text-sm font-medium">
                  Ghi chú cho lô ảnh này
                </label>
                <input
                  id="batch-note"
                  value={note}
                  onChange={(event) => setNote(event.target.value)}
                  placeholder="Áp dụng cho toàn bộ ảnh vừa chọn"
                  className="h-10 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
                />
              </div>

              <div className="flex justify-end">
                <button
                  type="button"
                  onClick={handleAddImages}
                  disabled={pendingImages.length === 0}
                  className="rounded-xl bg-blue-600 px-6 py-3 text-base font-bold uppercase tracking-wide text-white shadow-lg transition-all hover:bg-blue-700 hover:shadow-xl disabled:opacity-50"
                >
                  { }
                  Xem kết quả AI
                </button>
              </div>
            </div>
          ) : null}

          <UltrasoundImageGallery images={medicalCase.ultrasoundImages} />
        </section>

        <div className="space-y-5">
          <section className="rounded-xl border border-border p-6">
            <h2 className="mb-3 font-heading text-lg font-semibold text-foreground">
              Thông tin lâm sàng
            </h2>

            {medicalCase.symptoms && medicalCase.symptoms.length > 0 ? (
              <div className="mb-4">
                <h3 className="mb-2 text-sm font-semibold uppercase text-muted-foreground">Triệu chứng chi tiết</h3>
                <ul className="list-disc pl-5 space-y-1 text-sm">
                  {medicalCase.symptoms.map((s) => (
                    <li key={`${s.categoryId}-${s.symptomId ?? "other"}`}>
                      <span className="font-medium">{s.categoryName}:</span>{" "}
                      {s.symptomName ? s.symptomName : ""}
                      {s.otherNote ? ` (${s.otherNote})` : ""}
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}

            {medicalCase.clinicalInfo ? (
              <>
                <h3 className="mb-2 text-sm font-semibold uppercase text-muted-foreground">Ghi chú chung</h3>
                <p className="text-sm leading-relaxed">
                  {medicalCase.clinicalInfo}
                </p>
              </>
            ) : null}
          </section>

          <section className="rounded-xl border border-border p-6">
            <h2 className="mb-3 font-heading text-lg font-semibold text-foreground">
              Kết luận của bác sĩ
            </h2>

            {isConfirmedOrEnd ? (
              <dl className="space-y-4">
                {/* DTO thật tách hai trường; API Spec v0.1 gộp thành một `conclusion`. */}
                <div>
                  <dt className="text-xs font-semibold uppercase text-muted-foreground">
                    Chẩn đoán cuối cùng
                  </dt>
                  <dd className="mt-1 text-sm leading-relaxed">
                    {medicalCase.finalDiagnosis || EMPTY_VALUE}
                  </dd>
                </div>
                <div className="border-t border-border pt-4">
                  <dt className="text-xs font-semibold uppercase text-muted-foreground">
                    Kết luận / Hướng xử trí
                  </dt>
                  <dd className="mt-1 text-sm leading-relaxed">
                    {medicalCase.doctorConclusion || EMPTY_VALUE}
                  </dd>
                </div>
              </dl>
            ) : isResponsibleDoctor ? (
              <div className="space-y-4">
                <div>
                  <label htmlFor="finalDiagnosis" className="mb-1.5 block text-sm font-medium">
                    Chẩn đoán cuối cùng *
                  </label>
                  <textarea
                    id="finalDiagnosis"
                    value={finalDiagnosis}
                    onChange={(event) => setFinalDiagnosis(event.target.value)}
                    rows={3}
                    disabled={saveConclusionMutation.isPending || confirmMutation.isPending || isLocked}
                    placeholder="Ví dụ: Nhân xơ tử cung (BI-RADS 3)"
                    className="w-full rounded-lg border border-border bg-background p-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50"
                  />
                </div>
                <div>
                  <label htmlFor="doctorConclusion" className="mb-1.5 block text-sm font-medium">
                    Kết luận / Hướng xử trí *
                  </label>
                  <textarea
                    id="doctorConclusion"
                    value={doctorConclusion}
                    onChange={(event) => setDoctorConclusion(event.target.value)}
                    rows={3}
                    disabled={saveConclusionMutation.isPending || confirmMutation.isPending || isLocked}
                    placeholder="Ví dụ: Theo dõi định kỳ sau 6 tháng"
                    className="w-full rounded-lg border border-border bg-background p-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50"
                  />
                </div>

                {conclusionError ? (
                  <p className="rounded-lg bg-destructive/10 p-3 text-sm text-destructive" role="alert">
                    {conclusionError}
                  </p>
                ) : null}
                {saveConclusionMutation.isError ? (
                  <p className="rounded-lg bg-destructive/10 p-3 text-sm text-destructive" role="alert">
                    {getApiErrorMessage(saveConclusionMutation.error, "Lưu kết luận thất bại.")}
                  </p>
                ) : null}
                {confirmMutation.isError ? (
                  <p className="rounded-lg bg-destructive/10 p-3 text-sm text-destructive" role="alert">
                    {getApiErrorMessage(confirmMutation.error, "Kết thúc ca khám thất bại.")}
                  </p>
                ) : null}
                {isLocked && saveConclusionMutation.isSuccess && !confirmMutation.isSuccess ? (
                  <p className="rounded-lg bg-emerald-50 p-3 text-sm text-emerald-700" role="status">
                    Đã lưu kết luận. Bấm &ldquo;Sửa&rdquo; nếu muốn chỉnh sửa tiếp, hoặc &ldquo;Xác
                    nhận kết luận&rdquo; để khoá vĩnh viễn.
                  </p>
                ) : null}

                <div className="flex justify-end gap-3">
                  {isLocked ? (
                    <button
                      type="button"
                      onClick={handleEditConclusion}
                      className="rounded-lg border border-border px-4 py-2 text-sm font-medium hover:bg-accent"
                    >
                      Sửa
                    </button>
                  ) : (
                    <button
                      type="button"
                      onClick={handleSaveConclusion}
                      disabled={saveConclusionMutation.isPending || confirmMutation.isPending}
                      className="rounded-lg border border-border px-4 py-2 text-sm font-medium hover:bg-accent disabled:opacity-50"
                    >
                      {saveConclusionMutation.isPending ? "Đang lưu..." : "Lưu kết luận"}
                    </button>
                  )}
                  {/* Không có đường lùi: bấm xong ca chuyển CONFIRMED ngay (GB-01/P2), không
                      sửa lại được nữa. KHÔNG bị chặn bởi isLocked — chỉ gửi lại đúng nội dung
                      vừa lưu, không cần mở khoá trước. */}
                  <button
                    type="button"
                    onClick={handleConfirm}
                    disabled={saveConclusionMutation.isPending || confirmMutation.isPending}
                    className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
                  >
                    {confirmMutation.isPending ? "Đang lưu..." : "Xác nhận kết luận"}
                  </button>
                </div>
              </div>
            ) : (
              <div className="rounded-lg border border-dashed border-border p-5 text-center">
                <p className="text-sm font-semibold text-foreground">
                  Ca khám chưa được kết luận
                </p>
                <p className="mt-1 text-sm leading-relaxed text-muted-foreground">
                  Chỉ Bác sĩ phụ trách ca này mới chốt được kết luận.
                </p>
              </div>
            )}
          </section>
        </div>
      </div>

      <p className="mt-5 font-mono text-xs text-muted-foreground">
        Tạo lúc {formatIsoDateTime(medicalCase.createdAt)} · Cập nhật lần cuối{" "}
        {formatIsoDateTime(medicalCase.updatedAt)}
      </p>
    </div>
  );
}
