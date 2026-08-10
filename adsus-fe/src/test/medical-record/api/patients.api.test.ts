import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { searchPatients } from "@/features/medical-record/api/patients.api";

const emptyPage = { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 1 };

describe("searchPatients", () => {
  it("bỏ hẳn tham số rỗng thay vì gửi chuỗi rỗng", async () => {
    let receivedUrl = "";
    server.use(
      http.get(`${API_BASE_URL}/api/v1/patients`, ({ request }) => {
        receivedUrl = request.url;
        return HttpResponse.json({ code: 200, message: "ok", data: emptyPage });
      }),
    );

    await searchPatients({ search: "", page: 1, pageSize: 20 });

    // Gửi search= rỗng khiến backend hiểu là "lọc theo chuỗi rỗng" thay vì "không lọc".
    expect(receivedUrl).not.toContain("search=");
  });

  it("gửi hasProfile=false khi được yêu cầu", async () => {
    let receivedUrl = "";
    server.use(
      http.get(`${API_BASE_URL}/api/v1/patients`, ({ request }) => {
        receivedUrl = request.url;
        return HttpResponse.json({ code: 200, message: "ok", data: emptyPage });
      }),
    );

    await searchPatients({ hasProfile: false });

    // Luồng tạo hồ sơ nền (#17) chỉ được chọn tài khoản CHƯA có hồ sơ.
    expect(receivedUrl).toContain("hasProfile=false");
  });

  it("giữ nguyên patientProfileId null của tài khoản chưa có hồ sơ nền", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/patients`, () =>
        HttpResponse.json({
          code: 200,
          message: "ok",
          data: {
            ...emptyPage,
            items: [
              {
                patientProfileId: null,
                patientUserId: "user-1",
                fullName: "Phạm Hồng Hạnh",
                phone: "0912345678",
                latestVisitDate: null,
                latestVisitStatus: null,
              },
            ],
            totalItems: 1,
          },
        }),
      ),
    );

    const result = await searchPatients({});

    expect(result.items[0].patientProfileId).toBeNull();
  });

  it("throw khi API trả data null trên response 200", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/patients`, () =>
        HttpResponse.json({ code: 200, message: "Something broke", data: null }),
      ),
    );

    // data null trên 200 là lỗi backend, không phải "kết quả rỗng hợp lệ" — kết quả rỗng
    // vẫn phải là một PagedResult có items: [].
    await expect(searchPatients({})).rejects.toThrow("Something broke");
  });
});
