import type { Metadata } from "next";

import { PatientProfileForm } from "@/features/medical-record/components/patient-profile-form";

export const metadata: Metadata = {
  title: "Hồ sơ nền bệnh nhân | ADSUS",
};

/** SCR-10 chế độ sửa — UC-06. */
export default async function EditPatientProfilePage({
  params,
}: {
  params: Promise<{ profileId: string }>;
}) {
  const { profileId } = await params;

  return <PatientProfileForm mode="edit" profileId={profileId} />;
}
