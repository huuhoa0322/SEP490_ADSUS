import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { createUser } from "./users.api";

describe("createUser", () => {
  it("giữ và dịch cảnh báo gửi email của API khi tài khoản vẫn được tạo", async () => {
    const warning =
      "Account created, but the temporary password could not be emailed. Use Reset password to try sending it again.";
    const account = {
      userId: "user-123",
      phoneNumber: "0900000123",
      fullName: "Nguyễn Văn Test",
      email: "test@example.com",
      role: "DOCTOR" as const,
      status: "ACTIVE" as const,
      dateOfBirth: "1990-01-01",
      mustChangePassword: true,
      createdAt: "2026-07-31T10:00:00Z",
      isCurrentUser: false,
    };

    server.use(
      http.post(`${API_BASE_URL}/api/v1/admin/users`, () =>
        HttpResponse.json(
          {
            code: "USER_CREATED",
            message: warning,
            data: account,
          },
          { status: 201 },
        ),
      ),
    );

    await expect(
      createUser({
        phoneNumber: account.phoneNumber,
        fullName: account.fullName,
        email: account.email,
        role: account.role,
        dateOfBirth: account.dateOfBirth,
      }),
    ).resolves.toEqual({
      account,
      message:
        "Đã tạo tài khoản, nhưng không gửi được email chứa mật khẩu tạm. Hãy bấm Cấp lại mật khẩu để gửi lại.",
    });
  });
});
