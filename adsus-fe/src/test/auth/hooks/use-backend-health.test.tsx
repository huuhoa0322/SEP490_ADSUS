import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { useBackendHealth } from "@/features/auth/hooks/use-backend-health";

function Wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient();
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

describe("useBackendHealth", () => {
  it("GET /api/health thành công — isSuccess=true", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/health`, () => HttpResponse.json({ status: "ok" })),
    );

    const { result } = renderHook(() => useBackendHealth(), { wrapper: Wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
  });

  it("/api/health lỗi (500) — isError=true sau khi hết lượt retry", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/health`, () =>
        HttpResponse.json({}, { status: 500 }),
      ),
    );

    const { result } = renderHook(() => useBackendHealth(), { wrapper: Wrapper });

    // Hook tự retry 1 lần (retry: 1) trước khi settle lỗi — react-query đợi ~1s giữa 2 lần
    // thử, nên timeout mặc định của waitFor không đủ, phải nới ra.
    await waitFor(() => expect(result.current.isError).toBe(true), { timeout: 5000 });
  });
});
