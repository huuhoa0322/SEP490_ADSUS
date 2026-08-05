// Chạy dưới jsdom (mặc định của dự án) — KHÔNG đổi sang môi trường node.
//
// downloadCaseReport dùng responseType "blob". Dưới Node, axios rơi về adapter "http", mà
// adapter đó chỉ dựng Blob cho data-URI chứ không cho response HTTP thường
// (axios/lib/adapters/http.js) — responseType bị bỏ qua và hàm trả về chuỗi thay vì Blob.
// Chỉ adapter "xhr" của jsdom mới hỗ trợ blob đầy đủ.
//
// Phần test tải ảnh (multipart) nằm ở cases.api.upload.test.ts vì nó cần môi trường ngược
// lại — xem chú thích đầu file đó.
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { downloadCaseReport } from "@/features/medical-record/api/cases.api";

describe("downloadCaseReport", () => {
  it("trả về Blob khi thành công", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/cases/case-1/report`, () =>
        HttpResponse.arrayBuffer(new Uint8Array([0x25, 0x50, 0x44, 0x46]).buffer, {
          headers: { "Content-Type": "application/pdf" },
        }),
      ),
    );

    const blob = await downloadCaseReport("case-1");

    expect(blob).toBeInstanceOf(Blob);
  });

  it("đọc được thông báo lỗi dù axios đã ép body thành Blob", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/cases/case-1/report`, () =>
        HttpResponse.json(
          { code: 422, message: "The case is not confirmed yet.", data: null },
          { status: 422 },
        ),
      ),
    );

    // Cái bẫy của endpoint này: đã ép responseType "blob" cho nhánh thành công, nên nhánh
    // lỗi cũng về dưới dạng Blob. Không đọc ngược Blob về text thì mọi lỗi đều hiện thông
    // báo trống và người dùng không biết vì sao không xuất được PDF.
    await expect(downloadCaseReport("case-1")).rejects.toThrow("The case is not confirmed yet.");
  });

  it("rơi về lỗi gốc khi thân phản hồi không phải JSON", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/cases/case-1/report`, () =>
        HttpResponse.text("<html><body>502 Bad Gateway</body></html>", { status: 502 }),
      ),
    );

    // Gateway/proxy chen vào giữa đường trả HTML thay vì JSON — JSON.parse thất bại. Phải
    // rơi về lỗi axios gốc (rejects.toThrow không cần chuỗi cụ thể) chứ không phải để lộ
    // SyntaxError của JSON.parse ra ngoài — đó là lỗi thực thi bên trong, không phải thông
    // báo có ý nghĩa cho người dùng.
    await expect(downloadCaseReport("case-1")).rejects.not.toThrow(/Unexpected token|JSON/);
  });
});
