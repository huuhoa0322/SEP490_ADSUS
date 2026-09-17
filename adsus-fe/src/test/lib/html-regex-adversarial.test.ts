import { describe, expect, it } from "vitest";
import { containsHtmlTags } from "@/lib/utils";

describe("Adversarial Challenge: containsHtmlTags() in TypeScript", () => {
  it("Group 1: Tricky HTML/XSS vectors must be detected (returns true)", () => {
    const vectors = [
      "<SCRIPT>alert(1)</SCRIPT>",
      '<div style="display:none">',
      "<img/src=x onerror=alert(1)>",
      "<svg onload=alert(1)>",
      '<a href="javascript:...">',
      "<br/>",
      "<hr/>",
      "<textarea></textarea>",
      "</br>",
      "</p>",
      '<IMG SRC="javascript:alert(1);">',
      "<svg/onload=alert(1)>",
      "<BODY ONLOAD=alert(1)>",
      '<iframe src="http://evil.com">',
      "<STYLE>.evil{display:none}</STYLE>",
      '<details ontoggle="alert(1)">',
      "<b>bold</b>",
      '<INPUT TYPE="IMAGE" SRC="javascript:alert(1);">',
    ];

    for (const payload of vectors) {
      expect(
        containsHtmlTags(payload),
        `Failed to detect XSS/HTML payload: ${payload}`
      ).toBe(true);
    }
  });

  it("Group 2: Medical notations must NOT be rejected (returns false)", () => {
    const notations = [
      "Đau bụng < 3 ngày",
      "Nhiệt độ > 38.5°C",
      "Huyết áp < 120/80 mmHg",
      "SpO2 > 95%",
      "Bạch cầu < 4.0 và SpO2 > 95%",
      "Thân nhiệt <38°C",
      "HbA1c < 6.5%",
      "Bạch cầu <4.0",
      "Glucose < 70 mg/dL",
      "Creatinine <1.2 mg/dL",
      "Kali < 3.5 mEq/L",
      "Tiểu cầu <150 G/L",
      "AST < 35 U/L & ALT < 35 U/L",
      "PaO2 < 60 mmHg, PaCO2 > 45 mmHg",
      "Sốt cao > 39°C kéo dài < 2 ngày",
      "Cân nặng < 2500g",
      "Chiều dài đầu mông < 10mm",
    ];

    for (const note of notations) {
      expect(
        containsHtmlTags(note),
        `False positive rejection on medical notation: ${note}`
      ).toBe(false);
    }
  });

  it("Group 3: Null, empty, and whitespace strings (returns false)", () => {
    const blanks = [
      null,
      undefined,
      "",
      "   ",
      "\t\n\r",
      "         ",
    ];

    for (const b of blanks) {
      expect(
        containsHtmlTags(b),
        `Failed on null/empty/whitespace: "${b}"`
      ).toBe(false);
    }
  });
});
