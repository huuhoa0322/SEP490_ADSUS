import { describe, expect, it } from "vitest";
import { containsHtmlTags } from "@/lib/utils";

describe("CHALLENGER 2: Web HTML Tag Blocking & Medical Notation Oracle", () => {
  describe("Malicious HTML & XSS vectors must ALL be detected", () => {
    const maliciousPayloads = [
      "<html>",
      "</html>",
      "<b>Đậm</b>",
      "<i>Nghiêng</i>",
      "<u>Gạch chân</u>",
      "<p>Đoạn văn</p>",
      "<div>Khối</div>",
      "</span>",
      "<br/>",
      "<br />",
      "<hr>",
      "<script>alert('XSS')</script>",
      "<SCRIPT SRC='https://evil.com/xss.js'></SCRIPT>",
      "<img src=x onerror=alert(1)>",
      "<svg/onload=alert(1)>",
      "<body onload=alert(1)>",
      "<iframe src='javascript:alert(1)'></iframe>",
      "<input type='text' value='bad'/>",
      "<textarea>nội dung</textarea>",
      "<a href='javascript:alert(1)'>Link độc</a>",
      "<style>body { display: none; }</style>",
      "<link rel='stylesheet' href='evil.css'>",
      "<object data='evil.swf'></object>",
      "<embed src='evil.swf'>",
      "<meta http-equiv='refresh' content='0;url=http://evil.com'>",
      "</b >",
      "<tag-name-custom>",
    ];

    it.each(maliciousPayloads)("detects HTML tag: %s", (payload) => {
      expect(containsHtmlTags(payload)).toBe(true);
    });
  });

  describe("Legitimate clinical & medical inequality notations must NEVER be falsely blocked", () => {
    const medicalNotes = [
      "Khám thai định kỳ 12 tuần",
      "Đau bụng âm ỉ < 3 ngày",
      "Sốt cao > 38.5°C liên tục 2 ngày",
      "Khó thở khi gắng sức, SpO2 > 95%",
      "Huyết áp tụt, HA < 90/60 mmHg",
      "Trẻ em < 5 tuổi sốt co giật",
      "Người già > 65 tuổi suy tim độ 2",
      "Bạch cầu máu < 4.0 G/L",
      "Đường huyết lúc đói > 7.0 mmol/L",
      "HbA1c > 6.5%",
      "Creatinine huyết thanh > 1.2 mg/dL",
      "eGFR < 60 mL/min/1.73m2",
      "pH máu < 7.35 toan chuyển hóa",
      "Tiểu cầu < 100 G/L",
      "Nhiệt độ cơ thể dao động từ > 37°C đến < 39°C",
      "Đau hạ vị < 24 giờ sau ăn",
    ];

    it.each(medicalNotes)("safely permits clinical notation: %s", (note) => {
      expect(containsHtmlTags(note)).toBe(false);
    });
  });

  describe("Defensive boundaries: empty, whitespace, null, undefined", () => {
    it("returns false for null", () => {
      expect(containsHtmlTags(null)).toBe(false);
    });

    it("returns false for undefined", () => {
      expect(containsHtmlTags(undefined)).toBe(false);
    });

    it("returns false for empty string", () => {
      expect(containsHtmlTags("")).toBe(false);
    });

    it("returns false for whitespace-only strings", () => {
      expect(containsHtmlTags("   ")).toBe(false);
      expect(containsHtmlTags("\n\t  \r")).toBe(false);
    });
  });

  describe("Hybrid inputs: legitimate medical notes containing injected HTML tags", () => {
    it("detects tags even when embedded in clinical text", () => {
      expect(containsHtmlTags("Bệnh nhân sốt < 3 ngày nhưng có <b>dấu hiệu lạ</b>")).toBe(true);
      expect(containsHtmlTags("Nhiệt độ > 38°C <script>fetch('http://evil.com')</script>")).toBe(true);
      expect(containsHtmlTags("HA < 120/80 mmHg <img src=x onerror=alert(1)>")).toBe(true);
    });
  });

  describe("Form blocking logic invariants across Mobile and Web", () => {
    it("booking-view: blocks submission when reason has HTML tags", () => {
      const dirtyReason = "Đau bụng <script>alert(1)</script>";
      const isBlocked = Boolean(dirtyReason && containsHtmlTags(dirtyReason));
      expect(isBlocked).toBe(true);

      const cleanReason = "Đau bụng < 3 ngày, sốt > 38°C";
      const isCleanBlocked = Boolean(cleanReason && containsHtmlTags(cleanReason));
      expect(isCleanBlocked).toBe(false);
    });

    it("book-appointment-modal: blocks submission when reason has HTML tags", () => {
      const dirtyReason = "<b>Khám tổng quát</b>";
      const isBlocked = Boolean(dirtyReason && containsHtmlTags(dirtyReason));
      expect(isBlocked).toBe(true);

      const cleanReason = "Khám thai định kỳ < 2 tuần";
      const isCleanBlocked = Boolean(cleanReason && containsHtmlTags(cleanReason));
      expect(isCleanBlocked).toBe(false);
    });

    it("appointment-detail-modal: independently blocks rescheduleReason or newReason", () => {
      // 1. Reschedule reason with HTML
      const dirtyRescheduleReason = "<i>Bác sĩ bận đột xuất</i>";
      const cleanNewReason = "Tái khám theo hẹn";
      expect(containsHtmlTags(dirtyRescheduleReason)).toBe(true);
      expect(containsHtmlTags(cleanNewReason)).toBe(false);

      // 2. New reason with HTML
      const cleanRescheduleReason = "Bệnh nhân dời lịch";
      const dirtyNewReason = "<img src=x onerror=alert(1)>";
      expect(containsHtmlTags(cleanRescheduleReason)).toBe(false);
      expect(containsHtmlTags(dirtyNewReason)).toBe(true);

      // 3. Both clean with clinical inequality
      const medRescheduleReason = "Đổi lịch vì sốt > 39°C";
      const medNewReason = "Cần theo dõi sát HA < 90/60 mmHg";
      expect(containsHtmlTags(medRescheduleReason)).toBe(false);
      expect(containsHtmlTags(medNewReason)).toBe(false);
    });
  });
});
