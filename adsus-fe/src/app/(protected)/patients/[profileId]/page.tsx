import type { Metadata } from "next";

import { PatientRecordView } from "@/features/medical-record/components/patient-record-view";

export const metadata: Metadata = {
  title: "Hồ sơ bệnh án | ADSUS",
};

/** SCR-12 — UC-08. Hồ sơ nền + danh sách lần khám của một bệnh nhân. */
export default async function PatientRecordPage({
  params,
}: {
  params: Promise<{ profileId: string }>;
}) {
  const { profileId } = await params;

  return <PatientRecordView profileId={profileId} />;
}
