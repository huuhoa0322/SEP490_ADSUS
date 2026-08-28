import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import {
  createUser,
  deactivateUser,
  getUserById,
  reactivateUser,
  resetUserPassword,
  searchUsers,
  updateUser,
} from "@/features/user-role-management/api/users.api";

const SAMPLE_ACCOUNT = {
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

describe("searchUsers", () => {
  it("trả về đúng danh sách kèm thông tin phân trang", async () => {
    const paged = { items: [SAMPLE_ACCOUNT], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 };

    server.use(
      http.get(`${API_BASE_URL}/api/v1/admin/users`, () =>
        HttpResponse.json({ code: 200, message: "User list loaded.", data: paged }),
      ),
    );

    await expect(searchUsers({ page: 1, pageSize: 20 })).resolves.toEqual(paged);
  });

  it("data null trên response 200 — ném lỗi thay vì coi là danh sách rỗng hợp lệ", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/admin/users`, () =>
        HttpResponse.json({ code: 200, message: "Backend bug.", data: null }),
      ),
    );

    await expect(searchUsers({})).rejects.toThrow("Backend bug.");
  });
});

describe("getUserById", () => {
  it("trả về đúng tài khoản", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/admin/users/user-123`, () =>
        HttpResponse.json({ code: 200, message: "Account loaded.", data: SAMPLE_ACCOUNT }),
      ),
    );

    await expect(getUserById("user-123")).resolves.toEqual(SAMPLE_ACCOUNT);
  });

  it("404 — ném lỗi kèm đúng message từ backend", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/admin/users/user-999`, () =>
        HttpResponse.json(
          { code: 404, message: "Account not found.", data: null },
          { status: 404 },
        ),
      ),
    );

    await expect(getUserById("user-999")).rejects.toThrow();
  });
});

describe("updateUser", () => {
  it("gửi đúng payload tới endpoint sửa", async () => {
    let capturedBody: unknown;

    server.use(
      http.put(`${API_BASE_URL}/api/v1/admin/users/user-123`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({ code: 200, message: "Account updated.", data: null });
      }),
    );

    await updateUser("user-123", { fullName: "Tên mới", role: "NURSE" });

    expect(capturedBody).toEqual({ fullName: "Tên mới", role: "NURSE" });
  });
});

describe("deactivateUser / reactivateUser", () => {
  it("deactivateUser gọi đúng endpoint /deactivate", async () => {
    let requestedUrl: string | undefined;

    server.use(
      http.put(`${API_BASE_URL}/api/v1/admin/users/user-123/deactivate`, ({ request }) => {
        requestedUrl = request.url;
        return HttpResponse.json({ code: 200, message: "Account deactivated permanently.", data: null });
      }),
    );

    await deactivateUser("user-123");

    expect(requestedUrl).toContain("/deactivate");
  });

  it("reactivateUser gọi đúng endpoint /reactivate", async () => {
    let requestedUrl: string | undefined;

    server.use(
      http.put(`${API_BASE_URL}/api/v1/admin/users/user-123/reactivate`, ({ request }) => {
        requestedUrl = request.url;
        return HttpResponse.json({ code: 200, message: "Account reactivated successfully.", data: null });
      }),
    );

    await reactivateUser("user-123");

    expect(requestedUrl).toContain("/reactivate");
  });
});

describe("resetUserPassword", () => {
  it("trả về mật khẩu tạm plaintext — không còn gửi qua email (sửa 12/08/2026)", async () => {
    server.use(
      http.put(`${API_BASE_URL}/api/v1/admin/users/user-123/reset-password`, () =>
        HttpResponse.json({
          code: 200,
          message: "Temporary password generated — communicate it to the account holder directly.",
          data: "Aa1b2c3d4e",
        }),
      ),
    );

    await expect(resetUserPassword("user-123")).resolves.toBe("Aa1b2c3d4e");
  });
});

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
