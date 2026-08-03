import type { Metadata } from "next";

import { AiModelForm } from "@/features/ai-model-management/components/ai-model-form";

export const metadata: Metadata = {
  title: "Đăng ký AI Model | ADSUS",
};

export default function NewAiModelPage() {
  return <AiModelForm />;
}
