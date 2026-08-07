import { describe, expect, it } from "vitest";

import type { Role } from "@/types/api.types";

import { getHomePathForRole, isRoleAllowedOnPath, useAuthStore } from "./auth-store";

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

describe("Bất biến chống treo màn hình", () => {
  it("đích đến của mỗi vai trò phải là nơi chính vai trò đó được vào", () => {
    // Đây là bất biến giữ cho AuthGuard không quay vòng vô tận.
    //
    // AuthGuard thấy sai khu vực thì đưa người dùng về getHomePathForRole(role). Nếu đích
    // đó lại là nơi vai trò ấy KHÔNG được vào, guard sẽ đẩy đi đẩy lại mãi và màn hình kẹt
    // cứng ở vòng quay "đang kiểm tra phiên đăng nhập" — không lỗi, không nội dung, không
    // làm gì được.
    //
    // Test này đỏ ngay khi ai đó thêm một luật vào ROUTE_ROLES mà quên chỉnh đích đến.
    for (const role of ["ADMIN", "DOCTOR", "NURSE", "PATIENT"] satisfies Role[]) {
      const home = getHomePathForRole(role);

      expect(
        isRoleAllowedOnPath(role, home),
        `Vai trò ${role} bị đưa về ${home} nhưng lại không được vào đó`,
      ).toBe(true);
    }
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

describe("AuthUser.userId", () => {
  it("lưu userId vào phiên khi đăng nhập", () => {
    // GB-04 — form tạo ca khám cần id của chính người đang đăng nhập để điền sẵn ô Bác sĩ
    // phụ trách. Backend không suy ra được giá trị này từ token.
    useAuthStore.getState().signIn("token-abc", {
      userId: "user-42",
      fullName: "BS. Nguyễn Văn An",
      email: "an@example.com",
      role: "DOCTOR",
      mustChangePassword: false,
    });

    expect(useAuthStore.getState().user?.userId).toBe("user-42");
  });
});
