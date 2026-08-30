import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import {
  useActivateAiModel,
  useActiveAiModel,
  useAiModelDetail,
  useAiModelList,
  useCalculateMap50,
  useRegisterAiModel,
  useUpdateAiModel,
} from "@/features/ai-model-management/hooks/use-ai-models";

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

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  }
  return Wrapper;
}

describe("useAiModelList", () => {
  it("tải danh sách qua getAiModels, expose page/pageSize/totalPages từ PagedResult", async () => {
    server.use(
      http.get(BASE, () =>
        HttpResponse.json({
          code: 200,
          message: "OK",
          data: { items: [SAMPLE_MODEL], page: 2, pageSize: 10, totalItems: 15, totalPages: 2 },
        }),
      ),
    );

    const { result } = renderHook(
      () => useAiModelList({ page: 2, pageSize: 10 }),
      { wrapper: createWrapper() },
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.items).toEqual([SAMPLE_MODEL]);
    expect(result.current.data?.page).toBe(2);
    expect(result.current.data?.totalPages).toBe(2);
  });
});

describe("useActiveAiModel", () => {
  it("tải đúng phiên bản Active rút gọn (UC-20)", async () => {
    server.use(
      http.get(`${BASE}/active`, () =>
        HttpResponse.json({ code: 200, message: "OK", data: { versionCode: "YOLO26_v1", status: "Active" } }),
      ),
    );

    const { result } = renderHook(() => useActiveAiModel(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual({ versionCode: "YOLO26_v1", status: "Active" });
  });
});

describe("useAiModelDetail", () => {
  it("không gọi API khi id là undefined (enabled: false)", () => {
    let calls = 0;
    server.use(
      http.get(`${BASE}/:id`, () => {
        calls += 1;
        return HttpResponse.json({ code: 200, message: "OK", data: SAMPLE_MODEL });
      }),
    );

    const { result } = renderHook(() => useAiModelDetail(undefined), { wrapper: createWrapper() });

    expect(result.current.fetchStatus).toBe("idle");
    expect(calls).toBe(0);
  });

  it("có id — tải đúng phiên bản", async () => {
    server.use(
      http.get(`${BASE}/model-1`, () =>
        HttpResponse.json({ code: 200, message: "OK", data: SAMPLE_MODEL }),
      ),
    );

    const { result } = renderHook(() => useAiModelDetail("model-1"), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(SAMPLE_MODEL);
  });
});

function makeSharedWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  }
  return Wrapper;
}

describe("useRegisterAiModel — làm mới danh sách sau khi đăng ký", () => {
  it("đăng ký thành công — danh sách được gọi lại (invalidateQueries)", async () => {
    let listCallCount = 0;
    server.use(
      http.get(BASE, () => {
        listCallCount += 1;
        return HttpResponse.json({
          code: 200,
          message: "OK",
          data: { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 1 },
        });
      }),
      http.post(BASE, () =>
        HttpResponse.json(
          { code: 201, message: "Đăng ký thành công.", data: SAMPLE_MODEL },
          { status: 201 },
        ),
      ),
    );

    const Wrapper = makeSharedWrapper();
    const list = renderHook(() => useAiModelList({}), { wrapper: Wrapper });
    await waitFor(() => expect(list.result.current.isSuccess).toBe(true));
    expect(listCallCount).toBe(1);

    const register = renderHook(() => useRegisterAiModel(), { wrapper: Wrapper });
    register.result.current.mutate({ versionCode: "v", hfRepoId: "r", hfFilename: "f" });
    await waitFor(() => expect(register.result.current.isSuccess).toBe(true));

    await waitFor(() => expect(listCallCount).toBe(2));
  });
});

describe("useUpdateAiModel — làm mới cả detail lẫn danh sách", () => {
  it("cập nhật thành công — cả detail(id) và list đều bị invalidate", async () => {
    let listCallCount = 0;
    let detailCallCount = 0;
    server.use(
      http.get(BASE, () => {
        listCallCount += 1;
        return HttpResponse.json({
          code: 200,
          message: "OK",
          data: { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 1 },
        });
      }),
      http.get(`${BASE}/model-1`, () => {
        detailCallCount += 1;
        return HttpResponse.json({ code: 200, message: "OK", data: SAMPLE_MODEL });
      }),
      http.put(`${BASE}/model-1`, () =>
        HttpResponse.json({ code: 200, message: "Cập nhật thành công.", data: null }),
      ),
    );

    const Wrapper = makeSharedWrapper();
    const list = renderHook(() => useAiModelList({}), { wrapper: Wrapper });
    const detail = renderHook(() => useAiModelDetail("model-1"), { wrapper: Wrapper });
    await waitFor(() => expect(list.result.current.isSuccess).toBe(true));
    await waitFor(() => expect(detail.result.current.isSuccess).toBe(true));
    expect(listCallCount).toBe(1);
    expect(detailCallCount).toBe(1);

    const update = renderHook(() => useUpdateAiModel(), { wrapper: Wrapper });
    update.result.current.mutate({ id: "model-1", payload: { description: "Mới", hfRepoId: "r", hfFilename: "f" } });
    await waitFor(() => expect(update.result.current.isSuccess).toBe(true));

    await waitFor(() => expect(listCallCount).toBe(2));
    await waitFor(() => expect(detailCallCount).toBe(2));
  });
});

describe("useActivateAiModel — làm mới danh sách sau khi kích hoạt", () => {
  it("kích hoạt thành công — danh sách được gọi lại", async () => {
    let listCallCount = 0;
    server.use(
      http.get(BASE, () => {
        listCallCount += 1;
        return HttpResponse.json({
          code: 200,
          message: "OK",
          data: { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 1 },
        });
      }),
      http.patch(`${BASE}/model-1`, () =>
        HttpResponse.json({ code: 200, message: "Kích hoạt thành công.", data: null }),
      ),
    );

    const Wrapper = makeSharedWrapper();
    const list = renderHook(() => useAiModelList({}), { wrapper: Wrapper });
    await waitFor(() => expect(list.result.current.isSuccess).toBe(true));
    expect(listCallCount).toBe(1);

    const activate = renderHook(() => useActivateAiModel(), { wrapper: Wrapper });
    activate.result.current.mutate("model-1");
    await waitFor(() => expect(activate.result.current.isSuccess).toBe(true));

    await waitFor(() => expect(listCallCount).toBe(2));
  });
});

describe("useCalculateMap50 — làm mới danh sách sau khi tính lại mAP50", () => {
  it("tính toán thành công — danh sách được gọi lại", async () => {
    let listCallCount = 0;
    server.use(
      http.get(BASE, () => {
        listCallCount += 1;
        return HttpResponse.json({
          code: 200,
          message: "OK",
          data: { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 1 },
        });
      }),
      http.post(`${BASE}/model-1/calculate-map50`, () =>
        HttpResponse.json({ code: 200, message: "Tính toán mAP50 thành công.", data: null }),
      ),
    );

    const Wrapper = makeSharedWrapper();
    const list = renderHook(() => useAiModelList({}), { wrapper: Wrapper });
    await waitFor(() => expect(list.result.current.isSuccess).toBe(true));
    expect(listCallCount).toBe(1);

    const calc = renderHook(() => useCalculateMap50(), { wrapper: Wrapper });
    calc.result.current.mutate("model-1");
    await waitFor(() => expect(calc.result.current.isSuccess).toBe(true));

    await waitFor(() => expect(listCallCount).toBe(2));
  });
});
