"use client";

import { use, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { useDiagnosticStore } from "@/features/medical-record/stores/use-diagnostic-store";
import { useBackgroundAi } from "@/features/medical-record/hooks/use-background-ai";
import { DiagnosticCanvas } from "@/features/medical-record/components/diagnostic-canvas";
import { ArrowLeft, ChevronLeft, ChevronRight, Loader2 } from "lucide-react";
import { medicalRecordQueryKeys } from "@/features/medical-record/hooks/query-keys";
import { apiClient } from "@/lib/api-client";
import { checkIntersection, generateBurntImage } from "@/features/medical-record/utils/canvas-utils";

export default function DiagnosticPage({ params }: { params: Promise<{ caseId: string }> }) {
  const { caseId } = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();
  const { images, currentIndex, prevImage, nextImage, removeImage, clearSession, drafts, aiResults } = useDiagnosticStore();

  const [isSavingAll, setIsSavingAll] = useState(false);
  const [savingProgress, setSavingProgress] = useState(0);

  useBackgroundAi();

  useEffect(() => {
    if (images.length === 0) {
      router.push(`/cases/${caseId}`);
    }
  }, [images.length, caseId, router]);

  if (images.length === 0) return null;

  const currentFile = images[currentIndex];

  function handleNext() {
    queryClient.invalidateQueries({ queryKey: medicalRecordQueryKeys.case(caseId) });
    queryClient.invalidateQueries({ queryKey: medicalRecordQueryKeys.images(caseId) });
    queryClient.invalidateQueries({ queryKey: ["case-clinic-services", caseId] });
    queryClient.invalidateQueries({ queryKey: ["invoices"] });
    removeImage(currentIndex);
  }

  async function handleSaveAll() {
    // Check if any image is still processing
    const hasUnprocessed = images.some((_, i) => !aiResults[i]);
    if (hasUnprocessed) {
      alert("Vui lòng chờ AI phân tích xong tất cả các ảnh trước khi lưu hàng loạt!");
      return;
    }

    // Validate all calipers
    for (let i = 0; i < images.length; i++) {
      const draft = drafts[i];
      if (draft) {
        const confirmedLesions = draft.lesions.filter(l => !l.rejected);
        const hasError = confirmedLesions.some(l => !checkIntersection(l.pair_a, l.pair_b));
        if (hasError) {
          alert(`Ảnh số ${i + 1} có thước đo chưa hợp lệ (chưa tạo thành hình). Vui lòng kiểm tra lại!`);
          return;
        }
      }
    }

    setIsSavingAll(true);
    try {
      // Process sequentially
      for (let i = 0; i < images.length; i++) {
        setSavingProgress(i + 1);
        const file = images[i];
        const draft = drafts[i];
        const result = aiResults[i];
        
        const confirmedLesions = draft ? draft.lesions.filter(l => !l.rejected) : [];
        const note = draft ? draft.note : "";
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const aiDetections = result ? result.detections : [];

        const burntFile = await generateBurntImage(file, confirmedLesions);
        if (!burntFile) throw new Error(`Không thể tạo burnt image cho ảnh số ${i + 1}`);

        const url = URL.createObjectURL(file);
        const img = new Image();
        img.src = url;
        await new Promise(r => img.onload = r);
        const w = img.width, h = img.height;
        URL.revokeObjectURL(url);

        const doctorBboxes = confirmedLesions.map(l => {
          const pts = [...l.pair_a, ...l.pair_b];
          const xs = pts.map(p => p.x / w);
          const ys = pts.map(p => p.y / h);
          return {
            xmin: Math.min(...xs),
            ymin: Math.min(...ys),
            xmax: Math.max(...xs),
            ymax: Math.max(...ys),
            confidence: 1.0
          };
        });

        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const mappedAiBboxes = aiDetections.map((d: any) => ({
          xmin: d.bbox.xmin,
          ymin: d.bbox.ymin,
          xmax: d.bbox.xmax,
          ymax: d.bbox.ymax,
          confidence: d.confidence
        }));

        const formData = new FormData();
        formData.append("OriginalImage", file);
        formData.append("BurntImage", burntFile);
        formData.append("AiPredictionsJson", JSON.stringify(mappedAiBboxes));
        formData.append("DoctorAnnotationsJson", JSON.stringify(doctorBboxes));
        formData.append("ModelVersionId", "00000000-0000-0000-0000-000000000000");
        if (note.trim()) {
          formData.append("Note", note.trim());
        }

        await apiClient.post(`/api/v1/cases/${caseId}/images/confirm`, formData, {
          headers: { "Content-Type": "multipart/form-data" },
          timeout: 60000,
        });
      }

      queryClient.invalidateQueries({ queryKey: medicalRecordQueryKeys.case(caseId) });
      queryClient.invalidateQueries({ queryKey: medicalRecordQueryKeys.images(caseId) });
      queryClient.invalidateQueries({ queryKey: ["case-clinic-services", caseId] });
      queryClient.invalidateQueries({ queryKey: ["invoices"] });
      clearSession();
      router.push(`/cases/${caseId}`);

    } catch (err: unknown) {
      alert("Lỗi khi lưu hàng loạt: " + (err instanceof Error ? err.message : String(err)));
    } finally {
      setIsSavingAll(false);
    }
  }

  return (
    <div className="fixed inset-0 z-[100] flex h-screen w-screen flex-col bg-background text-foreground select-none">
      {/* 1. Header Studio (Chuẩn Preclinic Light Mode 55px) */}
      <header className="flex h-[55px] shrink-0 items-center justify-between border-b border-border bg-background px-4">
        <div className="flex items-center gap-3">
          <Link
            href={`/cases/${caseId}`}
            className="flex h-8 items-center gap-1.5 rounded-[5px] border border-[#E7E8EB] px-2.5 text-sm text-foreground font-semibold font-sans hover:text-primary hover:bg-[#F5F6F8] transition-colors"
          >
            <ArrowLeft className="h-4 w-4" />
            <span>Quay lại ca khám</span>
          </Link>
          <div className="h-4 w-px bg-[#E7E8EB]" />
          <h2 className="text-sm font-bold tracking-wide text-[#2E37A4] uppercase font-sans">
            AI Ultrasound Diagnostic Studio
          </h2>
        </div>

        {/* Phân trang ảnh siêu âm và nút lưu tất cả */}
        <div className="flex items-center gap-4">
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={prevImage}
              disabled={currentIndex === 0}
              aria-label="Ảnh trước"
              className="flex h-7 w-7 items-center justify-center rounded-[5px] border border-[#E7E8EB] text-foreground font-semibold disabled:opacity-40 hover:text-primary hover:bg-[#F5F6F8] transition-colors"
            >
              <ChevronLeft className="h-4 w-4" />
            </button>
            <span className="text-sm font-mono font-semibold text-[#0A1B39]">
              Ảnh {images.length > 0 ? currentIndex + 1 : 0} / {images.length}
            </span>
            <button
              type="button"
              onClick={nextImage}
              disabled={currentIndex === images.length - 1}
              aria-label="Ảnh kế tiếp"
              className="flex h-7 w-7 items-center justify-center rounded-[5px] border border-[#E7E8EB] text-foreground font-semibold disabled:opacity-40 hover:text-primary hover:bg-[#F5F6F8] transition-colors"
            >
              <ChevronRight className="h-4 w-4" />
            </button>
          </div>
          
          <div className="h-4 w-px bg-[#E7E8EB]" />

          <button
            onClick={handleSaveAll}
            disabled={isSavingAll}
            className="flex h-8 items-center gap-2 rounded-[5px] bg-[#00C16A] px-3 text-sm font-medium font-sans text-white hover:bg-[#00a85c] disabled:opacity-50 transition-colors shadow-sm"
          >
            {isSavingAll ? (
              <>
                <Loader2 className="h-3 w-3 animate-spin" />
                Đang lưu {savingProgress}/{images.length}
              </>
            ) : (
              `Lưu tất cả (${images.length})`
            )}
          </button>
        </div>
      </header>

      {/* 3. Vùng Thao Tác Trung Tâm: Render nguyên bản DiagnosticCanvas đã có sẵn (2 panels) */}
      <main className="flex flex-1 overflow-hidden bg-background">
        <DiagnosticCanvas
          key={currentIndex}
          caseId={caseId}
          file={currentFile}
          onConfirm={handleNext}
        />
      </main>
    </div>
  );
}
