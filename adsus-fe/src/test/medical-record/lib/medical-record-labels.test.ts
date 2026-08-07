import { describe, expect, it } from "vitest";

import {
  caseStatusLabel,
  formatIsoDate,
  formatIsoDateTime,
  genderLabel,
} from "@/features/medical-record/lib/medical-record-labels";

describe("caseStatusLabel", () => {
  it("dịch cả ba trạng thái ca khám sang tiếng Việt", () => {
    expect(caseStatusLabel("CREATED")).toBe("Mới tạo");
    expect(caseStatusLabel("ANALYZED")).toBe("Đã phân tích");
    expect(caseStatusLabel("CONFIRMED")).toBe("Đã kết luận");
  });
});

describe("genderLabel", () => {
  it("dịch cả ba giá trị giới tính", () => {
    expect(genderLabel("FEMALE")).toBe("Nữ");
    expect(genderLabel("MALE")).toBe("Nam");
    expect(genderLabel("OTHER")).toBe("Khác");
  });
});

describe("formatIsoDate", () => {
  it("đổi DateOnly của backend sang dd/MM/yyyy", () => {
    expect(formatIsoDate("2026-07-22")).toBe("22/07/2026");
  });

  it("trả dấu gạch khi không có ngày", () => {
    // Nhiều trường ngày ở Module 04 là nullable (ngày sinh, lần khám gần nhất). Trả chuỗi
    // rỗng sẽ làm ô trên bảng trông như lỗi render.
    expect(formatIsoDate(null)).toBe("—");
  });

  it("không lệch ngày do múi giờ", () => {
    // new Date("2026-01-01") được hiểu là UTC; máy ở múi giờ âm sẽ hiển thị 31/12/2025.
    // Hàm này phải cắt chuỗi chứ không đi qua Date.
    expect(formatIsoDate("2026-01-01")).toBe("01/01/2026");
  });
});

describe("formatIsoDateTime", () => {
  it("hiển thị ngày kèm giờ phút", () => {
    expect(formatIsoDateTime("2026-07-22T09:05:00Z")).toMatch(/^22\/07\/2026 \d{2}:\d{2}$/);
  });

  it("trả dấu gạch khi không có giá trị", () => {
    expect(formatIsoDateTime(null)).toBe("—");
  });
});
