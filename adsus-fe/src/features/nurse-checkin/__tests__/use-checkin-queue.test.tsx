import { describe, it, expect, vi } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { useCheckinQueue } from "../hooks/use-checkin-queue";
import * as checkinApi from "../api/checkin.api";
import { nurseCheckinQueryKeys } from "../query-keys";

vi.mock("../api/checkin.api", () => ({
  getCheckinQueue: vi.fn(),
}));

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });

  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );
  };
}

describe("useCheckinQueue", () => {
  it("should query check-in queue with string date parameter", async () => {
    const mockData = {
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 15,
      totalPages: 0,
    };
    vi.mocked(checkinApi.getCheckinQueue).mockResolvedValueOnce(mockData);

    const { result } = renderHook(() => useCheckinQueue("2026-09-10"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(checkinApi.getCheckinQueue).toHaveBeenCalledWith("2026-09-10");
    expect(result.current.data).toEqual(mockData);
  });

  it("should query check-in queue with CheckinQueueParams object and retain placeholderData", async () => {
    const params = {
      fromDate: "2026-09-01",
      toDate: "2026-09-10",
      status: "APPROVED",
      search: "Trần",
      page: 1,
      pageSize: 15,
    };
    const mockData = {
      items: [
        {
          appointmentId: "app-2",
          slotTime: "2026-09-05T09:00:00Z",
          patientFullName: "Trần Thị B",
          patientPhone: "0912345678",
          patientProfileId: "prof-2",
          caseId: "case-2",
          reason: "Tái khám",
          doctorName: "BS. Lan",
          status: "Approved",
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 15,
      totalPages: 1,
    };
    vi.mocked(checkinApi.getCheckinQueue).mockResolvedValueOnce(mockData);

    const { result } = renderHook(() => useCheckinQueue(params), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(checkinApi.getCheckinQueue).toHaveBeenCalledWith(params);
    expect(result.current.data?.items).toHaveLength(1);
  });

  it("should generate proper query keys", () => {
    expect(nurseCheckinQueryKeys.queue("2026-09-10")).toEqual([
      "nurse-checkin",
      "queue",
      { date: "2026-09-10" },
    ]);

    const params = { fromDate: "2026-09-01", toDate: "2026-09-07", page: 1 };
    expect(nurseCheckinQueryKeys.queue(params)).toEqual([
      "nurse-checkin",
      "queue",
      params,
    ]);
  });
});
