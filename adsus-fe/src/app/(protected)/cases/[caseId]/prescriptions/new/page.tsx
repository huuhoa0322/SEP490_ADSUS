import type { Metadata } from "next";

import { PrescribePageClient } from "@/features/prescription-adherence/components/prescribe-page-client";

export const metadata: Metadata = {
  title: "Kê đơn thuốc | ADSUS",
};

interface PageProps {
  params: Promise<{ caseId: string }>;
  searchParams: Promise<{ patientId?: string }>;
}

/**
 * Module 7 UC-18 — trang kê đơn mới (Doctor only).
 * Case phải ở trạng thái CONFIRMED — BE Service enforce (BR-04).
 * Page server render + đẩy cho PrescribePageClient để xử lý redirect sau khi kê.
 */
export default async function NewPrescriptionPage({ params, searchParams }: PageProps) {
  const { caseId } = await params;
  const { patientId } = await searchParams;

  if (!patientId) {
    return (
      <div className="mx-auto max-w-3xl px-6 py-10">
        <p className="text-destructive">Thiếu patientId trên URL.</p>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-3xl px-6 py-10">
      <h1 className="font-heading text-[32px] font-bold tracking-[-0.02em] text-[#223a66]">
        Kê đơn thuốc
      </h1>
      <p className="mt-2 text-muted-foreground">
        Đơn thuốc sẽ được gắn với ca khám và bác sĩ đang đăng nhập.
      </p>
      <div className="mt-8">
        <PrescribePageClient caseId={caseId} patientProfileId={patientId} />
      </div>
    </div>
  );
}