import type { Metadata } from "next";

import { PrescriptionHistoryView } from "@/features/prescription-adherence/components/prescription-history-view";

export const metadata: Metadata = {
  title: "Lịch sử đơn thuốc | ADSUS",
};

interface PageProps {
  params: Promise<{ patientId: string }>;
}

/**
 * Module 7 UC-11 — trang lịch sử đơn thuốc cho Doctor/Nurse.
 * Next.js 16: params là Promise → phải await (BREAKING change so với Next.js 14).
 */
export default async function PatientPrescriptionsPage({ params }: PageProps) {
  const { patientId } = await params;
  return (
    <div className="mx-auto max-w-5xl px-6 py-10">
      <h1 className="font-heading text-[32px] font-bold tracking-[-0.02em] text-[#223a66]">
        Lịch sử đơn thuốc
      </h1>
      <p className="mt-2 text-muted-foreground">
        Theo dõi đơn thuốc và tỉ lệ tuân thủ điều trị của bệnh nhân.
      </p>
      <div className="mt-8">
        <PrescriptionHistoryView patientProfileId={patientId} />
      </div>
    </div>
  );
}