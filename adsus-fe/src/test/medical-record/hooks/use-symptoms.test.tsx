import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { useSymptomCategories } from "@/features/medical-record/hooks/use-symptoms";

function makeWrapper(client: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

describe("useSymptomCategories", () => {
  it("tải danh mục triệu chứng", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/symptoms/categories`, () =>
        HttpResponse.json({
          code: 200,
          message: "ok",
          data: [{ categoryId: "cat-1", name: "Đau vú", isOther: false, symptoms: [] }],
        }),
      ),
    );

    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result } = renderHook(() => useSymptomCategories(), { wrapper: makeWrapper(client) });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.[0].name).toBe("Đau vú");
  });
});
