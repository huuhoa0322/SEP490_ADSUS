import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { useForgotPassword } from "@/features/auth/hooks/use-forgot-password";

function Wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

describe("useForgotPassword", () => {
  it("UC-03 FT-06: gửi yêu cầu thành công — isSuccess=true", async () => {
    server.use(
      http.post(`${API_BASE_URL}/api/v1/auth/forgot-password`, () =>
        HttpResponse.json({
          code: 200,
          message: "If the information is correct, a new password has been sent to your email.",
          data: null,
        }),
      ),
    );

    const { result } = renderHook(() => useForgotPassword(), { wrapper: Wrapper });
    result.current.mutate({ phoneNumber: "0900000000", email: "a@b.com" });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
  });

  it("backend lỗi (500) — isError=true, mutation không throw ra ngoài React", async () => {
    server.use(
      http.post(`${API_BASE_URL}/api/v1/auth/forgot-password`, () =>
        HttpResponse.json(
          { code: 500, message: "Operation failed.", data: null },
          { status: 500 },
        ),
      ),
    );

    const { result } = renderHook(() => useForgotPassword(), { wrapper: Wrapper });
    result.current.mutate({ phoneNumber: "0900000000", email: "a@b.com" });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});
