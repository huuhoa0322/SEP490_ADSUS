import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { getDashboardStatistics } from "@/features/dashboard/api/dashboard.api";
import type { DashboardStatistics } from "@/features/dashboard/types/dashboard.types";

const SAMPLE_STATISTICS: DashboardStatistics = {
  fromDate: "2026-07-01",
  toDate: "2026-07-31",
  accounts: {
    total: 10,
    adminCount: 1,
    doctorCount: 3,
    nurseCount: 2,
    patientCount: 4,
    activeCount: 9,
    deactivatedCount: 1,
    newInRange: 2,
    activeRate: 90,
  },
  clinical: { caseCount: 5, aiRunCount: 0, aiConfirmedCount: 0, aiRejectedCount: 0, aiPendingCount: 0, aiConfirmRate: 0 },
  appointments: { bookedCount: 6, cancelledCount: 4, slotCount: 8, cancellationRate: 40 },
  adherence: { scheduledDoseCount: 20, takenDoseCount: 15, adherenceRate: 75 },
  activeAiModel: { versionCode: "YOLO26_v1" },
  trend: [],
};

describe("getDashboardStatistics", () => {
  it("gửi fromDate/toDate qua query param và trả về đúng dữ liệu", async () => {
    let capturedUrl: string | undefined;

    server.use(
      http.get(`${API_BASE_URL}/api/v1/dashboard/statistics`, ({ request }) => {
        capturedUrl = request.url;
        return HttpResponse.json({ code: 200, message: "Statistics loaded.", data: SAMPLE_STATISTICS });
      }),
    );

    const result = await getDashboardStatistics({ fromDate: "2026-07-01", toDate: "2026-07-31" });

    expect(result).toEqual(SAMPLE_STATISTICS);
    expect(capturedUrl).toContain("fromDate=2026-07-01");
    expect(capturedUrl).toContain("toDate=2026-07-31");
  });

  it("không truyền fromDate/toDate — không gửi lên query rỗng", async () => {
    let capturedUrl: string | undefined;

    server.use(
      http.get(`${API_BASE_URL}/api/v1/dashboard/statistics`, ({ request }) => {
        capturedUrl = request.url;
        return HttpResponse.json({ code: 200, message: "Statistics loaded.", data: SAMPLE_STATISTICS });
      }),
    );

    await getDashboardStatistics({});

    expect(capturedUrl).not.toContain("fromDate=");
    expect(capturedUrl).not.toContain("toDate=");
  });

  it("data null trên response 200 — ném lỗi thay vì coi là hợp lệ", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/dashboard/statistics`, () =>
        HttpResponse.json({ code: 200, message: "Backend bug.", data: null }),
      ),
    );

    await expect(getDashboardStatistics({})).rejects.toThrow("Backend bug.");
  });
});
