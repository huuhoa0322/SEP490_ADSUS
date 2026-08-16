"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState, useRef, use } from "react";
import Link from "next/link";
import { useDiagnosticStore } from "@/features/medical-record/stores/use-diagnostic-store";
import { useCaseDetail } from "@/features/medical-record/hooks/use-cases";
import { DiagnosticCanvas } from "@/features/medical-record/components/diagnostic-canvas";
import { useQueryClient } from "@tanstack/react-query";
import { medicalRecordQueryKeys } from "@/features/medical-record/hooks/query-keys";

export default function DiagnosticPage({ params }: { params: Promise<{ caseId: string }> }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { caseId } = use(params);
  const { data: medicalCase, isLoading } = useCaseDetail(caseId);
  const { images, currentIndex, nextImage, clearSession } = useDiagnosticStore();
  
  const [activeModel, setActiveModel] = useState("yolo-efficientNetv2-m1-nbl.pt"); // Mock model for badge
  
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

    if (isLastImage) {
      clearSession();
      router.push(`/cases/${caseId}`);
    } else {
      nextImage();
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
          <div className="text-sm font-medium">
            Ảnh {currentIndex + 1} / {images.length}
          </div>
        </div>
      </header>

      {/* MAIN DIAGNOSTIC WORKSPACE */}
      <main className="flex flex-1 overflow-hidden">
        <DiagnosticCanvas 
          caseId={caseId} 
          file={currentFile} 
          onConfirm={handleNext} 
        />
      </main>
    </div>
  );
}
