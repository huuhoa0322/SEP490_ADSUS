import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import {
  createPatientProfile,
  updatePatientProfile,
} from "@/features/medical-record/api/patient-profiles.api";

const profile = {
  patientProfileId: "profile-1",
  patientUserId: "user-1",
  fullName: "Lê Thị Hoa",
  phone: "0978123456",
  dateOfBirth: "1984-03-12",
  gender: "FEMALE" as const,
  medicalHistory: "Đã từng có u lành tính",
  allergies: "Penicillin",
  createdBy: "nurse-1",
  createdAt: "2026-08-04T09:00:00Z",
  updatedAt: "2026-08-04T09:00:00Z",
};

describe("createPatientProfile", () => {
  it("gửi gender null được khi bỏ trống", async () => {
    let body: unknown;
    server.use(
      http.post(`${API_BASE_URL}/api/v1/patient-profiles`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json({ code: 200, message: "ok", data: profile }, { status: 201 });
      }),
    );

    await createPatientProfile({
      patientUserId: "user-1",
      gender: null,
      medicalHistory: null,
      allergies: null,
    });

    // #17 cho phép bỏ trống gender (DB có default), khác #18 vốn bắt buộc.
    expect(body).toMatchObject({ patientUserId: "user-1", gender: null });
  });

  it("throw khi API trả data null", async () => {
    server.use(
      http.post(`${API_BASE_URL}/api/v1/patient-profiles`, () =>
        HttpResponse.json({ code: 409, message: "Profile already exists.", data: null }),
      ),
    );

    await expect(
      createPatientProfile({
        patientUserId: "user-1",
        gender: "FEMALE",
        medicalHistory: null,
        allergies: null,
      }),
    ).rejects.toThrow("Profile already exists.");
  });
});

describe("updatePatientProfile", () => {
  it("không gửi họ tên / SĐT / ngày sinh", async () => {
    let body: Record<string, unknown> = {};
    server.use(
      http.put(`${API_BASE_URL}/api/v1/patient-profiles/profile-1`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({ code: 200, message: "ok", data: profile });
      }),
    );

    await updatePatientProfile("profile-1", {
      gender: "FEMALE",
      medicalHistory: "Cập nhật sau tái khám",
      allergies: null,
    });

    // UC-06 bước 2 — ba trường định danh lấy từ bảng users, chỉ đọc. #18 không nhận chúng;
    // gửi thừa lên là hiểu sai hợp đồng.
    expect(body).not.toHaveProperty("fullName");
    expect(body).not.toHaveProperty("phone");
    expect(body).not.toHaveProperty("dateOfBirth");
  });
});
