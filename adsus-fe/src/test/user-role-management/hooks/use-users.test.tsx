import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import {
  useDeactivateUser,
  useReactivateUser,
  useResetUserPassword,
  useUserDetail,
  useUserList,
} from "@/features/user-role-management/hooks/use-users";

const SAMPLE_ACCOUNT = {
  userId: "user-123",
  phoneNumber: "0900000123",
  fullName: "Nguyễn Văn Test",
  email: null,
  role: "DOCTOR" as const,
  status: "ACTIVE" as const,
  dateOfBirth: null,
  mustChangePassword: false,
  createdAt: "2026-07-31T10:00:00Z",
  isCurrentUser: false,
};

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  }
  return Wrapper;
}

describe("useUserList", () => {
  it("tải danh sách qua searchUsers, expose page/pageSize/totalPages từ PagedResult", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/admin/users`, () =>
        HttpResponse.json({
          code: 200,
          message: "User list loaded.",
          data: { items: [SAMPLE_ACCOUNT], page: 2, pageSize: 20, totalCount: 25, totalPages: 2 },
        }),
      ),
    );

    const { result } = renderHook(
      () => useUserList({ page: 2, pageSize: 20 }),
      { wrapper: createWrapper() },
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.items).toEqual([SAMPLE_ACCOUNT]);
    expect(result.current.data?.page).toBe(2);
    expect(result.current.data?.totalPages).toBe(2);
  });
});

describe("useUserDetail", () => {
  it("không gọi API khi userId là undefined (enabled: false)", () => {
    let calls = 0;
    server.use(
      http.get(`${API_BASE_URL}/api/v1/admin/users/:id`, () => {
        calls += 1;
        return HttpResponse.json({ code: 200, message: "Account loaded.", data: SAMPLE_ACCOUNT });
      }),
    );

    const { result } = renderHook(() => useUserDetail(undefined), { wrapper: createWrapper() });

    expect(result.current.fetchStatus).toBe("idle");
    expect(calls).toBe(0);
  });

  it("có userId — tải đúng tài khoản", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/admin/users/user-123`, () =>
        HttpResponse.json({ code: 200, message: "Account loaded.", data: SAMPLE_ACCOUNT }),
      ),
    );

    const { result } = renderHook(() => useUserDetail("user-123"), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(SAMPLE_ACCOUNT);
  });
});

describe("useDeactivateUser / useReactivateUser — làm mới danh sách sau khi thao tác", () => {
  it("deactivate thành công — danh sách được gọi lại (invalidateQueries)", async () => {
    let listCallCount = 0;
    server.use(
      http.get(`${API_BASE_URL}/api/v1/admin/users`, () => {
        listCallCount += 1;
        return HttpResponse.json({
          code: 200,
          message: "User list loaded.",
          data: { items: [SAMPLE_ACCOUNT], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 },
        });
      }),
      http.put(`${API_BASE_URL}/api/v1/admin/users/user-123/deactivate`, () =>
        HttpResponse.json({ code: 200, message: "Account deactivated permanently.", data: null }),
      ),
    );

    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    function Wrapper({ children }: { children: ReactNode }) {
      return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
    }

    const list = renderHook(() => useUserList({ page: 1, pageSize: 20 }), { wrapper: Wrapper });
    await waitFor(() => expect(list.result.current.isSuccess).toBe(true));
    expect(listCallCount).toBe(1);

    const deactivate = renderHook(() => useDeactivateUser(), { wrapper: Wrapper });
    deactivate.result.current.mutate("user-123");
    await waitFor(() => expect(deactivate.result.current.isSuccess).toBe(true));

    // invalidateQueries đánh dấu query cũ stale và kích hoạt refetch cho mọi observer đang mount.
    await waitFor(() => expect(listCallCount).toBe(2));
  });

  it("reactivate thành công — danh sách cũng được làm mới", async () => {
    let listCallCount = 0;
    server.use(
      http.get(`${API_BASE_URL}/api/v1/admin/users`, () => {
        listCallCount += 1;
        return HttpResponse.json({
          code: 200,
          message: "User list loaded.",
          data: { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 1 },
        });
      }),
      http.put(`${API_BASE_URL}/api/v1/admin/users/user-123/reactivate`, () =>
        HttpResponse.json({ code: 200, message: "Account reactivated successfully.", data: null }),
      ),
    );

    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    function Wrapper({ children }: { children: ReactNode }) {
      return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
    }

    const list = renderHook(() => useUserList({ page: 1, pageSize: 20 }), { wrapper: Wrapper });
    await waitFor(() => expect(list.result.current.isSuccess).toBe(true));

    const reactivate = renderHook(() => useReactivateUser(), { wrapper: Wrapper });
    reactivate.result.current.mutate("user-123");
    await waitFor(() => expect(reactivate.result.current.isSuccess).toBe(true));

    await waitFor(() => expect(listCallCount).toBe(2));
  });
});

describe("useResetUserPassword — KHÔNG làm mới danh sách", () => {
  it("cấp lại mật khẩu thành công không kích hoạt refetch danh sách", async () => {
    // Comment tại use-users.ts nói rõ lý do: thao tác này chỉ đổi mật khẩu/cờ buộc đổi,
    // không đổi thứ gì đang hiển thị trên bảng SCR-06 — nên cố tình KHÔNG invalidate.
    let listCallCount = 0;
    server.use(
      http.get(`${API_BASE_URL}/api/v1/admin/users`, () => {
        listCallCount += 1;
        return HttpResponse.json({
          code: 200,
          message: "User list loaded.",
          data: { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 1 },
        });
      }),
      http.put(`${API_BASE_URL}/api/v1/admin/users/user-123/reset-password`, () =>
        HttpResponse.json({
          code: 200,
          message: "Temporary password generated — communicate it to the account holder directly.",
          data: "Aa1b2c3d4e",
        }),
      ),
    );

    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    function Wrapper({ children }: { children: ReactNode }) {
      return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
    }

    const list = renderHook(() => useUserList({ page: 1, pageSize: 20 }), { wrapper: Wrapper });
    await waitFor(() => expect(list.result.current.isSuccess).toBe(true));
    expect(listCallCount).toBe(1);

    const reset = renderHook(() => useResetUserPassword(), { wrapper: Wrapper });
    reset.result.current.mutate("user-123");
    await waitFor(() => expect(reset.result.current.isSuccess).toBe(true));

    expect(listCallCount).toBe(1);
  });
});
