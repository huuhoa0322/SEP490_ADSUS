import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { getSymptomCategories } from "@/features/medical-record/api/symptoms.api";

describe("getSymptomCategories", () => {
  it("trả về danh sách category kèm symptom lồng nhau", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/symptoms/categories`, () =>
        HttpResponse.json({
          code: 200,
          message: "ok",
          data: [
            {
              categoryId: "cat-1",
              name: "Đau vú",
              isOther: false,
              symptoms: [{ symptomId: "sym-1", name: "Đau khi chạm", isOther: false }],
            },
          ],
        }),
      ),
    );

    const result = await getSymptomCategories();

    expect(result).toHaveLength(1);
    expect(result[0].name).toBe("Đau vú");
    expect(result[0].symptoms[0].name).toBe("Đau khi chạm");
  });

  it("throw khi API trả data null trên response 200", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/symptoms/categories`, () =>
        HttpResponse.json({ code: 200, message: "Something broke", data: null }),
      ),
    );

    await expect(getSymptomCategories()).rejects.toThrow("Something broke");
  });
});
