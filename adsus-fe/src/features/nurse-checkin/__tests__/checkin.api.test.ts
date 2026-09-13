import { describe, it, expect, vi, beforeEach } from "vitest";
import { apiClient } from "@/lib/api-client";
import {
  getCheckinQueue,
  checkinAppointment,
  checkinByCaseId,
  staffBookAppointment,
} from "../api/checkin.api";
import type { CheckinQueueResponse } from "../types/checkin.types";

vi.mock("@/lib/api-client", () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn(),
  },
}));

describe("checkin.api", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe("getCheckinQueue", () => {
    it("should fetch check-in queue with legacy string date parameter", async () => {
      const mockResponse: CheckinQueueResponse = {
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 15,
        totalPages: 0,
      };

      vi.mocked(apiClient.get).mockResolvedValueOnce({
        data: {
          code: 200,
          message: "Success",
          data: mockResponse,
        },
      });

      const result = await getCheckinQueue("2026-09-10");
      expect(apiClient.get).toHaveBeenCalledWith("/api/v1/appointments/checkin-queue", {
        params: { date: "2026-09-10" },
      });
      expect(result).toEqual(mockResponse);
    });

    it("should fetch check-in queue with full CheckinQueueParams object", async () => {
      const mockResponse: CheckinQueueResponse = {
        items: [
          {
            appointmentId: "app-1",
            slotTime: "2026-09-10T08:00:00Z",
            patientFullName: "Nguyen Van A",
            patientPhone: "0901234567",
            patientProfileId: "prof-1",
            caseId: "case-1",
            reason: "Kham tong quat",
            doctorName: "BS. Minh",
            status: "Booked",
          },
        ],
        totalCount: 1,
        page: 1,
        pageSize: 15,
        totalPages: 1,
      };

      vi.mocked(apiClient.get).mockResolvedValueOnce({
        data: {
          code: 200,
          message: "Success",
          data: mockResponse,
        },
      });

      const result = await getCheckinQueue({
        fromDate: "2026-09-01",
        toDate: "2026-09-10",
        status: "BOOKED",
        search: "Nguyen",
        page: 2,
        pageSize: 15,
      });

      expect(apiClient.get).toHaveBeenCalledWith("/api/v1/appointments/checkin-queue", {
        params: {
          fromDate: "2026-09-01",
          toDate: "2026-09-10",
          status: "BOOKED",
          search: "Nguyen",
          page: 2,
          pageSize: 15,
        },
      });
      expect(result.items).toHaveLength(1);
      expect(result.totalCount).toBe(1);
    });

    it("should throw error if response does not contain data", async () => {
      vi.mocked(apiClient.get).mockResolvedValueOnce({
        data: {
          code: 400,
          message: "Error fetching queue",
          data: null,
        },
      });

      await expect(getCheckinQueue()).rejects.toThrow("Error fetching queue");
    });
  });

  describe("checkinAppointment", () => {
    it("should call appointment-based checkin when caseId is empty GUID", async () => {
      const emptyGuid = "00000000-0000-0000-0000-000000000000";
      vi.mocked(apiClient.post).mockResolvedValueOnce({
        data: {
          code: 200,
          message: "Check-in successful",
          data: null,
        },
      });

      const res = await checkinAppointment("app-123", emptyGuid);
      expect(apiClient.post).toHaveBeenCalledWith(
        "/api/v1/cases/appointment/app-123/checkin"
      );
      expect(res.code).toBe(200);
    });

    it("should call case-based checkin when caseId is valid GUID", async () => {
      vi.mocked(apiClient.post).mockResolvedValueOnce({
        data: {
          code: 200,
          message: "Check-in successful",
          data: null,
        },
      });

      const res = await checkinAppointment("", "case-456");
      expect(apiClient.post).toHaveBeenCalledWith(
        "/api/v1/cases/case-456/appointment/checkin"
      );
      expect(res.code).toBe(200);
    });

    it("should throw error when check-in returns non-200/201 code", async () => {
      vi.mocked(apiClient.post).mockResolvedValueOnce({
        data: {
          code: 500,
          message: "Server internal error",
          data: null,
        },
      });

      await expect(checkinAppointment("app-123", "case-456")).rejects.toThrow(
        "Server internal error"
      );
    });
  });

  describe("checkinByCaseId", () => {
    it("should post to case checkin endpoint", async () => {
      vi.mocked(apiClient.post).mockResolvedValueOnce({
        data: {
          code: 200,
          message: "Check-in successful",
          data: null,
        },
      });

      await expect(checkinByCaseId("case-789")).resolves.toBeUndefined();
      expect(apiClient.post).toHaveBeenCalledWith(
        "/api/v1/cases/case-789/appointment/checkin"
      );
    });

    it("should throw if non-200 code returned", async () => {
      vi.mocked(apiClient.post).mockResolvedValueOnce({
        data: {
          code: 400,
          message: "Already checked in",
          data: null,
        },
      });

      await expect(checkinByCaseId("case-789")).rejects.toThrow(
        "Already checked in"
      );
    });
  });

  describe("staffBookAppointment", () => {
    it("should post to book-for-patient endpoint and return data on 201", async () => {
      const mockResult = {
        code: 201,
        message: "Success",
        data: { appointmentId: "app-123" },
      };
      vi.mocked(apiClient.post).mockResolvedValueOnce({
        data: mockResult,
      });

      const request = {
        patientProfileId: "prof-1",
        scheduleSlotId: "slot-1",
        reason: "Tái khám",
      };

      const result = await staffBookAppointment(request);
      expect(apiClient.post).toHaveBeenCalledWith(
        "/api/v1/appointments/book-for-patient",
        request
      );
      expect(result).toEqual(mockResult);
    });

    it("should throw if response code is not 200 or 201", async () => {
      vi.mocked(apiClient.post).mockResolvedValueOnce({
        data: {
          code: 400,
          message: "Slot đã được đặt",
          data: null,
        },
      });

      await expect(
        staffBookAppointment({
          patientProfileId: "prof-1",
          scheduleSlotId: "slot-1",
        })
      ).rejects.toThrow("Slot đã được đặt");
    });
  });
});
