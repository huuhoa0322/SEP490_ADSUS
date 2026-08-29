import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { usePatientList } from "@/features/medical-record/hooks/use-patients";

function makeWrapper(client: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

describe("usePatientList", () => {
  it("tải danh sách bệnh nhân theo query", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/patients`, () =>
        HttpResponse.json({
          code: 200,
          message: "ok",
          data: {
            items: [
              {
                patientProfileId: "profile-1",
                patientUserId: "user-1",
                fullName: "Phạm Hồng Hạnh",
                phone: "0912345678",
                latestVisitDate: null,
                latestVisitStatus: null,
              },
            ],
            page: 1,
            pageSize: 20,
            totalItems: 1,
            totalPages: 1,
          },
        }),
      ),
    );

    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result } = renderHook(() => usePatientList({ page: 1, pageSize: 20 }), {
      wrapper: makeWrapper(client),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.items[0].fullName).toBe("Phạm Hồng Hạnh");
  });
});
