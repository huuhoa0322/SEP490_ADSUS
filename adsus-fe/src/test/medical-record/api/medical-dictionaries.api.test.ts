import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import {
  listAllergyTypes,
  listDiseases,
} from "@/features/medical-record/api/medical-dictionaries.api";

describe("listDiseases", () => {
  it("trả về danh sách bệnh nền", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/medical-dictionaries/diseases`, () =>
        HttpResponse.json({
          code: 200,
          message: "ok",
          data: [{ id: "d-1", name: "Tiểu đường", requiresNote: true, isOther: false }],
        }),
      ),
    );

    const result = await listDiseases();

    expect(result).toEqual([{ id: "d-1", name: "Tiểu đường", requiresNote: true, isOther: false }]);
  });

  it("throw khi API trả data null trên response 200", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/medical-dictionaries/diseases`, () =>
        HttpResponse.json({ code: 200, message: "Something broke", data: null }),
      ),
    );

    await expect(listDiseases()).rejects.toThrow("Something broke");
  });
});

describe("listAllergyTypes", () => {
  it("trả về danh sách loại dị ứng", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/medical-dictionaries/allergy-types`, () =>
        HttpResponse.json({
          code: 200,
          message: "ok",
          data: [{ id: "a-1", name: "Dị ứng thuốc kháng sinh", isOther: false }],
        }),
      ),
    );

    const result = await listAllergyTypes();

    expect(result).toEqual([{ id: "a-1", name: "Dị ứng thuốc kháng sinh", isOther: false }]);
  });

  it("throw khi API trả data null trên response 200", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/medical-dictionaries/allergy-types`, () =>
        HttpResponse.json({ code: 200, message: "Something broke", data: null }),
      ),
    );

    await expect(listAllergyTypes()).rejects.toThrow("Something broke");
  });
});
