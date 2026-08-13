import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { createUser } from "@/features/user-role-management/api/users.api";

describe("createUser", () => {
  it("trả về tài khoản kèm mật khẩu tạm — sửa 12/08/2026, không còn gửi qua email", async () => {
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
    const temporaryPassword = "Aa1b2c3d4e";

    server.use(
      http.post(`${API_BASE_URL}/api/v1/admin/users`, () =>
        HttpResponse.json(
          {
            code: "USER_CREATED",
            message:
              "Account created. Temporary password generated — communicate it to the account holder directly.",
            data: { account, temporaryPassword },
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
    ).resolves.toEqual({ account, temporaryPassword });
  });
});
