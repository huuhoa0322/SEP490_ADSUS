import type { Metadata } from "next";

import { AiModelForm } from "@/features/ai-model-management/components/ai-model-form";

export const metadata: Metadata = {
  title: "Sửa AI Model | ADSUS",
};

export default async function EditAiModelPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const resolvedParams = await params;
  return <AiModelForm id={resolvedParams.id} />;
}
