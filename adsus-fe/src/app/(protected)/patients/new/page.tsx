import type { Metadata } from "next";

import { NewPatientFlow } from "@/features/medical-record/components/new-patient-flow";

export const metadata: Metadata = {
  title: "Thêm bệnh nhân | ADSUS",
};

/**
 * SCR-10 chế độ tạo — hai luồng dùng chung một route:
 *   /patients/new?patientUserId=X — tài khoản đã có, chỉ tạo hồ sơ nền (#17)
 *   /patients/new                 — Điều dưỡng tạo cả tài khoản (BE-4) rồi mới tạo hồ sơ nền
 */
export default async function NewPatientPage({
  searchParams,
}: {
  searchParams: Promise<{ patientUserId?: string }>;
}) {
  const { patientUserId } = await searchParams;

  return <NewPatientFlow patientUserId={patientUserId} />;
}
