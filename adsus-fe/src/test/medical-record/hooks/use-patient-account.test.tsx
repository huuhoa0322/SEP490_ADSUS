import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { medicalRecordQueryKeys } from "@/features/medical-record/hooks/query-keys";
import { useCreatePatientAccount } from "@/features/medical-record/hooks/use-patient-account";

function makeWrapper(client: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

describe("useCreatePatientAccount", () => {
  it("làm mới danh sách bệnh nhân sau khi tạo tài khoản", async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, "invalidateQueries");

    server.use(
      http.post(`${API_BASE_URL}/api/v1/patients`, () =>
        HttpResponse.json(
          {
            code: 200,
            message: "ok",
            data: {
              userId: "user-9",
              fullName: "Lê Thị Hoa",
              phoneNumber: "0981234567",
              dateOfBirth: "1984-03-12",
              email: null,
            },
          },
          { status: 201 },
        ),
      ),
    );

    const { result } = renderHook(() => useCreatePatientAccount(), {
      wrapper: makeWrapper(client),
    });

    result.current.mutate({
      phoneNumber: "0981234567",
      fullName: "Lê Thị Hoa",
      dateOfBirth: "1984-03-12",
      email: null,
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    // Tài khoản mới phải xuất hiện ngay ở SCR-09 (dòng chưa có hồ sơ nền), không đợi người
    // dùng tự tải lại trang.
    expect(invalidate).toHaveBeenCalledWith({ queryKey: medicalRecordQueryKeys.all });
  });
});
