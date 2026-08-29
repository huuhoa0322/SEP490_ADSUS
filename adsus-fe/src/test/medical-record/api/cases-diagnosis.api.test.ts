// @vitest-environment node
//
// Multipart/FormData: PHẢI chạy dưới Node, cùng lý do đã ghi ở cases.api.upload.test.ts — lớp
// File/FormData của jsdom nằm ở realm khác với undici (MSW dùng để bóc thân multipart).
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { analyzeImage, confirmAnalysis } from "@/features/medical-record/api/cases-diagnosis.api";

function fakeImage(name: string): File {
  return new File([new Uint8Array([0xff, 0xd8, 0xff, 0xe0])], name, { type: "image/jpeg" });
}

describe("analyzeImage", () => {
  it("gửi ảnh dưới khoá 'image' và map session_id/detections về camelCase", async () => {
    let receivedFileName = "";
    server.use(
      http.post(`${API_BASE_URL}/api/v1/cases/case-1/analyze`, async ({ request }) => {
        const form = await request.formData();
        receivedFileName = (form.get("image") as File).name;
        return HttpResponse.json({
          code: 200,
          message: "ok",
          data: { session_id: "sess-1", detections: [] },
        });
      }),
    );

    const result = await analyzeImage("case-1", fakeImage("a.jpg"));

    expect(receivedFileName).toBe("a.jpg");
    expect(result).toEqual({ sessionId: "sess-1", detections: [] });
  });

  it("session_id rỗng thì mặc định 'completed', detections rỗng thì mặc định []", async () => {
    server.use(
      http.post(`${API_BASE_URL}/api/v1/cases/case-1/analyze`, () =>
        HttpResponse.json({ code: 200, message: "ok", data: {} }),
      ),
    );

    const result = await analyzeImage("case-1", fakeImage("a.jpg"));

    expect(result).toEqual({ sessionId: "completed", detections: [] });
  });

  it("throw khi API trả data null trên response 200", async () => {
    server.use(
      http.post(`${API_BASE_URL}/api/v1/cases/case-1/analyze`, () =>
        HttpResponse.json({ code: 200, message: "AI Backend đang tắt", data: null }),
      ),
    );

    await expect(analyzeImage("case-1", fakeImage("a.jpg"))).rejects.toThrow(
      "AI Backend đang tắt",
    );
  });
});

describe("confirmAnalysis", () => {
  const validInput = {
    originalImage: fakeImage("orig.jpg"),
    burntImage: fakeImage("burnt.jpg"),
    aiPredictions: [{ xmin: 0, ymin: 0, xmax: 1, ymax: 1, confidence: 0.9 }],
    doctorAnnotations: [{ xmin: 0, ymin: 0, xmax: 1, ymax: 1, confidence: 1 }],
  };

  it("gửi đủ field bắt buộc dưới đúng tên khoá backend mong đợi", async () => {
    let form: FormData | null = null;
    server.use(
      http.post(`${API_BASE_URL}/api/v1/cases/case-1/images/confirm`, async ({ request }) => {
        form = await request.formData();
        return HttpResponse.json({ code: 200, message: "ok", data: null });
      }),
    );

    await confirmAnalysis("case-1", validInput);

    expect((form!.get("OriginalImage") as File).name).toBe("orig.jpg");
    expect((form!.get("BurntImage") as File).name).toBe("burnt.jpg");
    expect(JSON.parse(form!.get("AiPredictionsJson") as string)).toEqual(validInput.aiPredictions);
    expect(JSON.parse(form!.get("DoctorAnnotationsJson") as string)).toEqual(
      validInput.doctorAnnotations,
    );
    expect(form!.get("ModelVersionId")).toBe("00000000-0000-0000-0000-000000000000");
    // Note không được truyền → không append field, không gửi chuỗi rỗng.
    expect(form!.get("Note")).toBeNull();
  });

  it("có note thì gửi kèm field Note", async () => {
    let form: FormData | null = null;
    server.use(
      http.post(`${API_BASE_URL}/api/v1/cases/case-1/images/confirm`, async ({ request }) => {
        form = await request.formData();
        return HttpResponse.json({ code: 200, message: "ok", data: null });
      }),
    );

    await confirmAnalysis("case-1", { ...validInput, note: "Nghi ngờ u lành" });

    expect(form!.get("Note")).toBe("Nghi ngờ u lành");
  });

  it("throw khi backend trả code khác 200", async () => {
    server.use(
      http.post(`${API_BASE_URL}/api/v1/cases/case-1/images/confirm`, () =>
        HttpResponse.json({ code: 422, message: "Ảnh không hợp lệ", data: null }),
      ),
    );

    await expect(confirmAnalysis("case-1", validInput)).rejects.toThrow("Ảnh không hợp lệ");
  });
});
