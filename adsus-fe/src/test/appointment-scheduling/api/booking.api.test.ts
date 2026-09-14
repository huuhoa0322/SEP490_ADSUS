import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { getCancellationStatusToday } from "@/features/appointment-scheduling/api/booking.api";

describe("getCancellationStatusToday", () => {
  it("trả về đúng shape với isNextCancellationFinal: true", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/appointments/cancellation-status-today`, () =>
        HttpResponse.json({
          code: 200,
          message: "OK",
          data: {
            cancellationsToday: 2,
            maxCancellations: 3,
            canBookOnline: true,
            isNextCancellationFinal: true,
          },
        }),
      ),
    );

    const result = await getCancellationStatusToday();

    expect(result).toEqual({
      cancellationsToday: 2,
      maxCancellations: 3,
      canBookOnline: true,
      isNextCancellationFinal: true,
    });
  });

  it("ném lỗi khi data.data là null trên response 200", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/appointments/cancellation-status-today`, () =>
        HttpResponse.json({ code: 200, message: "Không tải được trạng thái hủy lịch.", data: null }),
      ),
    );

    await expect(getCancellationStatusToday()).rejects.toThrow("Không tải được trạng thái hủy lịch.");
  });
});
