import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { ACCESS_TOKEN_KEY, API_BASE_URL } from "@/lib/api-client";
import { useAuthStore } from "@/store/auth-store";
import { server } from "@/test/mocks/server";

import { useSignIn } from "@/features/auth/hooks/use-sign-in";

const { replaceMock } = vi.hoisted(() => ({ replaceMock: vi.fn() }));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: replaceMock }),
}));

function Wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

function loginResponse(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    userId: "user-1",
    accessToken: "access-token-abc",
    role: "DOCTOR",
    fullName: "BS. Test",
    email: "doctor@example.com",
    mustChangePassword: false,
    ...overrides,
  };
}

describe("useSignIn", () => {
  beforeEach(() => {
    replaceMock.mockClear();
    useAuthStore.setState({ accessToken: null, user: null });
  });

  it("đăng nhập thành công — lưu token, lưu session, điều hướng theo home path của vai trò", async () => {
    server.use(
      http.post(`${API_BASE_URL}/api/v1/auth/login`, () =>
        HttpResponse.json({ code: 200, message: "OK", data: loginResponse({ role: "DOCTOR" }) }),
      ),
    );

    const { result } = renderHook(() => useSignIn(), { wrapper: Wrapper });
    result.current.mutate({ phoneNumber: "0900000000", password: "Password1" });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    // Token phải được lưu TRƯỚC khi signIn() để axios interceptor gắn được vào request kế tiếp.
    expect(window.localStorage.getItem(ACCESS_TOKEN_KEY)).toBe("access-token-abc");
    expect(useAuthStore.getState().accessToken).toBe("access-token-abc");
    expect(useAuthStore.getState().user?.role).toBe("DOCTOR");
    expect(replaceMock).toHaveBeenCalledWith("/patients");
  });

  it("UC-25: mustChangePassword=true — điều hướng tới /change-password, không tới home path", async () => {
    server.use(
      http.post(`${API_BASE_URL}/api/v1/auth/login`, () =>
        HttpResponse.json({
          code: 200,
          message: "OK",
          data: loginResponse({ role: "ADMIN", mustChangePassword: true }),
        }),
      ),
    );

    const { result } = renderHook(() => useSignIn(), { wrapper: Wrapper });
    result.current.mutate({ phoneNumber: "0900000001", password: "Password1" });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(replaceMock).toHaveBeenCalledWith("/change-password");
    expect(replaceMock).not.toHaveBeenCalledWith("/dashboard");
  });

  it("UC-01: role PATIENT — chặn TRƯỚC khi lưu token, không lưu session, không điều hướng", async () => {
    server.use(
      http.post(`${API_BASE_URL}/api/v1/auth/login`, () =>
        HttpResponse.json({ code: 200, message: "OK", data: loginResponse({ role: "PATIENT" }) }),
      ),
    );

    const { result } = renderHook(() => useSignIn(), { wrapper: Wrapper });
    result.current.mutate({ phoneNumber: "0900000002", password: "Password1" });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(window.localStorage.getItem(ACCESS_TOKEN_KEY)).toBeNull();
    expect(useAuthStore.getState().accessToken).toBeNull();
    expect(replaceMock).not.toHaveBeenCalled();
  });

  it("sai số điện thoại/mật khẩu (401) — không lưu token, không điều hướng", async () => {
    server.use(
      http.post(`${API_BASE_URL}/api/v1/auth/login`, () =>
        HttpResponse.json(
          { code: 401, message: "Invalid phone number or password.", data: null },
          { status: 401 },
        ),
      ),
    );

    const { result } = renderHook(() => useSignIn(), { wrapper: Wrapper });
    result.current.mutate({ phoneNumber: "0900000003", password: "WrongPass" });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(window.localStorage.getItem(ACCESS_TOKEN_KEY)).toBeNull();
    expect(replaceMock).not.toHaveBeenCalled();
  });
});
