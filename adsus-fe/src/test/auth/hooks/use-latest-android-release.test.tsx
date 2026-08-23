import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { describe, expect, it } from "vitest";

import { server } from "@/test/mocks/server";

import { useLatestAndroidRelease } from "@/features/auth/hooks/use-latest-android-release";

function Wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient();
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

describe("useLatestAndroidRelease", () => {
  it("route trả về downloadUrl thật — data là URL đó", async () => {
    server.use(
      http.get("/api/mobile-release", () =>
        HttpResponse.json({ downloadUrl: "https://github.com/.../adsus-mobile-1.0.0.apk" }),
      ),
    );

    const { result } = renderHook(() => useLatestAndroidRelease(), { wrapper: Wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toBe("https://github.com/.../adsus-mobile-1.0.0.apk");
  });

  it("route trả về downloadUrl null (chưa có release, hoặc token lỗi) — data là null, KHÔNG phải lỗi", async () => {
    server.use(
      http.get("/api/mobile-release", () => HttpResponse.json({ downloadUrl: null })),
    );

    const { result } = renderHook(() => useLatestAndroidRelease(), { wrapper: Wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toBeNull();
  });
});
