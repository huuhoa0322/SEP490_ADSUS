import { describe, expect, it } from "vitest";

import { formatDateTime } from "@/features/user-role-management/lib/user-labels";

describe("formatDateTime", () => {
  it("chuỗi ISO hợp lệ — có đủ ngày/tháng/năm và giờ:phút, không phải 'Invalid Date'", () => {
    const formatted = formatDateTime("2026-01-15T08:30:00Z");

    expect(formatted).toContain("2026");
    expect(formatted).toMatch(/\d{2}:\d{2}/);
    expect(formatted).not.toContain("Invalid");
  });

  it("chuỗi không hợp lệ — trả về gạch ngang thay vì 'Invalid Date'", () => {
    expect(formatDateTime("khong-phai-ngay-thang")).toBe("—");
  });

  it("chuỗi rỗng — trả về gạch ngang", () => {
    expect(formatDateTime("")).toBe("—");
  });
});
