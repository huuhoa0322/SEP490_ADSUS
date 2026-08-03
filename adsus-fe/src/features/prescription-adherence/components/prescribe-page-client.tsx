"use client";

import { useRouter } from "next/navigation";

import { PrescribeMedicationForm } from "./prescribe-medication-form";

interface PrescribePageClientProps {
  caseId: string;
  patientProfileId: string;
}

/**
 * Client wrapper — server component page không truy cập được router.
 * Khi form kê đơn thành công → redirect sang trang detail đơn thuốc.
 */
export function PrescribePageClient({ caseId, patientProfileId }: PrescribePageClientProps) {
  const router = useRouter();
  return (
    <PrescribeMedicationForm
      caseId={caseId}
      patientProfileId={patientProfileId}
      onSuccess={(id) => router.push(`/patients/${patientProfileId}/prescriptions/${id}`)}
    />
  );
}