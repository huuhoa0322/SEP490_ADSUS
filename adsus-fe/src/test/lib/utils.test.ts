import { describe, expect, it } from "vitest";
import { formatCurrency, formatMetricPercent } from "@/lib/utils";

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
