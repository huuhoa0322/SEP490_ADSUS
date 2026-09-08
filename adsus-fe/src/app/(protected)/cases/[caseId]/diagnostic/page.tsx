"use client";

import { use, useEffect } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { useDiagnosticStore } from "@/features/medical-record/stores/use-diagnostic-store";
import { useBackgroundAi } from "@/features/medical-record/hooks/use-background-ai";
import { DiagnosticCanvas } from "@/features/medical-record/components/diagnostic-canvas";
import { ArrowLeft, ChevronLeft, ChevronRight } from "lucide-react";
import { medicalRecordQueryKeys } from "@/features/medical-record/hooks/query-keys";

export default function DiagnosticPage({ params }: { params: Promise<{ caseId: string }> }) {
  const { caseId } = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();
  const { images, currentIndex, prevImage, nextImage, removeImage } = useDiagnosticStore();

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
    removeImage(currentIndex);
  }

  return (
    <div className="fixed inset-0 z-[100] flex h-screen w-screen flex-col bg-white text-[#0A1B39] select-none">
      {/* 1. Header Studio (Chuẩn Preclinic Light Mode 55px) */}
      <header className="flex h-[55px] shrink-0 items-center justify-between border-b border-[#E7E8EB] bg-[#FFFFFF] px-4">
        <div className="flex items-center gap-3">
          <Link
            href={`/cases/${caseId}`}
            className="flex h-8 items-center gap-1.5 rounded-[5px] border border-[#E7E8EB] px-2.5 text-xs text-[#6C7688] hover:bg-[#F5F6F8] hover:text-[#0A1B39] transition-colors"
          >
            <ArrowLeft className="h-4 w-4" />
            <span>Quay lại ca khám</span>
          </Link>
          <div className="h-4 w-px bg-[#E7E8EB]" />
          <h2 className="text-xs font-bold tracking-wide text-[#2E37A4] uppercase">
            AI Ultrasound Diagnostic Studio
          </h2>
        </div>

        {/* Phân trang ảnh siêu âm */}
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={prevImage}
            disabled={currentIndex === 0}
            aria-label="Ảnh trước"
            className="flex h-7 w-7 items-center justify-center rounded-[5px] border border-[#E7E8EB] text-[#6C7688] hover:bg-[#F5F6F8] hover:text-[#0A1B39] disabled:opacity-30 transition-colors"
          >
            <ChevronLeft className="h-4 w-4" />
          </button>
          <span className="text-xs font-mono text-[#0A1B39]">
            Ảnh {images.length > 0 ? currentIndex + 1 : 0} / {images.length}
          </span>
          <button
            type="button"
            onClick={nextImage}
            disabled={currentIndex === images.length - 1}
            aria-label="Ảnh kế tiếp"
            className="flex h-7 w-7 items-center justify-center rounded-[5px] border border-[#E7E8EB] text-[#6C7688] hover:bg-[#F5F6F8] hover:text-[#0A1B39] disabled:opacity-30 transition-colors"
          >
            <ChevronRight className="h-4 w-4" />
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
