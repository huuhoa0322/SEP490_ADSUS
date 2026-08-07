import type { Metadata } from "next";

import { CaseDetailView } from "@/features/medical-record/components/case-detail-view";

export const metadata: Metadata = {
  title: "Chi tiết ca khám | ADSUS",
};

/** SCR-30 — UC-08, UC-12. */
export default async function CaseDetailPage({
  params,
}: {
  params: Promise<{ caseId: string }>;
}) {
  const { caseId } = await params;

  return <CaseDetailView caseId={caseId} />;
}
