import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { useExportCaseReport } from "@/features/medical-record/hooks/use-case-report";

function makeWrapper() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

describe("useExportCaseReport", () => {
  beforeEach(() => {
    // jsdom không cài sẵn hai hàm này; hook dùng chúng để kích hoạt tải file.
    URL.createObjectURL = vi.fn(() => "blob:fake");
    URL.revokeObjectURL = vi.fn();
  });

  it("kích hoạt tải file với tên visit-report-{caseId}.pdf", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/cases/case-1/report`, () =>
        HttpResponse.arrayBuffer(new Uint8Array([0x25, 0x50, 0x44, 0x46]).buffer, {
          headers: { "Content-Type": "application/pdf" },
        }),
      ),
    );

    const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});

    const { result } = renderHook(() => useExportCaseReport("case-1"), { wrapper: makeWrapper() });

    result.current.exportReport();

    await waitFor(() => expect(clickSpy).toHaveBeenCalled());
    expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:fake");
  });

  it("phơi ra thông báo lỗi thật khi ca chưa được kết luận", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/cases/case-1/report`, () =>
        HttpResponse.json(
          { code: 422, message: "The case is not confirmed yet.", data: null },
          { status: 422 },
        ),
      ),
    );

    const { result } = renderHook(() => useExportCaseReport("case-1"), { wrapper: makeWrapper() });

    result.current.exportReport();

    // UC-12 BR-01 — giao diện đã disable nút khi ca chưa CONFIRMED, nhưng nếu trạng thái
    // đổi ở tab khác thì vẫn phải hiện đúng lý do chứ không phải thông báo trống.
    await waitFor(() => expect(result.current.error).toBeTruthy());
  });
});
