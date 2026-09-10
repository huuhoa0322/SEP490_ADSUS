import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuditLogs, auditLogsQueryKeys } from "../hooks/use-audit-logs";
import * as auditApi from "../api/audit-logs.api";
import type { AuditLogEntry } from "../types/audit-logs.types";
import type { PagedResult } from "@/types/api.types";

vi.mock("../api/audit-logs.api", () => ({
  getAuditLogs: vi.fn(),
}));

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });

  function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  }

  return Wrapper;
}

describe("useAuditLogs", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("generates structured query keys", () => {
    const keys = auditLogsQueryKeys.list({ page: 1, pageSize: 15, search: "admin" });
    expect(keys).toEqual(["admin", "audit-logs", "list", { page: 1, pageSize: 15, search: "admin" }]);
  });

  it("successfully fetches paged audit logs", async () => {
    const mockData: PagedResult<AuditLogEntry> = {
      items: [
        {
          logId: "log-1",
          actorId: "actor-1",
          actorName: "Admin Root",
          actorRole: "ADMIN",
          action: "CREATE_ACCOUNT",
          detail: "Created user",
          performedAt: "2026-09-10T10:00:00Z",
        },
      ],
      page: 1,
      pageSize: 15,
      totalItems: 1,
      totalPages: 1,
    };

    vi.mocked(auditApi.getAuditLogs).mockResolvedValueOnce(mockData);

    const { result } = renderHook(() => useAuditLogs({ page: 1, pageSize: 15 }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual(mockData);
    expect(auditApi.getAuditLogs).toHaveBeenCalledWith({
      page: 1,
      pageSize: 15,
      search: undefined,
      action: undefined,
      fromDate: undefined,
      toDate: undefined,
    });
  });

  it("handles errors gracefully and sets isError", async () => {
    vi.mocked(auditApi.getAuditLogs).mockRejectedValueOnce(new Error("Network Failure"));

    const { result } = renderHook(() => useAuditLogs({ page: 1 }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});
