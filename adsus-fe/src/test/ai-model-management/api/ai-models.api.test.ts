import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import {
  activateAiModel,
  calculateMap50,
  getActiveAiModel,
  getAiModelById,
  getAiModels,
  registerAiModel,
  updateAiModel,
} from "@/features/ai-model-management/api/ai-models.api";

const BASE = `${API_BASE_URL}/api/v1/ai-model-versions`;

const SAMPLE_MODEL = {
  modelVersionId: "model-1",
  versionCode: "YOLO26_v1",
  description: "Mô hình chính thức",
  metricsPrecision: 91.5,
  metricsMap50: 88.2,
  metricsRecall: 0.93,
  hfRepoId: "org/repo",
  hfFilename: "model.pt",
  status: "Inactive" as const,
  registeredAt: "2026-08-01T00:00:00Z",
  registeredBy: "admin-1",
};

describe("getAiModels", () => {
  it("trả về đúng danh sách kèm thông tin phân trang", async () => {
    const paged = { items: [SAMPLE_MODEL], page: 1, pageSize: 20, totalItems: 1, totalPages: 1 };

    server.use(
      http.get(BASE, () => HttpResponse.json({ code: 200, message: "OK", data: paged })),
    );

    await expect(getAiModels({ keyword: "yolo", page: 1, pageSize: 20 })).resolves.toEqual(paged);
  });

  it("data null trên response 200 — ném lỗi thay vì coi là danh sách rỗng hợp lệ", async () => {
    server.use(
      http.get(BASE, () => HttpResponse.json({ code: 200, message: "Backend bug.", data: null })),
    );

    await expect(getAiModels({})).rejects.toThrow("Backend bug.");
  });
});

describe("getAiModelById", () => {
  it("trả về đúng phiên bản model", async () => {
    server.use(
      http.get(`${BASE}/model-1`, () =>
        HttpResponse.json({ code: 200, message: "OK", data: SAMPLE_MODEL }),
      ),
    );

    await expect(getAiModelById("model-1")).resolves.toEqual(SAMPLE_MODEL);
  });

  it("404 — ném lỗi", async () => {
    server.use(
      http.get(`${BASE}/model-999`, () =>
        HttpResponse.json({ code: 404, message: "Not found.", data: null }, { status: 404 }),
      ),
    );

    await expect(getAiModelById("model-999")).rejects.toThrow();
  });
});

describe("getActiveAiModel — Doctor-facing, chỉ trả code/status (UC-20)", () => {
  it("trả về đúng phiên bản Active dạng rút gọn", async () => {
    server.use(
      http.get(`${BASE}/active`, () =>
        HttpResponse.json({
          code: 200,
          message: "OK",
          data: { versionCode: "YOLO26_v1", status: "Active" },
        }),
      ),
    );

    await expect(getActiveAiModel()).resolves.toEqual({ versionCode: "YOLO26_v1", status: "Active" });
  });

  it("data null (chưa có phiên bản nào Active) — trả về null, KHÔNG throw", async () => {
    server.use(
      http.get(`${BASE}/active`, () => HttpResponse.json({ code: 200, message: "OK", data: null })),
    );

    await expect(getActiveAiModel()).resolves.toBeNull();
  });
});

describe("registerAiModel", () => {
  it("gửi đúng payload và trả về model + message", async () => {
    let capturedBody: unknown;

    server.use(
      http.post(BASE, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json(
          { code: 201, message: "Đăng ký phiên bản AI mới thành công.", data: SAMPLE_MODEL },
          { status: 201 },
        );
      }),
    );

    const payload = {
      versionCode: "YOLO26_v1",
      hfRepoId: "org/repo",
      hfFilename: "model.pt",
      description: "Mô hình chính thức",
      metricsPrecision: 91.5,
      metricsMap50: 88.2,
      metricsRecall: 0.93,
    };

    const result = await registerAiModel(payload);

    expect(capturedBody).toEqual(payload);
    expect(result.data).toEqual(SAMPLE_MODEL);
    // translateApiMessage() chỉ dịch message tiếng Anh có trong bảng tra cứu — message tiếng
    // Việt từ backend không khớp entry nào nên trả về nguyên văn không đổi.
    expect(result.message).toBe("Đăng ký phiên bản AI mới thành công.");
  });

  it("dùng message mặc định khi backend không trả message", async () => {
    server.use(
      http.post(BASE, () =>
        HttpResponse.json({ code: 201, message: "", data: SAMPLE_MODEL }, { status: 201 }),
      ),
    );

    const result = await registerAiModel({ versionCode: "v", hfRepoId: "r", hfFilename: "f" });

    expect(result.message).toBe("Đã đăng ký phiên bản mô hình mới.");
  });

  it("data null trên response 200/201 — ném lỗi", async () => {
    server.use(
      http.post(BASE, () =>
        HttpResponse.json({ code: 201, message: "Backend bug.", data: null }, { status: 201 }),
      ),
    );

    await expect(
      registerAiModel({ versionCode: "v", hfRepoId: "r", hfFilename: "f" }),
    ).rejects.toThrow("Backend bug.");
  });
});

describe("updateAiModel", () => {
  it("gửi đúng payload tới endpoint sửa", async () => {
    let capturedBody: unknown;

    server.use(
      http.put(`${BASE}/model-1`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({ code: 200, message: "Cập nhật phiên bản thành công.", data: null });
      }),
    );

    const payload = { description: "Mới", hfRepoId: "org/repo2", hfFilename: "m2.pt" };
    await updateAiModel("model-1", payload);

    expect(capturedBody).toEqual(payload);
  });
});

describe("activateAiModel", () => {
  it("gửi PATCH kèm { status: \"ACTIVE\" }", async () => {
    let capturedBody: unknown;

    server.use(
      http.patch(`${BASE}/model-1`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({ code: 200, message: "Kích hoạt phiên bản thành công.", data: null });
      }),
    );

    await activateAiModel("model-1");

    expect(capturedBody).toEqual({ status: "ACTIVE" });
  });
});

describe("calculateMap50", () => {
  it("gọi đúng endpoint /calculate-map50", async () => {
    let requestedUrl: string | undefined;

    server.use(
      http.post(`${BASE}/model-1/calculate-map50`, ({ request }) => {
        requestedUrl = request.url;
        return HttpResponse.json({ code: 200, message: "Tính toán mAP50 thành công.", data: null });
      }),
    );

    await calculateMap50("model-1");

    expect(requestedUrl).toContain("/calculate-map50");
  });
});
