import type { Metadata } from "next";

import { CreateCaseForm } from "@/features/medical-record/components/create-case-form";

export const metadata: Metadata = {
  title: "Tạo ca khám | ADSUS",
};

/** SCR-11 — UC-07. */
export default async function CreateCasePage({
  params,
}: {
  params: Promise<{ profileId: string }>;
}) {
  const { profileId } = await params;

  return <CreateCaseForm patientProfileId={profileId} />;
}
