"use client";

import { useRouter } from "next/navigation";
import { useEffect, use, useState } from "react";
import { useDiagnosticStore } from "@/features/medical-record/stores/use-diagnostic-store";
import { useCaseDetail } from "@/features/medical-record/hooks/use-cases";
import { DiagnosticCanvas } from "@/features/medical-record/components/diagnostic-canvas";
import { useQueryClient } from "@tanstack/react-query";
import { medicalRecordQueryKeys } from "@/features/medical-record/hooks/query-keys";
import { useActiveAiModel } from "@/features/ai-model-management/hooks/use-ai-models";
import { useBackgroundAi } from "@/features/medical-record/hooks/use-background-ai";
import { apiClient } from "@/lib/api-client";
import { checkIntersection, generateBurntImage } from "@/features/medical-record/utils/canvas-utils";
import { Loader2 } from "lucide-react";

export default function DiagnosticPage({ params }: { params: Promise<{ caseId: string }> }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { caseId } = use(params);
  const { data: medicalCase, isLoading } = useCaseDetail(caseId);
  const { images, currentIndex, nextImage, prevImage, clearSession, removeImage, drafts, aiResults } = useDiagnosticStore();
  
  const { data: activeModelData } = useActiveAiModel();
  const activeModel = activeModelData?.versionCode || "Không rõ";

  useBackgroundAi();
  
  const [isSavingAll, setIsSavingAll] = useState(false);
  const [savingProgress, setSavingProgress] = useState(0);

  useEffect(() => {
    // If no images in store, redirect back to case details
    if (images.length === 0) {
      router.push(`/cases/${caseId}`);
    }
  }, [images.length, caseId, router]);

  if (images.length === 0) return null;
  if (isLoading) return <div className="p-8">Đang tải thông tin...</div>;

  const currentFile = images[currentIndex];
  const isLastImage = currentIndex === images.length - 1;

  function handleCancel() {
    clearSession();
    router.push(`/cases/${caseId}`);
  }

  function handleNext() {
    // Invalidate the cache for the case and its images so the UI is fresh when we return
    queryClient.invalidateQueries({ queryKey: medicalRecordQueryKeys.case(caseId) });
    queryClient.invalidateQueries({ queryKey: medicalRecordQueryKeys.images(caseId) });

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
      // Process sequentially to avoid overwhelming the server/browser
      for (let i = 0; i < images.length; i++) {
        setSavingProgress(i + 1);
        const file = images[i];
        const draft = drafts[i];
        const result = aiResults[i];
        
        // Use empty array if draft doesn't exist
        const confirmedLesions = draft ? draft.lesions.filter(l => !l.rejected) : [];
        const note = draft ? draft.note : "";
        const aiDetections = result ? result.detections : [];

        // Generate burnt image using off-screen canvas
        const burntFile = await generateBurntImage(file, confirmedLesions);
        if (!burntFile) throw new Error(`Không thể tạo burnt image cho ảnh số ${i + 1}`);

        // Image loading logic inside generateBurntImage gives us natural dims, but we need them for normalization
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

      // Done
      queryClient.invalidateQueries({ queryKey: medicalRecordQueryKeys.case(caseId) });
      queryClient.invalidateQueries({ queryKey: medicalRecordQueryKeys.images(caseId) });
      clearSession();
      router.push(`/cases/${caseId}`);

    } catch (err: any) {
      alert("Lỗi khi lưu hàng loạt: " + (err.message || String(err)));
    } finally {
      setIsSavingAll(false);
    }
  }

  return (
    <div className="flex h-[calc(100vh-64px)] flex-col bg-background overflow-hidden">
      {/* HEADER */}
      <header className="flex h-16 shrink-0 items-center justify-between border-b border-border bg-card px-6">
        <div className="flex items-center gap-4">
          <button onClick={handleCancel} className="text-sm font-medium text-muted-foreground hover:text-foreground">
            ← Quay lại
          </button>
          <div className="h-6 w-px bg-border" />
          <div>
            <h1 className="font-heading font-semibold text-foreground">
              {medicalCase?.patientProfile?.fullName || "Bệnh nhân ẩn danh"}
            </h1>
            <p className="text-xs text-muted-foreground">
              Tuổi: {medicalCase?.patientProfile?.dateOfBirth ? new Date().getFullYear() - new Date(medicalCase.patientProfile.dateOfBirth).getFullYear() : "?"} • Mã ca: {caseId.slice(0, 8)}
            </p>
          </div>
        </div>

        <div className="flex items-center gap-4">
          <div className="flex items-center gap-2 rounded-full border border-border bg-muted/50 px-3 py-1 text-xs">
            <span className="relative flex h-2 w-2">
              <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-75"></span>
              <span className="relative inline-flex h-2 w-2 rounded-full bg-emerald-500"></span>
            </span>
            AI Active: <span className="font-mono font-medium">{activeModel}</span>
          </div>
          
          <div className="flex items-center gap-3 rounded-md bg-muted px-2 py-1">
            <button 
              onClick={() => prevImage()} 
              disabled={currentIndex === 0}
              className="flex h-7 w-7 items-center justify-center rounded text-muted-foreground hover:bg-background hover:text-foreground disabled:opacity-30 disabled:hover:bg-transparent"
              title="Ảnh trước"
            >
              ◀
            </button>
            <div className="text-sm font-medium tabular-nums">
              Ảnh {currentIndex + 1} / {images.length}
            </div>
            <button 
              onClick={() => nextImage()} 
              disabled={isLastImage}
              className="flex h-7 w-7 items-center justify-center rounded text-muted-foreground hover:bg-background hover:text-foreground disabled:opacity-30 disabled:hover:bg-transparent"
              title="Ảnh sau"
            >
              ▶
            </button>
          </div>

          <button
            onClick={handleSaveAll}
            disabled={isSavingAll}
            className="flex items-center gap-2 rounded-md bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
          >
            {isSavingAll ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin" />
                Đang lưu {savingProgress}/{images.length}
              </>
            ) : (
              `Lưu tất cả (${images.length})`
            )}
          </button>
        </div>
      </header>

      {/* MAIN DIAGNOSTIC WORKSPACE */}
      <main className="flex flex-1 overflow-hidden">
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
