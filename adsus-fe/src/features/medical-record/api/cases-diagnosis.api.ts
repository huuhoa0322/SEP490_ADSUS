import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";

import type { AiDetection } from "../stores/use-diagnostic-store";

const BASE = "/api/v1/cases";

export interface AnalyzeImageResult {
  sessionId: string;
  detections: AiDetection[];
}

interface RawAnalyzeImageData {
  session_id?: string;
  detections?: AiDetection[];
}

/**
 * UC-19 — gửi 1 ảnh siêu âm cho AI Backend phân tích. Dùng chung cho cả nút "Chạy AI" bấm tay
 * (`diagnostic-canvas.tsx`) lẫn hàng đợi tự chạy nền (`use-background-ai.ts`) — trước 29/08/2026
 * hai nơi này tự gọi `apiClient` riêng, trùng lặp y hệt logic dưới đây.
 */
export async function analyzeImage(caseId: string, image: File): Promise<AnalyzeImageResult> {
  const form = new FormData();
  form.append("image", image);

  // Không tự đặt header Content-Type: axios cần tự sinh boundary cho FormData, đặt tay sẽ làm
  // hỏng request (cùng lý do đã ghi ở createCase trong cases.api.ts).
  const { data } = await apiClient.post<ApiResponse<RawAnalyzeImageData>>(
    `${BASE}/${caseId}/analyze`,
    form,
  );

  if (!data.data) throw new Error(data.message || "Phân tích ảnh AI thất bại.");

  return {
    sessionId: data.data.session_id || "completed",
    detections: data.data.detections || [],
  };
}

export interface ConfirmAnalysisBbox {
  xmin: number;
  ymin: number;
  xmax: number;
  ymax: number;
  confidence: number;
}

export interface ConfirmAnalysisInput {
  originalImage: File;
  burntImage: File;
  aiPredictions: ConfirmAnalysisBbox[];
  doctorAnnotations: ConfirmAnalysisBbox[];
  note?: string;
}

/**
 * UC-19 — bác sĩ xác nhận kết quả phân tích: ghi nhận ảnh đã "đánh dấu" (burnt) + toạ độ AI
 * lẫn bác sĩ để tính IoU/mAP50 (CaseDiagnosisService.ConfirmAnalysisAsync ở backend).
 */
export async function confirmAnalysis(caseId: string, input: ConfirmAnalysisInput): Promise<void> {
  const form = new FormData();
  form.append("OriginalImage", input.originalImage);
  form.append("BurntImage", input.burntImage);
  form.append("AiPredictionsJson", JSON.stringify(input.aiPredictions));
  form.append("DoctorAnnotationsJson", JSON.stringify(input.doctorAnnotations));
  form.append("ModelVersionId", "00000000-0000-0000-0000-000000000000");
  if (input.note) form.append("Note", input.note);

  const { data } = await apiClient.post<ApiResponse<null>>(`${BASE}/${caseId}/images/confirm`, form, {
    timeout: 60000, // Supabase uploads can take lâu hơn 15s mặc định
  });

  if (data.code !== 200) throw new Error(data.message || "Lưu ảnh thất bại.");
}
