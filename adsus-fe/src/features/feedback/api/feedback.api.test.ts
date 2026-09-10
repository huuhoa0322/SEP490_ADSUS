import { describe, it, expect, vi, beforeEach } from "vitest";
import { getAdminFeedbacks } from "./feedback.api";
import { apiClient } from "@/lib/api-client";

vi.mock("@/lib/api-client", () => ({
  apiClient: {
    get: vi.fn(),
  },
}));

describe("feedback.api - getAdminFeedbacks", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("calls GET /api/v1/admin/feedbacks with default page 1 and pageSize 15", async () => {
    const mockPagedData = {
      items: [
        {
          id: "fb-1",
          rating: 5,
          content: "Dịch vụ tốt",
          submittedAt: "2026-09-10T10:00:00Z",
          caseId: "case-1",
          doctorName: "BS. Hoàng Văn Phúc",
          doctorId: "doc-1",
          patientProfileId: "pat-1",
          patientName: "Nguyễn Văn Đạt",
          patientPhone: "0901234567",
        },
      ],
      page: 1,
      pageSize: 15,
      totalItems: 1,
      totalPages: 1,
    };

    vi.mocked(apiClient.get).mockResolvedValueOnce({
      data: {
        code: 200,
        message: "OK",
        data: mockPagedData,
      },
    });

    const result = await getAdminFeedbacks();

    expect(apiClient.get).toHaveBeenCalledWith("/api/v1/admin/feedbacks", {
      params: {
        page: 1,
        pageSize: 15,
        search: undefined,
        minRating: undefined,
      },
    });

    expect(result.items).toHaveLength(1);
    expect(result.items[0].doctorName).toBe("BS. Hoàng Văn Phúc");
    expect(result.totalItems).toBe(1);
  });

  it("passes search query trimmed and minRating filter", async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce({
      data: {
        code: 200,
        message: "OK",
        data: {
          items: [],
          page: 2,
          pageSize: 15,
          totalItems: 0,
          totalPages: 0,
        },
      },
    });

    await getAdminFeedbacks({
      page: 2,
      pageSize: 15,
      search: "  BS. Phúc  ",
      minRating: 4,
    });

    expect(apiClient.get).toHaveBeenCalledWith("/api/v1/admin/feedbacks", {
      params: {
        page: 2,
        pageSize: 15,
        search: "BS. Phúc",
        minRating: 4,
      },
    });
  });

  it("handles empty search by sending undefined", async () => {
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

    await getAdminFeedbacks({
      search: "   ",
    });

    expect(apiClient.get).toHaveBeenCalledWith("/api/v1/admin/feedbacks", {
      params: {
        page: 1,
        pageSize: 15,
        search: undefined,
        minRating: undefined,
      },
    });
  });

  it("throws error when response data is null", async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce({
      data: {
        code: 500,
        message: "Lỗi hệ thống",
        data: null,
      },
    });

    await expect(getAdminFeedbacks()).rejects.toThrow("Lỗi hệ thống");
  });

  it("handles fallback calculation for totalItems and totalPages", async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce({
      data: {
        code: 200,
        message: "OK",
        data: {
          items: [{ id: "fb-1", rating: 5 }],
          page: 1,
          pageSize: 15,
          totalCount: 30,
        },
      },
    });

    const result = await getAdminFeedbacks();

    expect(result.totalItems).toBe(30);
    expect(result.totalPages).toBe(2);
  });
});
