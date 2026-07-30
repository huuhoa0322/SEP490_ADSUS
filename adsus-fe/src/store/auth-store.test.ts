import { describe, expect, it } from "vitest";

import type { Role } from "@/types/api.types";

import { getHomePathForRole, isRoleAllowedOnPath } from "./auth-store";

/**
 * Điều hướng và phân quyền theo vai trò.
 *
 * Đây là chỗ duy nhất trên Web dịch "vai trò" thành "được vào đâu", nên cũng là chỗ duy
 * nhất cần khẳng định lại PRD §3.2 Permission Matrix và UC-01 BR-03.
 */
describe("getHomePathForRole — UC-01 BR-03", () => {
  it("Admin vào màn thống kê", () => {
    expect(getHomePathForRole("ADMIN")).toBe("/dashboard");
  });

  it("Doctor và Nurse vào cùng một chỗ", () => {
    // Quyết định ghi đè PRD trong UCS: Nurse là giá trị vai trò riêng nhưng quyền hạn
    // giống hệt Doctor. Nếu ai đó lỡ tay tách hai vai trò này ra, test sẽ đỏ.
    expect(getHomePathForRole("NURSE")).toBe(getHomePathForRole("DOCTOR"));
    expect(getHomePathForRole("DOCTOR")).toBe("/patients");
  });
});

describe("isRoleAllowedOnPath — PRD §3.2 Permission Matrix", () => {
  it('chỉ Admin xem được màn thống kê ("Statistics dashboard | View")', () => {
    expect(isRoleAllowedOnPath("ADMIN", "/dashboard")).toBe(true);

    for (const role of ["DOCTOR", "NURSE", "PATIENT"] satisfies Role[]) {
      expect(isRoleAllowedOnPath(role, "/dashboard")).toBe(false);
    }
  });

  it("Admin KHÔNG vào danh sách bệnh nhân (UC-09)", () => {
    // Admin quản lý tài khoản ở SCR-06, không đụng tới màn lâm sàng.
    expect(isRoleAllowedOnPath("ADMIN", "/patients")).toBe(false);
  });

  it("Doctor và Nurse đều vào được danh sách bệnh nhân", () => {
    expect(isRoleAllowedOnPath("DOCTOR", "/patients")).toBe(true);
    expect(isRoleAllowedOnPath("NURSE", "/patients")).toBe(true);
  });

  it("luật áp cho cả đường dẫn con", () => {
    // /patients/123 cũng phải bị chặn với Admin, không chỉ đúng /patients.
    expect(isRoleAllowedOnPath("ADMIN", "/patients/123")).toBe(false);
    expect(isRoleAllowedOnPath("DOCTOR", "/patients/123")).toBe(true);
  });

  it("chỉ Admin vào được khu quản lý tài khoản (UC-04)", () => {
    // Bảng quyền: Create, Lock/Deactivate, Assign role đều là No cho Doctor/Nurse/Patient.
    // Đây là chỗ đầu tiên NURSE bị chặn trong khi DOCTOR cũng bị chặn — hai vai trò này
    // giống nhau ở mọi màn lâm sàng nên rất dễ bị hiểu nhầm là giống nhau ở mọi nơi.
    expect(isRoleAllowedOnPath("ADMIN", "/admin/users")).toBe(true);
    expect(isRoleAllowedOnPath("ADMIN", "/admin/users/new")).toBe(true);

    for (const role of ["DOCTOR", "NURSE", "PATIENT"] satisfies Role[]) {
      expect(isRoleAllowedOnPath(role, "/admin/users")).toBe(false);
      expect(isRoleAllowedOnPath(role, "/admin/users/new")).toBe(false);
    }
  });

  it('đổi mật khẩu thì mọi vai trò đều vào được ("Change own password" = Full)', () => {
    for (const role of ["ADMIN", "DOCTOR", "NURSE", "PATIENT"] satisfies Role[]) {
      expect(isRoleAllowedOnPath(role, "/change-password")).toBe(true);
    }
  });
});
