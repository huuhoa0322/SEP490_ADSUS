import type { Metadata } from "next";

import { AiModelList } from "@/features/ai-model-management/components/ai-model-list";

export const metadata: Metadata = {
  title: "Quản lý AI Models | ADSUS",
};

// UC-20 Quản lý mô hình AI
export default function AiModelsPage() {
  return <AiModelList />;
}
