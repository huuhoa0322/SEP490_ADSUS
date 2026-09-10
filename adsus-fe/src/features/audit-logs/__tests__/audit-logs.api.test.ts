import { describe, expect, it, vi, beforeEach } from "vitest";
import { apiClient } from "@/lib/api-client";
import { getAuditLogs } from "../api/audit-logs.api";
import type { AuditLogEntry } from "../types/audit-logs.types";

vi.mock("@/lib/api-client", () => ({
  apiClient: {
    get: vi.fn(),
  },
}));

describe("audit-logs.api", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("calls GET /api/v1/admin/audit-logs with default page=1 and pageSize=15", async () => {
    const mockData: AuditLogEntry[] = [
      {
        logId: "log-1",
        actorId: "actor-1",
        actorName: "Admin Root",
        actorRole: "ADMIN",
        action: "CREATE_ACCOUNT",
        detail: "Tạo tài khoản bác sĩ",
        performedAt: "2026-09-10T08:00:00Z",
      },
    ];

    vi.mocked(apiClient.get).mockResolvedValueOnce({
      data: {
        code: 200,
        message: "OK",
        data: {
          items: mockData,
          page: 1,
          pageSize: 15,
          totalItems: 1,
          totalPages: 1,
        },
      },
    });

    const result = await getAuditLogs();

    expect(apiClient.get).toHaveBeenCalledWith("/api/v1/admin/audit-logs", {
      params: {
        page: 1,
        pageSize: 15,
        search: undefined,
        action: undefined,
        fromDate: undefined,
        toDate: undefined,
      },
    });

    expect(result.items).toHaveLength(1);
    expect(result.totalItems).toBe(1);
    expect(result.pageSize).toBe(15);
  });

  it("trims whitespace from search input and sends action parameter", async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce({
      data: {
        code: 200,
        message: "OK",
        data: {
          items: [],
          page: 1,
          pageSize: 15,
          totalItems: 0,
          totalPages: 0,
        },
      },
    });

    await getAuditLogs({
      search: "   Admin Root   ",
      action: "ACCOUNT",
      page: 2,
      pageSize: 15,
    });

    expect(apiClient.get).toHaveBeenCalledWith("/api/v1/admin/audit-logs", {
      params: {
        page: 2,
        pageSize: 15,
        search: "Admin Root",
        action: "ACCOUNT",
        fromDate: undefined,
        toDate: undefined,
      },
    });
  });

  it("omits action parameter when category is 'ALL'", async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce({
      data: {
        code: 200,
        message: "OK",
        data: {
          items: [],
          page: 1,
          pageSize: 15,
          totalItems: 0,
          totalPages: 0,
        },
      },
    });

    await getAuditLogs({
      action: "ALL",
    });

    expect(apiClient.get).toHaveBeenCalledWith("/api/v1/admin/audit-logs", {
      params: {
        page: 1,
        pageSize: 15,
        search: undefined,
        action: undefined,
        fromDate: undefined,
        toDate: undefined,
      },
    });
  });

  it("passes fromDate and toDate query parameters correctly", async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce({
      data: {
        code: 200,
        message: "OK",
        data: {
          items: [],
          page: 1,
          pageSize: 15,
          totalItems: 0,
          totalPages: 0,
        },
      },
    });

    await getAuditLogs({
      fromDate: "2026-09-01T00:00:00Z",
      toDate: "2026-09-10T23:59:59Z",
    });

    expect(apiClient.get).toHaveBeenCalledWith("/api/v1/admin/audit-logs", {
      params: {
        page: 1,
        pageSize: 15,
        search: undefined,
        action: undefined,
        fromDate: "2026-09-01T00:00:00Z",
        toDate: "2026-09-10T23:59:59Z",
      },
    });
  });

  it("returns safe empty fallback when response data is null", async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce({
      data: {
        code: 200,
        message: "No data",
        data: null,
      },
    });

    const result = await getAuditLogs({ page: 3, pageSize: 15 });

    expect(result).toEqual({
      items: [],
      page: 3,
      pageSize: 15,
      totalItems: 0,
      totalPages: 0,
    });
  });
});
