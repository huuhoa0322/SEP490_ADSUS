import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { useDoctorList } from "@/features/medical-record/hooks/use-doctors";

function makeWrapper(client: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

describe("useDoctorList", () => {
  it("tải danh sách bác sĩ khi enabled mặc định true", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/doctors`, () =>
        HttpResponse.json({
          code: 200,
          message: "ok",
          data: [{ userId: "doctor-1", fullName: "BS. Lê Minh Hoàng" }],
        }),
      ),
    );

    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result } = renderHook(() => useDoctorList(), { wrapper: makeWrapper(client) });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.[0].fullName).toBe("BS. Lê Minh Hoàng");
  });

  it("không gọi API khi enabled=false", async () => {
    let called = false;
    server.use(
      http.get(`${API_BASE_URL}/api/v1/doctors`, () => {
        called = true;
        return HttpResponse.json({ code: 200, message: "ok", data: [] });
      }),
    );

    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result } = renderHook(() => useDoctorList(false), { wrapper: makeWrapper(client) });

    // Query không kích hoạt nên vẫn ở trạng thái pending, không có cách chờ "sẽ không xảy ra"
    // ngoài xác nhận trạng thái ngay lập tức.
    expect(result.current.fetchStatus).toBe("idle");
    expect(called).toBe(false);
  });
});
