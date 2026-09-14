import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";
import {
  useRelatives,
  useAddRelative,
  useCheckPhone,
} from "@/features/appointment-scheduling/hooks/use-relatives";

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  }
  return Wrapper;
}

describe("useRelatives", () => {
  it("lấy danh sách người thân thành công", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/relatives`, () =>
        HttpResponse.json({
          relatives: [
            {
              relationshipId: "rel-1",
              patientProfileId: "prof-1",
              patientName: "Nguyễn Thị Mẹ",
              patientPhone: "0912345678",
              dateOfBirth: "1960-01-01",
              gender: "FEMALE",
              relationshipName: "Mẹ",
              isRegisteredAccount: false,
              createdAt: "2026-09-01T00:00:00Z",
            },
          ],
        }),
      ),
    );

    const { result } = renderHook(() => useRelatives(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toHaveLength(1);
    expect(result.current.data?.[0].patientName).toBe("Nguyễn Thị Mẹ");
  });

  it("thêm người thân mới thành công qua useAddRelative", async () => {
    let capturedBody: unknown = null;
    server.use(
      http.post(`${API_BASE_URL}/api/v1/relatives`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json(
          {
            relationshipId: "rel-2",
            patientProfileId: "prof-2",
            patientName: "Nguyễn Văn Bố",
            patientPhone: "0987654321",
            dateOfBirth: "1958-05-05",
            gender: null,
            relationshipName: "Bố",
            isRegisteredAccount: false,
            createdAt: "2026-09-14T00:00:00Z",
          },
          { status: 201 },
        );
      }),
    );

    const { result } = renderHook(() => useAddRelative(), {
      wrapper: createWrapper(),
    });

    const added = await result.current.mutateAsync({
      fullName: "Nguyễn Văn Bố",
      phone: "0987654321",
      dateOfBirth: "1958-05-05",
      relationshipName: "Bố",
    });

    expect(capturedBody).toEqual({
      fullName: "Nguyễn Văn Bố",
      phone: "0987654321",
      dateOfBirth: "1958-05-05",
      relationshipName: "Bố",
    });
    expect(added.relationshipId).toBe("rel-2");
  });

  it("kiểm tra số điện thoại thành công qua useCheckPhone", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/relatives/check-phone`, ({ request }) => {
        const url = new URL(request.url);
        const phone = url.searchParams.get("phone");
        return HttpResponse.json({
          phone: phone ?? "",
          isRegistered: phone === "0900000001",
        });
      }),
    );

    const { result } = renderHook(() => useCheckPhone(), {
      wrapper: createWrapper(),
    });

    const res1 = await result.current.mutateAsync("0900000001");
    expect(res1.isRegistered).toBe(true);

    const res2 = await result.current.mutateAsync("0911111111");
    expect(res2.isRegistered).toBe(false);
  });
});
