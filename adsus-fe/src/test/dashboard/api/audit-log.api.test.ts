import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { getRecentAuditLogs } from "@/features/dashboard/api/audit-log.api";

describe("getRecentAuditLogs", () => {
  it("gửi limit qua query param và trả về danh sách", async () => {
    let capturedUrl: string | undefined;
    const entry = {
      logId: "log-1",
      actorId: "user-1",
      actorName: "Nguyễn Văn A",
      actorRole: "ADMIN" as const,
      action: "CREATE_ACCOUNT",
      detail: "BS. Trần Văn B (0900000000, DOCTOR)",
      performedAt: "2026-07-31T10:00:00Z",
    };

    server.use(
      http.get(`${API_BASE_URL}/api/v1/admin/audit-logs`, ({ request }) => {
        capturedUrl = request.url;
        return HttpResponse.json({ code: 200, message: "Audit log loaded.", data: [entry] });
      }),
    );

    const result = await getRecentAuditLogs(5);

    expect(result).toEqual([entry]);
    expect(capturedUrl).toContain("limit=5");
  });

  it("mặc định limit là 10 khi không truyền", async () => {
    let capturedUrl: string | undefined;

    server.use(
      http.get(`${API_BASE_URL}/api/v1/admin/audit-logs`, ({ request }) => {
        capturedUrl = request.url;
        return HttpResponse.json({ code: 200, message: "Audit log loaded.", data: [] });
      }),
    );

    await getRecentAuditLogs();

    expect(capturedUrl).toContain("limit=10");
  });

  it("data null trên response 200 — trả về mảng rỗng, KHÔNG ném lỗi", async () => {
    // Khác dashboard.api.ts: danh sách nhật ký rỗng là trạng thái hợp lệ ("chưa có thao tác
    // nào"), không phải dấu hiệu backend lỗi.
    server.use(
      http.get(`${API_BASE_URL}/api/v1/admin/audit-logs`, () =>
        HttpResponse.json({ code: 200, message: "Audit log loaded.", data: null }),
      ),
    );

    await expect(getRecentAuditLogs()).resolves.toEqual([]);
  });
});
