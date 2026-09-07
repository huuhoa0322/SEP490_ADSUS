import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { useDashboardStatistics } from "@/features/dashboard/hooks/use-dashboard";
import type { DashboardStatistics } from "@/features/dashboard/types/dashboard.types";

const SAMPLE_STATISTICS: DashboardStatistics = {
  fromDate: "2026-07-01",
  toDate: "2026-07-31",
  accounts: {
    total: 10, adminCount: 1, doctorCount: 3, nurseCount: 2, patientCount: 4,
    activeCount: 9, deactivatedCount: 1, newInRange: 2, activeRate: 90,
  },
  clinical: { caseCount: 5, aiRunCount: 0, aiConfirmedCount: 0, aiRejectedCount: 0, aiPendingCount: 0, aiConfirmRate: 0 },
  appointments: { bookedCount: 6, cancelledCount: 4, slotCount: 8, cancellationRate: 40 },
  adherence: { scheduledDoseCount: 20, takenDoseCount: 15, adherenceRate: 75 },
  activeAiModel: { versionCode: "YOLO26_v1" },
  trend: [],
};

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  }
  return Wrapper;
}

describe("useDashboardStatistics", () => {
  it("tải số liệu thành công qua getDashboardStatistics thật", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/dashboard/statistics`, () =>
        HttpResponse.json({ code: 200, message: "Statistics loaded.", data: SAMPLE_STATISTICS }),
      ),
    );

    const { result } = renderHook(
      () => useDashboardStatistics({ fromDate: "2026-07-01", toDate: "2026-07-31" }),
      { wrapper: createWrapper() },
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(SAMPLE_STATISTICS);
  });

  it("đổi khoảng thời gian — giữ dữ liệu cũ trong lúc tải (placeholderData), không nháy trắng", async () => {
    let callCount = 0;
    server.use(
      http.get(`${API_BASE_URL}/api/v1/dashboard/statistics`, async ({ request }) => {
        callCount += 1;
        const url = new URL(request.url);
        // Lần gọi thứ 2 (khoảng mới) cố tình trả chậm hơn để kiểm placeholderData còn hiệu lực.
        if (callCount === 2) await new Promise((r) => setTimeout(r, 20));
        return HttpResponse.json({
          code: 200,
          message: "Statistics loaded.",
          data: { ...SAMPLE_STATISTICS, fromDate: url.searchParams.get("fromDate") ?? SAMPLE_STATISTICS.fromDate },
        });
      }),
    );

    const Wrapper = createWrapper();
    const { result, rerender } = renderHook(
      ({ fromDate }: { fromDate: string }) => useDashboardStatistics({ fromDate, toDate: "2026-07-31" }),
      { wrapper: Wrapper, initialProps: { fromDate: "2026-07-01" } },
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.fromDate).toBe("2026-07-01");

    rerender({ fromDate: "2026-08-01" });

    // Trong lúc query mới chưa xong, data vẫn còn giá trị (không phải undefined) — đúng ý
    // đồ placeholderData: (previous) => previous.
    expect(result.current.data).not.toBeUndefined();

    await waitFor(() => expect(result.current.data?.fromDate).toBe("2026-08-01"));
  });

  it("backend lỗi — isError true, không văng exception ra ngoài React", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/dashboard/statistics`, () =>
        HttpResponse.json(
          { code: 500, message: "An unexpected error occurred. Please try again later.", data: null },
          { status: 500 },
        ),
      ),
    );

    const { result } = renderHook(() => useDashboardStatistics({}), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});
