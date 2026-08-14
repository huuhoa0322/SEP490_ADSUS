import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import type { AuthUser } from "@/store/auth-store";
import { useAuthStore } from "@/store/auth-store";
import { server } from "@/test/mocks/server";

import { useChangePassword } from "@/features/auth/hooks/use-change-password";

const { replaceMock } = vi.hoisted(() => ({ replaceMock: vi.fn() }));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: replaceMock }),
}));

function Wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

function nguoiDung(overrides: Partial<AuthUser> = {}): AuthUser {
  return {
    userId: "user-1",
    fullName: "Người dùng test",
    email: null,
    role: "DOCTOR",
    mustChangePassword: false,
    ...overrides,
  };
}

describe("useChangePassword", () => {
  beforeEach(() => {
    replaceMock.mockClear();
    useAuthStore.setState({ accessToken: "token", user: null });
  });

  it("UC-25: bị ép đổi (mustChangePassword=true) — xoá cờ VÀ điều hướng về home path của vai trò", async () => {
    useAuthStore.setState({ user: nguoiDung({ role: "ADMIN", mustChangePassword: true }) });

    server.use(
      http.post(`${API_BASE_URL}/api/v1/auth/change-password`, () =>
        HttpResponse.json({ code: 200, message: "Password changed successfully.", data: null }),
      ),
    );

    const { result } = renderHook(() => useChangePassword(), { wrapper: Wrapper });
    result.current.mutate({
      currentPassword: null,
      newPassword: "Valid123",
      confirmNewPassword: "Valid123",
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(useAuthStore.getState().user?.mustChangePassword).toBe(false);
    expect(replaceMock).toHaveBeenCalledWith("/dashboard");
  });

  it("đổi tự nguyện (mustChangePassword=false) — xoá cờ nhưng KHÔNG điều hướng đi đâu", async () => {
    useAuthStore.setState({ user: nguoiDung({ role: "DOCTOR", mustChangePassword: false }) });

    server.use(
      http.post(`${API_BASE_URL}/api/v1/auth/change-password`, () =>
        HttpResponse.json({ code: 200, message: "Password changed successfully.", data: null }),
      ),
    );

    const { result } = renderHook(() => useChangePassword(), { wrapper: Wrapper });
    result.current.mutate({
      currentPassword: "OldPass1",
      newPassword: "Valid123",
      confirmNewPassword: "Valid123",
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(useAuthStore.getState().user?.mustChangePassword).toBe(false);
    expect(replaceMock).not.toHaveBeenCalled();
  });

  it("mật khẩu hiện tại sai (400) — KHÔNG xoá cờ, không điều hướng (chỉ xử lý trong onSuccess)", async () => {
    useAuthStore.setState({ user: nguoiDung({ role: "DOCTOR", mustChangePassword: true }) });

    server.use(
      http.post(`${API_BASE_URL}/api/v1/auth/change-password`, () =>
        HttpResponse.json(
          { code: 400, message: "Current password is incorrect.", data: null },
          { status: 400 },
        ),
      ),
    );

    const { result } = renderHook(() => useChangePassword(), { wrapper: Wrapper });
    result.current.mutate({
      currentPassword: "WrongOld",
      newPassword: "Valid123",
      confirmNewPassword: "Valid123",
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(useAuthStore.getState().user?.mustChangePassword).toBe(true);
    expect(replaceMock).not.toHaveBeenCalled();
  });
});
