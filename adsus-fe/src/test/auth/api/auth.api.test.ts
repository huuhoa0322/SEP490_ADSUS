import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import {
  changePassword,
  forgotPassword,
  login,
} from "@/features/auth/api/auth.api";

describe("login", () => {
  it("trả về data.data khi đăng nhập thành công", async () => {
    const loginResponse = {
      userId: "user-1",
      accessToken: "fake.jwt.token",
      role: "DOCTOR",
      fullName: "Bác sĩ Test",
      email: null,
      mustChangePassword: false,
    };
    server.use(
      http.post(`${API_BASE_URL}/api/v1/auth/login`, () =>
        HttpResponse.json({ code: 200, message: "Login successful.", data: loginResponse }),
      ),
    );

    const result = await login({ phoneNumber: "0900000001", password: "Aa123456@" });

    expect(result).toEqual(loginResponse);
  });

  it("throw đúng message từ backend khi data null trên response 200", async () => {
    // data null trên 200 là lỗi backend (P_FE2) — không phải "kết quả rỗng hợp lệ".
    server.use(
      http.post(`${API_BASE_URL}/api/v1/auth/login`, () =>
        HttpResponse.json({ code: 200, message: "Invalid phone number or password.", data: null }),
      ),
    );

    await expect(
      login({ phoneNumber: "0900000001", password: "wrong" }),
    ).rejects.toThrow("Invalid phone number or password.");
  });

  it("throw câu mặc định khi backend không kèm message", async () => {
    server.use(
      http.post(`${API_BASE_URL}/api/v1/auth/login`, () =>
        HttpResponse.json({ code: 200, message: "", data: null }),
      ),
    );

    await expect(
      login({ phoneNumber: "0900000001", password: "wrong" }),
    ).rejects.toThrow("Đăng nhập thất bại.");
  });
});

describe("forgotPassword", () => {
  it("resolve thành công mà không ném lỗi (AF-01 — luôn cùng 1 kết quả)", async () => {
    server.use(
      http.post(`${API_BASE_URL}/api/v1/auth/forgot-password`, () =>
        HttpResponse.json({
          code: 200,
          message: "If the information is correct, a new password has been sent to your email.",
          data: null,
        }),
      ),
    );

    await expect(
      forgotPassword({ phoneNumber: "0900000001", email: "a@example.com" }),
    ).resolves.toBeUndefined();
  });
});

describe("changePassword", () => {
  it("resolve thành công khi backend trả 200", async () => {
    server.use(
      http.post(`${API_BASE_URL}/api/v1/auth/change-password`, () =>
        HttpResponse.json({ code: 200, message: "Password changed successfully.", data: null }),
      ),
    );

    await expect(
      changePassword({
        currentPassword: "Aa123456@",
        newPassword: "Bb987654@",
        confirmNewPassword: "Bb987654@",
      }),
    ).resolves.toBeUndefined();
  });
});
