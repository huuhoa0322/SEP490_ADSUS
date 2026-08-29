import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { listDoctors } from "@/features/medical-record/api/doctors.api";

describe("listDoctors", () => {
  it("trả về danh sách bác sĩ", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/doctors`, () =>
        HttpResponse.json({
          code: 200,
          message: "ok",
          data: [{ userId: "doctor-1", fullName: "BS. Lê Minh Hoàng" }],
        }),
      ),
    );

    const result = await listDoctors();

    expect(result).toEqual([{ userId: "doctor-1", fullName: "BS. Lê Minh Hoàng" }]);
  });

  it("throw khi API trả data null trên response 200", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/doctors`, () =>
        HttpResponse.json({ code: 200, message: "Something broke", data: null }),
      ),
    );

    await expect(listDoctors()).rejects.toThrow("Something broke");
  });
});
