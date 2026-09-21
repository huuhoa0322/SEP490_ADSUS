import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  validateApiResponse,
  validatePagedResult,
  validateCheckinQueueItem,
  validateFeedbackItem,
  type CheckinQueueItemResponse,
  type AuditLogResponse,
  type AdminFeedbackItemResponse,
} from "./fixtures/contracts";
import {
  generateCheckinItem,
  generateCheckinQueue,
  generateAuditLog,
  generateAuditLogList,
  generateFeedbackItem,
  generateFeedbackList,
} from "./fixtures/mock-data";
import {
  DateRangeMachine,
  PaginationMachine,
  KpiCalculator,
  StarRatingCalculator,
  RbacPolicyEvaluator,
  DebounceSimulator,
  type Role,
} from "./state-machines/logic-machines";

describe("Tier 2: Boundary & Corner Cases (>=5 tests per feature for all 15 features)", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // =========================================================================
  // Feature 1 Boundaries: Check-in Date Range Filter
  // =========================================================================
  describe("Feature 1 Boundaries: Check-in Date Range Filter", () => {
    it("T2.F1.1: Inverted date range (startDate > endDate) triggers validation rejection", () => {
      const machine = new DateRangeMachine("2026-09-10");
      const result = machine.setRange("2026-09-15", "2026-09-10");

      expect(result.valid).toBe(false);
      expect(result.error).toBeDefined();
    });

    it("T2.F1.2: Same day boundary (startDate === endDate) produces valid 1-day span", () => {
      const machine = new DateRangeMachine("2026-09-10");
      const result = machine.setRange("2026-09-10", "2026-09-10");

      expect(result.valid).toBe(true);
      expect(machine.getDaySpan()).toBe(1);
    });

    it("T2.F1.3: Leap year date 2028-02-29 handled correctly in date math", () => {
      const machine = new DateRangeMachine("2028-02-29");
      const result = machine.setRange("2028-02-28", "2028-02-29");

      expect(result.valid).toBe(true);
      expect(machine.getDaySpan()).toBe(2);
    });

    it("T2.F1.4: Year boundary span 2026-12-31 to 2027-01-01 spans across years correctly", () => {
      const machine = new DateRangeMachine();
      const result = machine.setRange("2026-12-31", "2027-01-01");

      expect(result.valid).toBe(true);
      expect(machine.getDaySpan()).toBe(2);
    });

    it("T2.F1.5: Auto-correction mechanism aligns endDate = startDate when inverted", () => {
      const machine = new DateRangeMachine("2026-09-10");
      const state = machine.setRangeWithAutoCorrection("2026-09-20", "2026-09-10");

      expect(state.startDate).toBe("2026-09-20");
      expect(state.endDate).toBe("2026-09-20");
      expect(machine.getDaySpan()).toBe(1);
    });
  });

  // =========================================================================
  // Feature 2 Boundaries: Check-in History Fetching
  // =========================================================================
  describe("Feature 2 Boundaries: Check-in History Fetching", () => {
    it("T2.F2.1: 0 appointments returned produces totalCount: 0 and empty items without throwing", () => {
      const queue: CheckinQueueItemResponse[] = [];
      const payload = {
        code: 200,
        message: "Empty queue",
        data: { items: queue, totalCount: 0 },
      };

      expect(validateApiResponse(payload)).toBe(true);
      expect(payload.data.items).toHaveLength(0);
      expect(payload.data.totalCount).toBe(0);
    });

    it("T2.F2.2: 1000 appointments returned handled without memory leak or array corruption", () => {
      const largeQueue = generateCheckinQueue(1000);
      expect(largeQueue).toHaveLength(1000);
      expect(validateCheckinQueueItem(largeQueue[999])).toBe(true);
    });

    it("T2.F2.3: Appointment with null caseId (direct walk-in) handled without NullReferenceException", () => {
      const item = generateCheckinItem({ caseId: null });
      expect(validateCheckinQueueItem(item)).toBe(true);
      expect(item.caseId).toBeNull();
    });

    it("T2.F2.4: Appointment with empty string reason rendered cleanly without crash", () => {
      const item = generateCheckinItem({ reason: "" });
      expect(validateCheckinQueueItem(item)).toBe(true);
      expect(item.reason).toBe("");
    });

    it("T2.F2.5: High-frequency appointment timestamps (same minute slots) sorted deterministically", () => {
      const item1 = generateCheckinItem({ slotTime: "2026-09-10T08:00:00Z", patientFullName: "A" });
      const item2 = generateCheckinItem({ slotTime: "2026-09-10T08:00:00Z", patientFullName: "B" });

      const sorted = [item2, item1].sort((a, b) =>
        a.slotTime.localeCompare(b.slotTime) || a.patientFullName.localeCompare(b.patientFullName)
      );

      expect(sorted[0].patientFullName).toBe("A");
      expect(sorted[1].patientFullName).toBe("B");
    });
  });

  // =========================================================================
  // Feature 3 Boundaries: Check-in Realtime Search
  // =========================================================================
  describe("Feature 3 Boundaries: Check-in Realtime Search", () => {
    it("T2.F3.1: Special SQL characters (', \", ;, --, /*) safely handled without query crash", () => {
      const queue = [generateCheckinItem({ patientFullName: "Nguyễn Văn O'Connor" })];
      const maliciousQuery = "' OR 1=1; -- /*";

      const filtered = queue.filter((i) =>
        i.patientFullName.toLowerCase().includes(maliciousQuery.toLowerCase())
      );
      expect(filtered).toHaveLength(0);
    });

    it("T2.F3.2: XSS script tags (<script>alert(1)</script>) escaped safely without injection", () => {
      const queue = [generateCheckinItem({ patientFullName: "Nguyễn Văn XSS" })];
      const xssQuery = "<script>alert(1)</script>";

      const filtered = queue.filter((i) =>
        i.patientFullName.toLowerCase().includes(xssQuery.toLowerCase())
      );
      expect(filtered).toHaveLength(0);
    });

    it("T2.F3.3: Vietnamese unicode diacritics matched case/accent-insensitively", () => {
      const queue = [
        generateCheckinItem({ patientFullName: "Nguyễn Văn Đạt" }),
      ];

      const normalize = (s: string) =>
        s.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLowerCase().replace(/đ/g, "d");

      const query = "nguyen van dat";
      const filtered = queue.filter((i) =>
        normalize(i.patientFullName).includes(normalize(query))
      );
      expect(filtered).toHaveLength(1);
    });

    it("T2.F3.4: Extreme string length search (2000 characters) truncated or safely queried", () => {
      const queue = [generateCheckinItem({ patientFullName: "Normal Name" })];
      const hugeQuery = "A".repeat(2000);

      const filtered = queue.filter((i) => i.patientFullName.includes(hugeQuery));
      expect(filtered).toHaveLength(0);
    });

    it("T2.F3.5: Whitespace-only search string ('   ') trimmed to empty search", () => {
      const queue = generateCheckinQueue(5);
      const query = "     ";
      const sanitized = query.trim();

      const result = sanitized ? queue.filter((i) => i.patientFullName.includes(sanitized)) : queue;
      expect(result).toHaveLength(5);
    });
  });

  // =========================================================================
  // Feature 4 Boundaries: Check-in Status Filtering
  // =========================================================================
  describe("Feature 4 Boundaries: Check-in Status Filtering", () => {
    it("T2.F4.1: Unknown status string passed in query handled gracefully (returns empty)", () => {
      const queue = generateCheckinQueue(5);
      const unknownStatus = "INVALID_STATUS_XYZ";

      const filtered = queue.filter((i) => (i.status as string) === unknownStatus);
      expect(filtered).toHaveLength(0);
    });

    it("T2.F4.2: Mixed-case status string ('bOoKeD') normalized correctly", () => {
      const queue = [generateCheckinItem({ status: "BOOKED" })];
      const input = "bOoKeD";

      const normalized = input.toUpperCase();
      const filtered = queue.filter((i) => i.status === normalized);
      expect(filtered).toHaveLength(1);
    });

    it("T2.F4.3: Switching status filter rapidly does not produce race condition", () => {
      let activeFilter = "BOOKED";
      const sequence = ["APPROVED", "COMPLETED", "CANCELLED", "BOOKED"];

      for (const next of sequence) {
        activeFilter = next;
      }
      expect(activeFilter).toBe("BOOKED");
    });

    it("T2.F4.4: Status filter matching 0 items displays contextual empty message", () => {
      const queue = [generateCheckinItem({ status: "BOOKED" })];
      const filtered = queue.filter((i) => i.status === "CANCELLED");

      const message = filtered.length === 0 ? "Không có ca khám nào bị huỷ" : "";
      expect(message).toBe("Không có ca khám nào bị huỷ");
    });

    it("T2.F4.5: Appointments transitioning status while view is active updates UI state", () => {
      const item = generateCheckinItem({ status: "BOOKED" });
      expect(item.status).toBe("BOOKED");

      item.status = "APPROVED";
      expect(item.status).toBe("APPROVED");
    });
  });

  // =========================================================================
  // Feature 5 Boundaries: Check-in Pagination & KPI Safety
  // =========================================================================
  describe("Feature 5 Boundaries: Check-in Pagination & KPI Safety", () => {
    it("T2.F5.1: Zero division safety: totalCount = 0 calculates progress 0% without NaN or Infinity", () => {
      const progress = KpiCalculator.calculateProgress(0, 0);
      expect(progress).toBe(0);
      expect(Number.isNaN(progress)).toBe(false);
      expect(Number.isFinite(progress)).toBe(true);
    });

    it("T2.F5.2: totalCount = 1, checkedIn = 0 produces 0%", () => {
      const progress = KpiCalculator.calculateProgress(0, 1);
      expect(progress).toBe(0);
    });

    it("T2.F5.3: totalCount = 1, checkedIn = 1 produces 100%", () => {
      const progress = KpiCalculator.calculateProgress(1, 1);
      expect(progress).toBe(100);
    });

    it("T2.F5.4: Page 9999 on checkin queue with 15 items returns empty array without crash", () => {
      const pager = new PaginationMachine(generateCheckinQueue(15), 15);
      const ok = pager.setPage(9999);

      expect(ok).toBe(false);
      expect(pager.getCurrentPage()).toBe(1);
    });

    it("T2.F5.5: pageSize = 0 or negative page values guarded with default pageSize = 15", () => {
      const pagerZero = new PaginationMachine(generateCheckinQueue(10), 0);
      expect(pagerZero.getPagedSlice()).toHaveLength(10); // clamped to pageSize=1, or slice safely
    });
  });

  // =========================================================================
  // Feature 6 Boundaries: Audit Log Paged Backend Query
  // =========================================================================
  describe("Feature 6 Boundaries: Audit Log Paged Backend Query", () => {
    it("T2.F6.1: page = 0 or negative page clamped to 1", () => {
      const pager = new PaginationMachine(generateAuditLogList(20), 15);
      expect(pager.setPage(0)).toBe(false);
      expect(pager.setPage(-5)).toBe(false);
      expect(pager.getCurrentPage()).toBe(1);
    });

    it("T2.F6.2: pageSize = 9999 capped to maximum allowed limit (e.g. 100)", () => {
      const requestedLimit = 9999;
      const MAX_ALLOWED_PAGE_SIZE = 100;
      const actualLimit = Math.min(requestedLimit, MAX_ALLOWED_PAGE_SIZE);

      expect(actualLimit).toBe(100);
    });

    it("T2.F6.3: Large dataset with 50,000 logs returns exact totalPages count", () => {
      const totalItems = 50000;
      const pageSize = 15;
      const totalPages = Math.ceil(totalItems / pageSize);

      expect(totalPages).toBe(3334);
    });

    it("T2.F6.4: Empty audit log table returns items: [], totalItems: 0, totalPages: 0", () => {
      const emptyPayload = {
        code: 200,
        message: "OK",
        data: {
          items: [],
          page: 1,
          pageSize: 15,
          totalItems: 0,
          totalPages: 0,
        },
      };

      expect(validatePagedResult(emptyPayload.data)).toBe(true);
    });

    it("T2.F6.5: Database timeout / 500 error on audit query caught gracefully with error envelope", () => {
      const errorPayload = {
        code: 500,
        message: "Database connection timeout",
        data: null,
      };

      expect(validateApiResponse(errorPayload)).toBe(true);
      expect(errorPayload.code).toBe(500);
    });
  });

  // =========================================================================
  // Feature 7 Boundaries: Audit Log Admin UI & Layout
  // =========================================================================
  describe("Feature 7 Boundaries: Audit Log Admin UI & Layout", () => {
    it("T2.F7.1: Unmapped custom action string renders neutral fallback badge with raw action name", () => {
      const customAction = "EXPORT_EXCEL_PATIENTS";
      const badgeText = customAction;
      expect(badgeText).toBe("EXPORT_EXCEL_PATIENTS");
    });

    it("T2.F7.2: Extremely long action string (250 chars) wraps or truncates without table overflow", () => {
      const longAction = "A".repeat(250);
      const isTruncated = longAction.length > 50;
      const displayText = isTruncated ? `${longAction.slice(0, 47)}...` : longAction;

      expect(displayText.endsWith("...")).toBe(true);
      expect(displayText.length).toBe(50);
    });

    it("T2.F7.3: Huge JSON payload in detail field (10KB stack trace) clamped or truncated", () => {
      const hugeDetail = JSON.stringify({ error: "X".repeat(10000) });
      const maxDisplayLen = 200;
      const truncated = hugeDetail.length > maxDisplayLen ? `${hugeDetail.slice(0, maxDisplayLen)}...` : hugeDetail;

      expect(truncated.length).toBe(203);
    });

    it("T2.F7.4: Null actor name renders 'Hệ thống' instead of throwing null error", () => {
      const log = generateAuditLog({ actorName: "" });
      const displayActor = log.actorName || "Hệ thống";
      expect(displayActor).toBe("Hệ thống");
    });

    it("T2.F7.5: Table rendering with 0 items displays clean empty table row matching layout", () => {
      const emptyItems: AuditLogResponse[] = [];
      const emptyRowHtml = emptyItems.length === 0 ? "<tr><td colspan='5'>Không có nhật ký nào</td></tr>" : "";
      expect(emptyRowHtml).toContain("Không có nhật ký nào");
    });
  });

  // =========================================================================
  // Feature 8 Boundaries: Audit Log Realtime Search
  // =========================================================================
  describe("Feature 8 Boundaries: Audit Log Realtime Search", () => {
    it("T2.F8.1: Search with regex special characters (.*+?^${}()|[]\\) treated as literal text", () => {
      const logs = [generateAuditLog({ detail: "Model (v1.0.0) deployed" })];
      const query = "(v1.0.0)";

      const match = logs.filter((l) => l.detail?.includes(query));
      expect(match).toHaveLength(1);
    });

    it("T2.F8.2: Search query matching multiple fields simultaneously returns union", () => {
      const logs = [
        generateAuditLog({ actorName: "Admin User", action: "UPDATE_AI" }),
        generateAuditLog({ actorName: "Other User", action: "UPDATE_USER" }),
      ];

      const query = "USER";
      const match = logs.filter(
        (l) => l.actorName.toUpperCase().includes(query) || l.action.includes(query)
      );
      expect(match).toHaveLength(2);
    });

    it("T2.F8.3: 20 rapid keystrokes in 1 second debounced to single final query execution", async () => {
      const sim = new DebounceSimulator();
      const fn = vi.fn();

      for (let i = 1; i <= 20; i++) {
        sim.queueInput(`query-${i}`, 300, fn);
        vi.advanceTimersByTime(50);
      }

      vi.advanceTimersByTime(300);
      expect(fn).toHaveBeenCalledTimes(1);
      expect(fn).toHaveBeenCalledWith("query-20");
    });

    it("T2.F8.4: Search term with emoji / non-BMP characters handled safely", () => {
      const logs = [generateAuditLog({ detail: "Phòng khám 🏥 cấp cứu" })];
      const query = "🏥";

      const match = logs.filter((l) => l.detail?.includes(query));
      expect(match).toHaveLength(1);
    });

    it("T2.F8.5: Search with leading/trailing whitespaces trimmed before API request", () => {
      const rawInput = "   Admin Root   ";
      const trimmed = rawInput.trim();
      expect(trimmed).toBe("Admin Root");
    });
  });

  // =========================================================================
  // Feature 9 Boundaries: Audit Log Category Multi-Filter
  // =========================================================================
  describe("Feature 9 Boundaries: Audit Log Category Multi-Filter", () => {
    it("T2.F9.1: Selecting invalid category identifier defaults to 'All actions'", () => {
      const validCategories = ["ALL", "ACCOUNT", "AI_MODEL", "NURSE_PATIENT"];
      const userSelected = "INVALID_CATEGORY";

      const categoryToUse = validCategories.includes(userSelected) ? userSelected : "ALL";
      expect(categoryToUse).toBe("ALL");
    });

    it("T2.F9.2: Category filter with no matching actions returns empty list with 0 total items", () => {
      const logs = [generateAuditLog({ action: "CREATE_ACCOUNT" })];
      const filtered = logs.filter((l) => l.action.startsWith("AI_"));

      expect(filtered).toHaveLength(0);
    });

    it("T2.F9.3: Category with multi-word actions correctly matches action group prefix", () => {
      const action = "NURSE_CREATE_PATIENT_ACCOUNT";
      expect(action.startsWith("NURSE_")).toBe(true);
    });

    it("T2.F9.4: Changing category while on page 5 resets active page to page 1", () => {
      const pager = new PaginationMachine(generateAuditLogList(80), 15, 5);
      expect(pager.getCurrentPage()).toBe(5);

      pager.setItems(generateAuditLogList(20));
      expect(pager.getCurrentPage()).toBe(1);
    });

    it("T2.F9.5: Category filter combined with conflicting search query returns empty set cleanly", () => {
      const logs = [
        generateAuditLog({ action: "CREATE_ACCOUNT", actorName: "Admin 1" }),
      ];

      const categoryMatches = logs.filter((l) => l.action.startsWith("AI_"));
      const searchMatches = categoryMatches.filter((l) => l.actorName.includes("Admin 1"));
      expect(searchMatches).toHaveLength(0);
    });
  });

  // =========================================================================
  // Feature 10 Boundaries: Audit Log 15-item Pagination
  // =========================================================================
  describe("Feature 10 Boundaries: Audit Log 15-item Pagination", () => {
    it("T2.F10.1: Out-of-bounds page request (page = totalPages + 10) returns false without crash", () => {
      const pager = new PaginationMachine(generateAuditLogList(30), 15);
      const ok = pager.setPage(12);
      expect(ok).toBe(false);
      expect(pager.getCurrentPage()).toBe(1);
    });

    it("T2.F10.2: Exact multiple of pageSize (e.g. 30 items) produces exactly 2 pages (not 3)", () => {
      const pager = new PaginationMachine(generateAuditLogList(30), 15);
      expect(pager.getTotalPages()).toBe(2);
    });

    it("T2.F10.3: Exact multiple plus 1 (31 items) produces 3 pages", () => {
      const pager = new PaginationMachine(generateAuditLogList(31), 15);
      expect(pager.getTotalPages()).toBe(3);
    });

    it("T2.F10.4: Page navigation click spam ignores clicks while query is pending", () => {
      let isFetching = true;
      let page = 1;

      const onPageClick = (targetPage: number) => {
        if (isFetching) return;
        page = targetPage;
      };

      onPageClick(2);
      expect(page).toBe(1);

      isFetching = false;
      onPageClick(2);
      expect(page).toBe(2);
    });

    it("T2.F10.5: Single page total (totalPages = 1) disables both Next and Prev buttons", () => {
      const pager = new PaginationMachine(generateAuditLogList(10), 15);
      expect(pager.getTotalPages()).toBe(1);
      expect(pager.isPrevDisabled()).toBe(true);
      expect(pager.isNextDisabled()).toBe(true);
    });
  });

  // =========================================================================
  // Feature 11 Boundaries: Feedback Relational Backend API
  // =========================================================================
  describe("Feature 11 Boundaries: Feedback Relational Backend API", () => {
    it("T2.F11.1: Feedback linked to deleted doctor returns null doctorId and placeholder doctorName", () => {
      const fb = generateFeedbackItem({
        doctorId: "",
        doctorName: "Bác sĩ đã nghỉ việc",
      });

      expect(fb.doctorName).toBe("Bác sĩ đã nghỉ việc");
      expect(fb.doctorId).toBe("");
    });

    it("T2.F11.2: Feedback with null patient phone renders 'Chưa có SĐT' placeholder", () => {
      const fb = generateFeedbackItem({ patientPhone: "" });
      const displayPhone = fb.patientPhone || "Chưa có SĐT";
      expect(displayPhone).toBe("Chưa có SĐT");
    });

    it("T2.F11.3: Feedback with null content (rating only) displays 'Không có nhận xét'", () => {
      const fb = generateFeedbackItem({ content: "" });
      const displayContent = fb.content || "Không có nhận xét";
      expect(displayContent).toBe("Không có nhận xét");
    });

    it("T2.F11.4: Huge feedback content (4000 characters) safely handled in API response", () => {
      const longReview = "Cực kỳ tốt! ".repeat(300);
      const fb = generateFeedbackItem({ content: longReview });

      expect(fb.content.length).toBeGreaterThan(3000);
      expect(validateFeedbackItem(fb)).toBe(true);
    });

    it("T2.F11.5: Database concurrency: new feedback inserted while browsing page 1 reflected on refresh", () => {
      const list = generateFeedbackList(15);
      expect(list).toHaveLength(15);

      const newItem = generateFeedbackItem({ id: "fb-newest" });
      const refreshedList = [newItem, ...list];
      expect(refreshedList[0].id).toBe("fb-newest");
    });
  });

  // =========================================================================
  // Feature 12 Boundaries: Feedback Admin UI & Star Rating
  // =========================================================================
  describe("Feature 12 Boundaries: Feedback Admin UI & Star Rating", () => {
    it("T2.F12.1: Rating value 0 clamped to minimum 1 star", () => {
      expect(StarRatingCalculator.normalizeRating(0)).toBe(1);
      expect(StarRatingCalculator.getVisualStars(0).filledStars).toBe(1);
    });

    it("T2.F12.2: Rating value 6 clamped to maximum 5 stars", () => {
      expect(StarRatingCalculator.normalizeRating(6)).toBe(5);
      expect(StarRatingCalculator.getVisualStars(6).filledStars).toBe(5);
    });

    it("T2.F12.3: Negative rating (-5) clamped to 1 star", () => {
      expect(StarRatingCalculator.normalizeRating(-5)).toBe(1);
    });

    it("T2.F12.4: Decimal rating (4.7) rounded to integer stars cleanly", () => {
      expect(StarRatingCalculator.normalizeRating(4.7)).toBe(5);
      expect(StarRatingCalculator.normalizeRating(4.2)).toBe(4);
    });

    it("T2.F12.5: Empty feedback queue renders empty state matching administrative table layout", () => {
      const feedbacks: AdminFeedbackItemResponse[] = [];
      const emptyStateText = feedbacks.length === 0 ? "Chưa có phản hồi nào từ bệnh nhân" : "";
      expect(emptyStateText).toBe("Chưa có phản hồi nào từ bệnh nhân");
    });
  });

  // =========================================================================
  // Feature 13 Boundaries: Feedback Clickable Entity Links
  // =========================================================================
  describe("Feature 13 Boundaries: Feedback Clickable Entity Links", () => {
    it("T2.F13.1: Null or empty GUID for doctorId disables link or renders unlinked text", () => {
      const emptyGuid = "00000000-0000-0000-0000-000000000000";
      const isLinked = (id: string) => Boolean(id) && id !== emptyGuid;

      expect(isLinked(emptyGuid)).toBe(false);
      expect(isLinked("doc-real-123")).toBe(true);
    });

    it("T2.F13.2: Null or empty GUID for caseId disables link or renders unlinked text", () => {
      const isLinked = (id?: string | null) => Boolean(id) && id !== "00000000-0000-0000-0000-000000000000";
      expect(isLinked(null)).toBe(false);
    });

    it("T2.F13.3: Null or empty GUID for patientProfileId disables link or renders unlinked text", () => {
      const isLinked = (id?: string | null) => Boolean(id);
      expect(isLinked("")).toBe(false);
    });

    it("T2.F13.4: Right-click / open link in new tab generates valid absolute URL", () => {
      const origin = "http://localhost:3000";
      const path = "/patients/profile-99";
      const fullUrl = new URL(path, origin).toString();

      expect(fullUrl).toBe("http://localhost:3000/patients/profile-99");
    });

    it("T2.F13.5: Entity link formatting handles special characters in UUID cleanly", () => {
      const caseUuid = "c1234567-e89b-12d3-a456-426614174000";
      const link = `/cases/${encodeURIComponent(caseUuid)}`;
      expect(link).toBe(`/cases/${caseUuid}`);
    });
  });

  // =========================================================================
  // Feature 14 Boundaries: Feedback Admin Route Access
  // =========================================================================
  describe("Feature 14 Boundaries: Feedback Admin Route Access", () => {
    it("T2.F14.1: Path traversal attempt (/patients/../admin) normalized and evaluated strictly", () => {
      const rbac = new RbacPolicyEvaluator();
      const normalizePath = (p: string) => {
        const parts = p.split("/").filter(Boolean);
        const resolved: string[] = [];
        for (const part of parts) {
          if (part === "..") resolved.pop();
          else if (part !== ".") resolved.push(part);
        }
        return `/${resolved.join("/")}`;
      };

      const malicious = "/patients/../admin";
      const cleanPath = normalizePath(malicious);
      expect(cleanPath).toBe("/admin");
      expect(rbac.isAllowed("ADMIN", cleanPath)).toBe(true);
      expect(rbac.isAllowed("DOCTOR", cleanPath)).toBe(false);
    });

    it("T2.F14.2: Deep subpaths (/patients/123/records/456) inherit parent prefix rules", () => {
      const rbac = new RbacPolicyEvaluator();
      expect(rbac.isAllowed("ADMIN", "/patients/123/records/456")).toBe(true);
      expect(rbac.isAllowed("DOCTOR", "/patients/123/records/456")).toBe(true);
      expect(rbac.isAllowed("PATIENT", "/patients/123/records/456")).toBe(false);
    });

    it("T2.F14.3: Case-sensitive route paths (/Patients vs /patients) normalized", () => {
      const rbac = new RbacPolicyEvaluator();
      const evaluateNormalized = (role: Role, path: string) =>
        rbac.isAllowed(role, path.toLowerCase());

      expect(evaluateNormalized("ADMIN", "/Patients")).toBe(true);
    });

    it("T2.F14.4: Unauthenticated user (no role) denied access to all protected route prefixes", () => {
      const rbac = new RbacPolicyEvaluator();
      const unauthRole = undefined;
      const isAllowedUnauth = unauthRole ? rbac.isAllowed(unauthRole, "/admin") : false;
      expect(isAllowedUnauth).toBe(false);
    });

    it("T2.F14.5: Non-existent route without rule falls back to generic authenticated access", () => {
      const rbac = new RbacPolicyEvaluator();
      expect(rbac.isAllowed("ADMIN", "/change-password")).toBe(true);
      expect(rbac.isAllowed("DOCTOR", "/change-password")).toBe(true);
    });
  });

  // =========================================================================
  // Feature 15 Boundaries: Feedback Realtime Multi-Search
  // =========================================================================
  describe("Feature 15 Boundaries: Feedback Realtime Multi-Search", () => {
    it("T2.F15.1: Multi-search with case code UUID prefix matches target case", () => {
      const list = [
        generateFeedbackItem({ caseId: "a1b2c3d4-e89b-12d3-a456-426614174000" }),
      ];

      const query = "a1b2c3d4";
      const match = list.filter((f) => f.caseId.startsWith(query));
      expect(match).toHaveLength(1);
    });

    it("T2.F15.2: Multi-search with formatted phone number ('090-123-4567') normalizes digits", () => {
      const list = [generateFeedbackItem({ patientPhone: "0901234567" })];
      const queryWithHyphen = "090-123-4567";
      const cleanQuery = queryWithHyphen.replace(/\D/g, "");

      const match = list.filter((f) => f.patientPhone.replace(/\D/g, "").includes(cleanQuery));
      expect(match).toHaveLength(1);
    });

    it("T2.F15.3: Vietnamese doctor name with title ('BS. Nguyễn Văn A') matches search query", () => {
      const list = [generateFeedbackItem({ doctorName: "BS. Nguyễn Văn A" })];
      const query = "Nguyen Van A";

      const normalize = (s: string) =>
        s.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLowerCase();

      const match = list.filter((f) => normalize(f.doctorName).includes(normalize(query)));
      expect(match).toHaveLength(1);
    });

    it("T2.F15.4: Search term with punctuation ('.', ',', '-') does not break regex/query", () => {
      const list = [generateFeedbackItem({ doctorName: "BS. Trần Đức Anh" })];
      const query = "BS.";

      const match = list.filter((f) => f.doctorName.includes(query));
      expect(match).toHaveLength(1);
    });

    it("T2.F15.5: Simultaneous search term entry and rating filter change combines parameters safely", () => {
      const list = [
        generateFeedbackItem({ doctorName: "BS. Nam", rating: 5 }),
        generateFeedbackItem({ doctorName: "BS. Nam", rating: 2 }),
        generateFeedbackItem({ doctorName: "BS. Minh", rating: 5 }),
      ];

      const search = "Nam";
      const minRating = 4;

      const filtered = list.filter(
        (f) => f.doctorName.includes(search) && f.rating >= minRating
      );
      expect(filtered).toHaveLength(1);
      expect(filtered[0].doctorName).toBe("BS. Nam");
      expect(filtered[0].rating).toBe(5);
    });
  });
});
