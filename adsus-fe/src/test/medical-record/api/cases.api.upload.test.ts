// @vitest-environment node
//
// Tách khỏi cases.api.test.ts vì hai nhóm test cần hai môi trường khác nhau, không thể ở
// chung một file:
//
// - Nhóm này (tải ảnh, multipart): PHẢI chạy dưới Node. Lớp File/FormData của jsdom nằm ở
//   realm khác với undici (thư viện MSW dùng để bóc thân multipart), nên undici tự kiểm
//   webidl.is.File() và từ chối File do jsdom tạo, dù nội dung byte hoàn toàn đúng.
// - Nhóm downloadCaseReport (cases.api.test.ts): PHẢI chạy dưới jsdom. Dưới Node, axios rơi
//   về adapter "http", mà adapter đó chỉ dựng Blob cho data-URI chứ không cho response HTTP
//   thường (axios/lib/adapters/http.js) — responseType "blob" bị bỏ qua và trả về chuỗi.
//
// Cả hai đều là giới hạn của công cụ test, không phải lỗi của cases.api.ts: trên trình duyệt
// thật chỉ có một realm và adapter xhr hỗ trợ blob đầy đủ.
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { createCase } from "@/features/medical-record/api/cases.api";

function fakeImage(name: string): File {
  return new File([new Uint8Array([0xff, 0xd8, 0xff, 0xe0])], name, { type: "image/jpeg" });
}

describe("createCase", () => {
  it("gửi multipart với nhiều ảnh dưới CÙNG một khoá images", async () => {
    let fileNames: string[] = [];
    server.use(
      http.post(`${API_BASE_URL}/api/v1/cases`, async ({ request }) => {
        const form = await request.formData();
        fileNames = form.getAll("images").map((f) => (f as File).name);
        return HttpResponse.json({ code: 200, message: "ok", data: { caseId: "case-1" } }, { status: 201 });
      }),
    );

    await createCase({
      patientProfileId: "profile-1",
      responsibleDoctorId: "doctor-1",
      clinicalInfo: "Rong kinh 3 tuần",
      symptoms: [],
      images: [fakeImage("a.jpg"), fakeImage("b.jpg")],
    });

    // Backend nhận List<IFormFile> images — phải append nhiều lần cùng khoá "images",
    // KHÔNG phải "images[]". Sai chỗ này thì server nhận được 0 file và trả 422.
    expect(fileNames).toEqual(["a.jpg", "b.jpg"]);
  });
});
