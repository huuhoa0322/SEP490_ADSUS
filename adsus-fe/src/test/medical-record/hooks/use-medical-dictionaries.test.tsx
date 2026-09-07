import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { useAllergyTypes, useDiseases } from "@/features/medical-record/hooks/use-medical-dictionaries";

function makeWrapper(client: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

describe("useDiseases", () => {
  it("tải danh mục bệnh nền", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/medical-dictionaries/diseases`, () =>
        HttpResponse.json({
          code: 200,
          message: "ok",
          data: [{ id: "d-1", name: "Tiểu đường", requiresNote: true, isOther: false }],
        }),
      ),
    );

    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result } = renderHook(() => useDiseases(), { wrapper: makeWrapper(client) });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.[0].name).toBe("Tiểu đường");
  });
});

describe("useAllergyTypes", () => {
  it("tải danh mục loại dị ứng", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/medical-dictionaries/allergy-types`, () =>
        HttpResponse.json({
          code: 200,
          message: "ok",
          data: [{ id: "a-1", name: "Dị ứng thuốc kháng sinh", isOther: false }],
        }),
      ),
    );

    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result } = renderHook(() => useAllergyTypes(), { wrapper: makeWrapper(client) });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.[0].name).toBe("Dị ứng thuốc kháng sinh");
  });
});
