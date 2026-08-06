import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import {
  createPatientAccount,
  resetPatientAccountPassword,
} from "@/features/medical-record/api/patient-accounts.api";

describe("createPatientAccount", () => {
  it("trả tài khoản kèm ngày sinh", async () => {
    server.use(
      http.post(`${API_BASE_URL}/api/v1/patients`, () =>
        HttpResponse.json(
          {
            code: 200,
            message: "Patient account created successfully",
            data: {
              userId: "user-9",
              fullName: "Lê Thị Hoa",
              phoneNumber: "0981234567",
              dateOfBirth: "1984-03-12",
              email: "hoa@example.com",
            },
          },
          { status: 201 },
        ),
      ),
    );

    const account = await createPatientAccount({
      phoneNumber: "0981234567",
      fullName: "Lê Thị Hoa",
      dateOfBirth: "1984-03-12",
      email: "hoa@example.com",
    });

    // Khác hẳn /admin/users của Module 2: ở đó ngày sinh của vai trò PATIENT luôn null
    // (UC-04 BR-01). Ở đây người gọi là Điều dưỡng và ngày sinh là dữ liệu lâm sàng.
    expect(account.dateOfBirth).toBe("1984-03-12");
  });

  it("throw khi API trả data null", async () => {
    server.use(
      http.post(`${API_BASE_URL}/api/v1/patients`, () =>
        HttpResponse.json({ code: 409, message: "This phone number is already registered.", data: null }),
      ),
    );

    await expect(
      createPatientAccount({
        phoneNumber: "0981234567",
        fullName: "Lê Thị Hoa",
        dateOfBirth: null,
        email: null,
      }),
    ).rejects.toThrow("This phone number is already registered.");
  });
});

describe("resetPatientAccountPassword", () => {
  it("gọi đúng đường dẫn và trả về null khi tài khoản có email", async () => {
    let receivedPath = "";
    server.use(
      http.put(`${API_BASE_URL}/api/v1/patients/:userId/reset-password`, ({ request }) => {
        receivedPath = new URL(request.url).pathname;
        return HttpResponse.json({ code: 200, message: "Temporary password sent", data: null });
      }),
    );

    // Quyết định ghi đè 06/08/2026 (Task C11-ext) — tài khoản CÓ email vẫn gửi âm thầm,
    // backend trả `data: null` và hàm truyền nguyên giá trị đó ra ngoài.
    await expect(resetPatientAccountPassword("user-9")).resolves.toBeNull();
    expect(receivedPath).toBe("/api/v1/patients/user-9/reset-password");
  });
});
