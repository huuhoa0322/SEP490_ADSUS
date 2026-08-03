import type { Metadata } from "next";

import { PrescribeMedicationForm } from "@/features/prescription-adherence/components/prescribe-medication-form";

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
 * Sau kê thành công → redirect đến trang detail của đơn.
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
        <PrescribeMedicationForm
          caseId={caseId}
          patientProfileId={patientId}
          onSuccess={(id) => {
            // Server component không có window — phải dùng Client wrapper cho redirect.
            // Inline redirect ở đây hoạt động với form nằm trong client component:
            //   dùng window.location ở client side sau khi mutate thành công.
            // Ở đây PrescribeMedicationForm handle redirect nội bộ rồi; page này
            // chỉ render form.
            if (typeof window !== "undefined") {
              window.location.href = `/patients/${patientId}/prescriptions/${id}`;
            }
          }}
        />
      </div>
    </div>
  );
}