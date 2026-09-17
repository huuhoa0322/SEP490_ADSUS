import { describe, expect, it } from "vitest";
import { containsHtmlTags, formatCurrency, formatMetricPercent } from "@/lib/utils";

describe("formatCurrency", () => {
  it("formats positive numbers as VND currency", () => {
    const formatted = formatCurrency(50000);
    expect(formatted).toMatch(/50\.000/);
  });
});

describe("formatMetricPercent", () => {
  describe("Defensive empty / invalid value handling", () => {
    it("returns 'Chưa có dữ liệu' for null", () => {
      expect(formatMetricPercent(null)).toBe("Chưa có dữ liệu");
    });

    it("returns 'Chưa có dữ liệu' for undefined", () => {
      expect(formatMetricPercent(undefined)).toBe("Chưa có dữ liệu");
    });

    it("returns 'Chưa có dữ liệu' when called with no arguments", () => {
      expect(formatMetricPercent()).toBe("Chưa có dữ liệu");
    });

    it("returns 'Chưa có dữ liệu' for NaN", () => {
      expect(formatMetricPercent(NaN)).toBe("Chưa có dữ liệu");
      expect(formatMetricPercent(0 / 0)).toBe("Chưa có dữ liệu");
    });

    it("returns 'Chưa có dữ liệu' for Infinity and -Infinity", () => {
      expect(formatMetricPercent(Infinity)).toBe("Chưa có dữ liệu");
      expect(formatMetricPercent(-Infinity)).toBe("Chưa có dữ liệu");
      expect(formatMetricPercent(1 / 0)).toBe("Chưa có dữ liệu");
    });
  });

  describe("Ratio handling (isRatio = true)", () => {
    it("multiplies ratio [0..1] by 100 and formats as percentage", () => {
      expect(formatMetricPercent(0.8523, true)).toBe("85.2%");
      expect(formatMetricPercent(0.9, true)).toBe("90.0%");
      expect(formatMetricPercent(1.0, true)).toBe("100.0%");
    });

    it("formats 0 ratio cleanly as 0.0%", () => {
      expect(formatMetricPercent(0, true)).toBe("0.0%");
    });

    it("supports custom decimal places with ratio", () => {
      expect(formatMetricPercent(0.8526, true, 2)).toBe("85.26%");
      expect(formatMetricPercent(0.8526, true, 0)).toBe("85%");
    });
  });

  describe("Percentage handling (isRatio = false)", () => {
    it("formats percentage [0..100] directly without multiplying", () => {
      expect(formatMetricPercent(85.23, false)).toBe("85.2%");
      expect(formatMetricPercent(100, false)).toBe("100.0%");
      expect(formatMetricPercent(0, false)).toBe("0.0%");
    });

    it("defaults isRatio to false if omitted", () => {
      expect(formatMetricPercent(75.5)).toBe("75.5%");
    });
  });

  describe("Clamping behavior", () => {
    it("clamps negative values to 0.0%", () => {
      expect(formatMetricPercent(-5, false)).toBe("0.0%");
      expect(formatMetricPercent(-0.1, true)).toBe("0.0%");
    });

    it("clamps values greater than 100 to 100.0%", () => {
      expect(formatMetricPercent(105, false)).toBe("100.0%");
      expect(formatMetricPercent(1.5, true)).toBe("100.0%");
    });
  });

  describe("Options object overload", () => {
    it("supports FormatMetricPercentOptions object", () => {
      expect(formatMetricPercent(0.852, { isRatio: true, decimals: 1 })).toBe("85.2%");
      expect(formatMetricPercent(85.2, { isRatio: false, decimals: 1 })).toBe("85.2%");
      expect(formatMetricPercent(null, { fallback: "N/A" })).toBe("N/A");
    });
  });
});

describe("containsHtmlTags", () => {
  it("detects opening, closing, and self-closing HTML tags", () => {
    expect(containsHtmlTags("<html>")).toBe(true);
    expect(containsHtmlTags("<b>bold</b>")).toBe(true);
    expect(containsHtmlTags("<script>alert(1)</script>")).toBe(true);
    expect(containsHtmlTags("<img src=x onerror=alert(1)>")).toBe(true);
    expect(containsHtmlTags("</div>")).toBe(true);
    expect(containsHtmlTags("<br/>")).toBe(true);
    expect(containsHtmlTags("<iframe src='evil.com'></iframe>")).toBe(true);
  });

  it("safely permits medical comparison operators and valid text", () => {
    expect(containsHtmlTags("Khám tổng quát")).toBe(false);
    expect(containsHtmlTags("Đau bụng < 3 ngày")).toBe(false);
    expect(containsHtmlTags("Nhiệt độ > 38.5°C")).toBe(false);
    expect(containsHtmlTags("Sốt <39°C")).toBe(false);
    expect(containsHtmlTags("Bạch cầu < 4.0 và SpO2 > 95%")).toBe(false);
    expect(containsHtmlTags("HA > 140/90 mmHg")).toBe(false);
  });

  it("returns false for falsy or empty inputs", () => {
    expect(containsHtmlTags("")).toBe(false);
    expect(containsHtmlTags(null)).toBe(false);
    expect(containsHtmlTags(undefined)).toBe(false);
  });
});

