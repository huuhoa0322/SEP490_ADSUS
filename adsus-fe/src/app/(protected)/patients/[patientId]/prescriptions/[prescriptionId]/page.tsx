import type { Metadata } from "next";

import { PrescriptionDetail } from "@/features/prescription-adherence/components/prescription-detail";

export const metadata: Metadata = {
  title: "Chi tiết đơn thuốc | ADSUS",
};

interface PageProps {
  params: Promise<{ patientId: string; prescriptionId: string }>;
}

/**
 * Module 7 UC-11 — trang chi tiết 1 đơn thuốc.
 * Next.js 16: params + searchParams là Promise, phải await.
 */
export default async function PrescriptionDetailPage({ params }: PageProps) {
  const { patientId, prescriptionId } = await params;
  return (
    <div className="mx-auto max-w-5xl px-6 py-10">
      <a
        href={`/patients/${patientId}/prescriptions`}
        className="mb-4 inline-block text-sm text-[#4488be] hover:underline"
      >
        ← Quay lại danh sách
      </a>
      <PrescriptionDetail prescriptionId={prescriptionId} />
    </div>
  );
}
