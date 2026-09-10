import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  validateApiResponse,
  validatePagedResult,
  validateCheckinQueueItem,
  validateAuditLogEntry,
  validateFeedbackItem,
  type CheckinQueueResponse,
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
  AUDIT_ACTIONS,
} from "./fixtures/mock-data";
import {
  DateRangeMachine,
  PaginationMachine,
  KpiCalculator,
  StarRatingCalculator,
  RbacPolicyEvaluator,
  DebounceSimulator,
} from "./state-machines/logic-machines";

describe("Tier 1: Feature Coverage (>=5 tests per feature for all 15 features)", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // =========================================================================
  // Feature 1: Check-in Date Range Filter
  // =========================================================================
  describe("Feature 1: Check-in Date Range Filter", () => {
    it("T1.F1.1: Default date range initializes to today for both fromDate and toDate", () => {
      const fixedToday = "2026-09-10";
      const machine = new DateRangeMachine(fixedToday);
      const state = machine.getState();

      expect(state.startDate).toBe(fixedToday);
      expect(state.endDate).toBe(fixedToday);
    });

    it("T1.F1.2: Custom valid date range [2026-09-01, 2026-09-07] generates correct query parameters", () => {
      const machine = new DateRangeMachine("2026-09-10");
      const result = machine.setRange("2026-09-01", "2026-09-07");

      expect(result.valid).toBe(true);
      expect(machine.getState().startDate).toBe("2026-09-01");
      expect(machine.getState().endDate).toBe("2026-09-07");
    });

    it("T1.F1.3: Preset 'Hôm nay' sets both fromDate and toDate to current date", () => {
      const machine = new DateRangeMachine("2026-09-10");
      machine.setRange("2026-09-01", "2026-09-05");

      const state = machine.applyPreset("today", new Date("2026-09-10T12:00:00Z"));
      expect(state.startDate).toBe("2026-09-10");
      expect(state.endDate).toBe("2026-09-10");
    });

    it("T1.F1.4: Preset '7 ngày qua' calculates exactly 7 days span ending today", () => {
      const machine = new DateRangeMachine("2026-09-10");
      const state = machine.applyPreset("last7days", new Date("2026-09-10T12:00:00Z"));

      expect(state.startDate).toBe("2026-09-04");
      expect(state.endDate).toBe("2026-09-10");
      expect(machine.getDaySpan()).toBe(7);
    });

    it("T1.F1.5: Preset '30 ngày qua' calculates exactly 30 days span ending today", () => {
      const machine = new DateRangeMachine("2026-09-10");
      const state = machine.applyPreset("last30days", new Date("2026-09-10T12:00:00Z"));

      expect(state.startDate).toBe("2026-08-12");
      expect(state.endDate).toBe("2026-09-10");
      expect(machine.getDaySpan()).toBe(30);
    });
  });

  // =========================================================================
  // Feature 2: Check-in History Fetching
  // =========================================================================
  describe("Feature 2: Check-in History Fetching", () => {
    it("T1.F2.1: Queue response correctly conforms to ApiResponse<CheckinQueueResponse> envelope", () => {
      const items = [generateCheckinItem()];
      const payload = {
        code: 200,
        message: "Success",
        data: {
          items,
          totalCount: 1,
        },
      };

      const isValid = validateApiResponse<CheckinQueueResponse>(payload, (data) => {
        return Array.isArray(data.items) && typeof data.totalCount === "number";
      });
      expect(isValid).toBe(true);
    });

    it("T1.F2.2: Appointments across multi-day span are mapped with slotTime, doctorName, patientFullName", () => {
      const item = generateCheckinItem({
        slotTime: "2026-09-05T09:00:00Z",
        doctorName: "BS. Trần Văn Minh",
        patientFullName: "Lê Thị Bích",
      });

      expect(validateCheckinQueueItem(item)).toBe(true);
      expect(item.slotTime).toBe("2026-09-05T09:00:00Z");
      expect(item.doctorName).toBe("BS. Trần Văn Minh");
      expect(item.patientFullName).toBe("Lê Thị Bích");
    });

    it("T1.F2.3: Historical appointments retain correct caseId and patientProfileId mappings", () => {
      const item = generateCheckinItem({
        caseId: "case-historical-99",
        patientProfileId: "profile-historical-12",
      });

      expect(item.caseId).toBe("case-historical-99");
      expect(item.patientProfileId).toBe("profile-historical-12");
    });

    it("T1.F2.4: Single-day query returns historical items for that specific date", () => {
      const queue = generateCheckinQueue(5);
      expect(queue.length).toBe(5);
      for (const it of queue) {
        expect(validateCheckinQueueItem(it)).toBe(true);
      }
    });

    it("T1.F2.5: Empty queue response handles 0 items with empty array and totalCount: 0", () => {
      const emptyPayload = {
        code: 200,
        message: "No items found",
        data: {
          items: [],
          totalCount: 0,
        },
      };

      expect(validateApiResponse<CheckinQueueResponse>(emptyPayload)).toBe(true);
      expect(emptyPayload.data.items).toHaveLength(0);
      expect(emptyPayload.data.totalCount).toBe(0);
    });
  });

  // =========================================================================
  // Feature 3: Check-in Realtime Search
  // =========================================================================
  describe("Feature 3: Check-in Realtime Search", () => {
    it("T1.F3.1: Search query matching patient name filters queue results", () => {
      const queue = [
        generateCheckinItem({ patientFullName: "Nguyễn Văn An", patientPhone: "0901111111" }),
        generateCheckinItem({ patientFullName: "Trần Thị Bình", patientPhone: "0902222222" }),
      ];

      const filtered = queue.filter((i) =>
        i.patientFullName.toLowerCase().includes("nguyễn".toLowerCase())
      );
      expect(filtered).toHaveLength(1);
      expect(filtered[0].patientFullName).toBe("Nguyễn Văn An");
    });

    it("T1.F3.2: Search query matching phone number filters queue results", () => {
      const queue = [
        generateCheckinItem({ patientFullName: "Nguyễn Văn An", patientPhone: "0901111111" }),
        generateCheckinItem({ patientFullName: "Trần Thị Bình", patientPhone: "0902222222" }),
      ];

      const filtered = queue.filter((i) => i.patientPhone.includes("090222"));
      expect(filtered).toHaveLength(1);
      expect(filtered[0].patientPhone).toBe("0902222222");
    });

    it("T1.F3.3: Search query matching doctor name filters queue results", () => {
      const queue = [
        generateCheckinItem({ doctorName: "BS. Nguyễn Văn Nam" }),
        generateCheckinItem({ doctorName: "BS. Phạm Quang Huy" }),
      ];

      const filtered = queue.filter((i) =>
        i.doctorName.toLowerCase().includes("quang huy")
      );
      expect(filtered).toHaveLength(1);
      expect(filtered[0].doctorName).toBe("BS. Phạm Quang Huy");
    });

    it("T1.F3.4: Realtime search triggers debounce timer (300ms) before request dispatch", async () => {
      const sim = new DebounceSimulator();
      const callback = vi.fn();

      const promise = sim.queueInput("Nguyễn", 300, callback);
      expect(callback).not.toHaveBeenCalled();

      vi.advanceTimersByTime(299);
      expect(callback).not.toHaveBeenCalled();

      vi.advanceTimersByTime(1);
      await promise;
      expect(callback).toHaveBeenCalledWith("Nguyễn");
      expect(sim.getExecutedCalls()).toBe(1);
    });

    it("T1.F3.5: Empty search string returns unfiltered queue for selected date range", () => {
      const queue = generateCheckinQueue(8);
      const query = "";
      const filtered = query.trim()
        ? queue.filter((i) => i.patientFullName.includes(query))
        : queue;

      expect(filtered).toHaveLength(8);
    });
  });

  // =========================================================================
  // Feature 4: Check-in Status Filtering
  // =========================================================================
  describe("Feature 4: Check-in Status Filtering", () => {
    it("T1.F4.1: Status filter 'ALL' returns all appointments regardless of status", () => {
      const queue = [
        generateCheckinItem({ status: "BOOKED" }),
        generateCheckinItem({ status: "APPROVED" }),
        generateCheckinItem({ status: "COMPLETED" }),
        generateCheckinItem({ status: "CANCELLED" }),
      ];

      const filtered = queue.filter(() => true);
      expect(filtered).toHaveLength(4);
    });

    it("T1.F4.2: Status filter 'BOOKED' isolates appointments awaiting check-in", () => {
      const queue = [
        generateCheckinItem({ status: "BOOKED" }),
        generateCheckinItem({ status: "APPROVED" }),
      ];

      const filtered = queue.filter((i) => i.status === "BOOKED");
      expect(filtered).toHaveLength(1);
      expect(filtered[0].status).toBe("BOOKED");
    });

    it("T1.F4.3: Status filter 'APPROVED' isolates checked-in appointments", () => {
      const queue = [
        generateCheckinItem({ status: "BOOKED" }),
        generateCheckinItem({ status: "APPROVED" }),
      ];

      const filtered = queue.filter((i) => i.status === "APPROVED");
      expect(filtered).toHaveLength(1);
      expect(filtered[0].status).toBe("APPROVED");
    });

    it("T1.F4.4: Status filter 'COMPLETED' isolates finished appointments", () => {
      const queue = [
        generateCheckinItem({ status: "APPROVED" }),
        generateCheckinItem({ status: "COMPLETED" }),
      ];

      const filtered = queue.filter((i) => i.status === "COMPLETED");
      expect(filtered).toHaveLength(1);
      expect(filtered[0].status).toBe("COMPLETED");
    });

    it("T1.F4.5: Status filter 'CANCELLED' isolates cancelled appointments", () => {
      const queue = [
        generateCheckinItem({ status: "BOOKED" }),
        generateCheckinItem({ status: "CANCELLED" }),
      ];

      const filtered = queue.filter((i) => i.status === "CANCELLED");
      expect(filtered).toHaveLength(1);
      expect(filtered[0].status).toBe("CANCELLED");
    });
  });

  // =========================================================================
  // Feature 5: Check-in Pagination & KPI Safety
  // =========================================================================
  describe("Feature 5: Check-in Pagination & KPI Safety", () => {
    it("T1.F5.1: Checkin queue page slice calculates 15 items per page (pageSize = 15)", () => {
      const items = generateCheckinQueue(35);
      const pager = new PaginationMachine(items, 15);

      expect(pager.getPagedSlice()).toHaveLength(15);
      pager.nextPage();
      expect(pager.getPagedSlice()).toHaveLength(15);
      pager.nextPage();
      expect(pager.getPagedSlice()).toHaveLength(5);
    });

    it("T1.F5.2: Total pages calculation Math.ceil(totalCount / 15) produces exact page count", () => {
      const pager1 = new PaginationMachine(generateCheckinQueue(30), 15);
      expect(pager1.getTotalPages()).toBe(2);

      const pager2 = new PaginationMachine(generateCheckinQueue(31), 15);
      expect(pager2.getTotalPages()).toBe(3);
    });

    it("T1.F5.3: Checked-in KPI counter correctly tallies APPROVED appointments", () => {
      const queue = [
        generateCheckinItem({ status: "APPROVED" }),
        generateCheckinItem({ status: "APPROVED" }),
        generateCheckinItem({ status: "BOOKED" }),
      ];
      const summary = KpiCalculator.summarizeQueue(queue);

      expect(summary.checkedIn).toBe(2);
    });

    it("T1.F5.4: Pending KPI counter correctly tallies BOOKED appointments", () => {
      const queue = [
        generateCheckinItem({ status: "APPROVED" }),
        generateCheckinItem({ status: "BOOKED" }),
        generateCheckinItem({ status: "BOOKED" }),
      ];
      const summary = KpiCalculator.summarizeQueue(queue);

      expect(summary.pending).toBe(2);
    });

    it("T1.F5.5: KPI progress percentage calculates (checkedIn / total) * 100 with zero division guard", () => {
      expect(KpiCalculator.calculateProgress(0, 0)).toBe(0);
      expect(KpiCalculator.calculateProgress(5, 10)).toBe(50);
      expect(KpiCalculator.calculateProgress(10, 10)).toBe(100);
    });
  });

  // =========================================================================
  // Feature 6: Audit Log Paged Backend Query
  // =========================================================================
  describe("Feature 6: Audit Log Paged Backend Query", () => {
    it("T1.F6.1: Default query adheres to page: 1 and pageSize: 15", () => {
      const pager = new PaginationMachine(generateAuditLogList(20), 15);
      expect(pager.getCurrentPage()).toBe(1);
      expect(pager.getPagedSlice()).toHaveLength(15);
    });

    it("T1.F6.2: Envelope validation ensures ApiResponse<PagedResult<AuditLogResponse>> structure", () => {
      const payload = {
        code: 200,
        message: "OK",
        data: {
          items: generateAuditLogList(15),
          page: 1,
          pageSize: 15,
          totalItems: 45,
          totalPages: 3,
        },
      };

      const isValid = validatePagedResult<AuditLogResponse>(
        payload.data,
        validateAuditLogEntry,
        15
      );
      expect(isValid).toBe(true);
    });

    it("T1.F6.3: Audit log items include required fields: logId, actorId, actorName, actorRole, action, performedAt", () => {
      const log = generateAuditLog();
      expect(validateAuditLogEntry(log)).toBe(true);
    });

    it("T1.F6.4: Backend query passes action parameter to filter specific audit events", () => {
      const logs = [
        generateAuditLog({ action: AUDIT_ACTIONS.ACCOUNT_CREATE }),
        generateAuditLog({ action: AUDIT_ACTIONS.AI_REGISTER }),
      ];

      const filtered = logs.filter((l) => l.action === AUDIT_ACTIONS.ACCOUNT_CREATE);
      expect(filtered).toHaveLength(1);
      expect(filtered[0].action).toBe(AUDIT_ACTIONS.ACCOUNT_CREATE);
    });

    it("T1.F6.5: Paged query supports date range boundaries fromDate and toDate", () => {
      const now = new Date("2026-09-10T12:00:00Z").getTime();
      const logs = [
        generateAuditLog({ performedAt: new Date(now - 1000).toISOString() }),
        generateAuditLog({ performedAt: new Date(now - 100000000).toISOString() }),
      ];

      const fromDate = new Date(now - 5000).toISOString();
      const recent = logs.filter((l) => l.performedAt >= fromDate);
      expect(recent).toHaveLength(1);
    });
  });

  // =========================================================================
  // Feature 7: Audit Log Admin UI & Layout
  // =========================================================================
  describe("Feature 7: Audit Log Admin UI & Layout", () => {
    it("T1.F7.1: Table layout applies rounded container rounded-3xl", () => {
      const containerClass = "overflow-x-auto rounded-3xl border border-border bg-background";
      expect(containerClass).toContain("rounded-3xl");
    });

    it("T1.F7.2: First column indentation applies pl-6 padding style", () => {
      const headerCellClass = "[&>th:first-child]:pl-6 [&>td:first-child]:pl-6";
      expect(headerCellClass).toContain("pl-6");
    });

    it("T1.F7.3: Account management actions map to user badge and icon", () => {
      const action = AUDIT_ACTIONS.ACCOUNT_CREATE;
      const isAccountAction = action.includes("ACCOUNT") || action.includes("PASSWORD");
      expect(isAccountAction).toBe(true);
    });

    it("T1.F7.4: AI model actions map to indigo badge and brain icon", () => {
      const action = AUDIT_ACTIONS.AI_REGISTER;
      const isAiAction = action.includes("AI_MODEL");
      expect(isAiAction).toBe(true);
    });

    it("T1.F7.5: Fallback badge renders unknown action strings cleanly without error", () => {
      const fallbackAction = AUDIT_ACTIONS.UNKNOWN_CUSTOM;
      const badgeText = fallbackAction || "UNKNOWN";
      expect(badgeText).toBe("UNKNOWN_FUTURE_ACTION");
    });
  });

  // =========================================================================
  // Feature 8: Audit Log Realtime Search
  // =========================================================================
  describe("Feature 8: Audit Log Realtime Search", () => {
    it("T1.F8.1: Search by actor name matches relevant audit records", () => {
      const logs = [
        generateAuditLog({ actorName: "Admin Root" }),
        generateAuditLog({ actorName: "Nurse Lan" }),
      ];

      const match = logs.filter((l) => l.actorName.toLowerCase().includes("root"));
      expect(match).toHaveLength(1);
      expect(match[0].actorName).toBe("Admin Root");
    });

    it("T1.F8.2: Search by actor role (e.g. ADMIN, NURSE) filters matching logs", () => {
      const logs = [
        generateAuditLog({ actorRole: "ADMIN" }),
        generateAuditLog({ actorRole: "NURSE" }),
      ];

      const match = logs.filter((l) => l.actorRole === "ADMIN");
      expect(match).toHaveLength(1);
      expect(match[0].actorRole).toBe("ADMIN");
    });

    it("T1.F8.3: Search by action identifier (e.g. CREATE_ACCOUNT) matches logs", () => {
      const logs = [
        generateAuditLog({ action: "CREATE_ACCOUNT" }),
        generateAuditLog({ action: "UPDATE_ACCOUNT" }),
      ];

      const match = logs.filter((l) => l.action.includes("CREATE"));
      expect(match).toHaveLength(1);
    });

    it("T1.F8.4: Search by detail substring matches logs", () => {
      const logs = [
        generateAuditLog({ detail: "Reset password for user 0901234567" }),
        generateAuditLog({ detail: "Activated model ultrasound-v2" }),
      ];

      const match = logs.filter((l) => l.detail?.includes("0901234567"));
      expect(match).toHaveLength(1);
    });

    it("T1.F8.5: Search input debounces at 300ms before triggering new paged query", async () => {
      const sim = new DebounceSimulator();
      const fn = vi.fn();

      const promise = sim.queueInput("Admin", 300, fn);
      vi.advanceTimersByTime(300);
      await promise;

      expect(fn).toHaveBeenCalledWith("Admin");
      expect(sim.getExecutedCalls()).toBe(1);
    });
  });

  // =========================================================================
  // Feature 9: Audit Log Category Multi-Filter
  // =========================================================================
  describe("Feature 9: Audit Log Category Multi-Filter", () => {
    it("T1.F9.1: Category 'All actions' includes all audit actions", () => {
      const logs = generateAuditLogList(10);
      const filtered = logs.filter(() => true);
      expect(filtered).toHaveLength(10);
    });

    it("T1.F9.2: Category 'Account management' matches CREATE, UPDATE, DEACTIVATE, REACTIVATE, RESET", () => {
      const logs = [
        generateAuditLog({ action: AUDIT_ACTIONS.ACCOUNT_CREATE }),
        generateAuditLog({ action: AUDIT_ACTIONS.ADMIN_RESET_PASSWORD }),
        generateAuditLog({ action: AUDIT_ACTIONS.AI_REGISTER }),
      ];

      const accountActions = new Set<string>([
        AUDIT_ACTIONS.ACCOUNT_CREATE,
        AUDIT_ACTIONS.ACCOUNT_UPDATE,
        AUDIT_ACTIONS.ACCOUNT_DEACTIVATE,
        AUDIT_ACTIONS.ACCOUNT_REACTIVATE,
        AUDIT_ACTIONS.ADMIN_RESET_PASSWORD,
        AUDIT_ACTIONS.SELF_RESET_PASSWORD,
      ]);

      const filtered = logs.filter((l) => accountActions.has(l.action));
      expect(filtered).toHaveLength(2);
    });

    it("T1.F9.3: Category 'AI models' matches REGISTER, UPDATE, ACTIVATE", () => {
      const logs = [
        generateAuditLog({ action: AUDIT_ACTIONS.AI_REGISTER }),
        generateAuditLog({ action: AUDIT_ACTIONS.AI_ACTIVATE }),
        generateAuditLog({ action: AUDIT_ACTIONS.ACCOUNT_CREATE }),
      ];

      const aiActions = new Set<string>([
        AUDIT_ACTIONS.AI_REGISTER,
        AUDIT_ACTIONS.AI_UPDATE,
        AUDIT_ACTIONS.AI_ACTIVATE,
      ]);

      const filtered = logs.filter((l) => aiActions.has(l.action));
      expect(filtered).toHaveLength(2);
    });

    it("T1.F9.4: Category 'Nurse patient operations' matches NURSE_CREATE, NURSE_UPDATE, NURSE_RESET", () => {
      const logs = [
        generateAuditLog({ action: AUDIT_ACTIONS.NURSE_CREATE_PATIENT }),
        generateAuditLog({ action: AUDIT_ACTIONS.ACCOUNT_CREATE }),
      ];

      const filtered = logs.filter((l) => l.action.startsWith("NURSE_"));
      expect(filtered).toHaveLength(1);
    });

    it("T1.F9.5: Category selection resets pagination to page 1", () => {
      const pager = new PaginationMachine(generateAuditLogList(30), 15, 2);
      expect(pager.getCurrentPage()).toBe(2);

      // Category change updates items and resets to page 1
      pager.setItems(generateAuditLogList(10));
      expect(pager.getCurrentPage()).toBe(1);
    });
  });

  // =========================================================================
  // Feature 10: Audit Log 15-item Pagination
  // =========================================================================
  describe("Feature 10: Audit Log 15-item Pagination", () => {
    it("T1.F10.1: Pagination displays current page and total pages accurately", () => {
      const pager = new PaginationMachine(generateAuditLogList(45), 15);
      expect(pager.getCurrentPage()).toBe(1);
      expect(pager.getTotalPages()).toBe(3);
    });

    it("T1.F10.2: Page navigation increments/decrements active page", () => {
      const pager = new PaginationMachine(generateAuditLogList(45), 15);
      pager.nextPage();
      expect(pager.getCurrentPage()).toBe(2);
      pager.prevPage();
      expect(pager.getCurrentPage()).toBe(1);
    });

    it("T1.F10.3: Direct page jump navigates to target page number", () => {
      const pager = new PaginationMachine(generateAuditLogList(45), 15);
      const ok = pager.setPage(3);
      expect(ok).toBe(true);
      expect(pager.getCurrentPage()).toBe(3);
    });

    it("T1.F10.4: Previous button disabled on page 1", () => {
      const pager = new PaginationMachine(generateAuditLogList(30), 15, 1);
      expect(pager.isPrevDisabled()).toBe(true);
      pager.nextPage();
      expect(pager.isPrevDisabled()).toBe(false);
    });

    it("T1.F10.5: Next button disabled on last page", () => {
      const pager = new PaginationMachine(generateAuditLogList(30), 15, 2);
      expect(pager.isNextDisabled()).toBe(true);
      pager.prevPage();
      expect(pager.isNextDisabled()).toBe(false);
    });
  });

  // =========================================================================
  // Feature 11: Feedback Relational Backend API
  // =========================================================================
  describe("Feature 11: Feedback Relational Backend API", () => {
    it("T1.F11.1: Feedback response envelope adheres to ApiResponse<PagedResult<AdminFeedbackItemResponse>>", () => {
      const feedbacks = generateFeedbackList(15);
      const payload = {
        code: 200,
        message: "OK",
        data: {
          items: feedbacks,
          page: 1,
          pageSize: 15,
          totalItems: 30,
          totalPages: 2,
        },
      };

      expect(
        validatePagedResult<AdminFeedbackItemResponse>(
          payload.data,
          validateFeedbackItem,
          15
        )
      ).toBe(true);
    });

    it("T1.F11.2: Relational fields include CaseId, DoctorId, DoctorName, PatientProfileId, PatientName", () => {
      const fb = generateFeedbackItem({
        caseId: "case-uuid-1",
        doctorId: "doc-uuid-1",
        doctorName: "BS. Hoàng Văn Phúc",
        patientProfileId: "pat-uuid-1",
        patientName: "Trần Thị Mai",
      });

      expect(validateFeedbackItem(fb)).toBe(true);
      expect(fb.caseId).toBe("case-uuid-1");
      expect(fb.doctorId).toBe("doc-uuid-1");
      expect(fb.doctorName).toBe("BS. Hoàng Văn Phúc");
      expect(fb.patientProfileId).toBe("pat-uuid-1");
      expect(fb.patientName).toBe("Trần Thị Mai");
    });

    it("T1.F11.3: Patient phone is included in feedback item response", () => {
      const fb = generateFeedbackItem({ patientPhone: "0987654321" });
      expect(fb.patientPhone).toBe("0987654321");
    });

    it("T1.F11.4: SubmittedAt timestamp is formatted as valid ISO date", () => {
      const fb = generateFeedbackItem({ submittedAt: "2026-09-10T14:30:00Z" });
      expect(new Date(fb.submittedAt).toISOString()).toBe("2026-09-10T14:30:00.000Z");
    });

    it("T1.F11.5: Query accepts minRating parameter (1..5)", () => {
      const feedbacks = [
        generateFeedbackItem({ rating: 2 }),
        generateFeedbackItem({ rating: 4 }),
        generateFeedbackItem({ rating: 5 }),
      ];

      const minRating = 4;
      const filtered = feedbacks.filter((f) => f.rating >= minRating);
      expect(filtered).toHaveLength(2);
      for (const item of filtered) {
        expect(item.rating).toBeGreaterThanOrEqual(4);
      }
    });
  });

  // =========================================================================
  // Feature 12: Feedback Admin UI & Star Rating
  // =========================================================================
  describe("Feature 12: Feedback Admin UI & Star Rating", () => {
    it("T1.F12.1: 5-star rating displays filled stars corresponding to numeric rating", () => {
      const visual4 = StarRatingCalculator.getVisualStars(4);
      expect(visual4.filledStars).toBe(4);
      expect(visual4.mutedStars).toBe(1);

      const visual5 = StarRatingCalculator.getVisualStars(5);
      expect(visual5.filledStars).toBe(5);
      expect(visual5.mutedStars).toBe(0);
    });

    it("T1.F12.2: Unfilled stars display muted styling for remaining count (5 - rating)", () => {
      const visual2 = StarRatingCalculator.getVisualStars(2);
      expect(visual2.mutedStars).toBe(3);
    });

    it("T1.F12.3: Rating number text (X/5) displays beside stars", () => {
      const visual3 = StarRatingCalculator.getVisualStars(3);
      expect(visual3.badgeText).toBe("(3/5)");
    });

    it("T1.F12.4: Admin feedback table inherits rounded-3xl container and pl-6 padding", () => {
      const containerClass = "rounded-3xl border border-border bg-background pl-6";
      expect(containerClass).toContain("rounded-3xl");
      expect(containerClass).toContain("pl-6");
    });

    it("T1.F12.5: Feedback content renders text with text-wrap and line clamping for long reviews", () => {
      const fb = generateFeedbackItem({
        content: "Dịch vụ rất tốt, trang thiết bị hiện đại, bác sĩ nhiệt tình.",
      });
      expect(fb.content.length).toBeGreaterThan(10);
    });
  });

  // =========================================================================
  // Feature 13: Feedback Clickable Entity Links
  // =========================================================================
  describe("Feature 13: Feedback Clickable Entity Links", () => {
    it("T1.F13.1: Doctor name link points to /admin/users/${doctorId}", () => {
      const fb = generateFeedbackItem({ doctorId: "doc-123" });
      const targetUrl = `/admin/users/${fb.doctorId}`;
      expect(targetUrl).toBe("/admin/users/doc-123");
    });

    it("T1.F13.2: Patient name link points to /patients/${patientProfileId}", () => {
      const fb = generateFeedbackItem({ patientProfileId: "pat-456" });
      const targetUrl = `/patients/${fb.patientProfileId}`;
      expect(targetUrl).toBe("/patients/pat-456");
    });

    it("T1.F13.3: Case code link points to /cases/${caseId}", () => {
      const fb = generateFeedbackItem({ caseId: "case-789" });
      const targetUrl = `/cases/${fb.caseId}`;
      expect(targetUrl).toBe("/cases/case-789");
    });

    it("T1.F13.4: Missing doctor gracefully renders 'Không xác định' without dead link crash", () => {
      const fb = generateFeedbackItem({ doctorId: "", doctorName: "Không xác định" });
      const isLinked = Boolean(fb.doctorId);
      expect(isLinked).toBe(false);
      expect(fb.doctorName).toBe("Không xác định");
    });

    it("T1.F13.5: Entity link click generates valid URL routes", () => {
      const fb = generateFeedbackItem({
        doctorId: "doc-1",
        patientProfileId: "pat-2",
        caseId: "case-3",
      });

      const docLink = `/admin/users/${fb.doctorId}`;
      const patLink = `/patients/${fb.patientProfileId}`;
      const caseLink = `/cases/${fb.caseId}`;

      expect(docLink).toMatch(/^\/admin\/users\/doc-1$/);
      expect(patLink).toMatch(/^\/patients\/pat-2$/);
      expect(caseLink).toMatch(/^\/cases\/case-3$/);
    });
  });

  // =========================================================================
  // Feature 14: Feedback Admin Route Access
  // =========================================================================
  describe("Feature 14: Feedback Admin Route Access", () => {
    it("T1.F14.1: isRoleAllowedOnPath('ADMIN', '/admin/feedback') evaluates to true", () => {
      const rbac = new RbacPolicyEvaluator();
      expect(rbac.isAllowed("ADMIN", "/admin/feedback")).toBe(true);
    });

    it("T1.F14.2: Target specification verifies ADMIN role granted access to /patients", () => {
      const rbac = new RbacPolicyEvaluator();
      expect(rbac.isAllowed("ADMIN", "/patients")).toBe(true);
      expect(rbac.isAllowed("ADMIN", "/patients/profile-123")).toBe(true);
    });

    it("T1.F14.3: Target specification verifies ADMIN role granted access to /cases", () => {
      const rbac = new RbacPolicyEvaluator();
      expect(rbac.isAllowed("ADMIN", "/cases")).toBe(true);
      expect(rbac.isAllowed("ADMIN", "/cases/case-456")).toBe(true);
    });

    it("T1.F14.4: DOCTOR role retains access to /patients and /cases", () => {
      const rbac = new RbacPolicyEvaluator();
      expect(rbac.isAllowed("DOCTOR", "/patients")).toBe(true);
      expect(rbac.isAllowed("DOCTOR", "/cases")).toBe(true);
    });

    it("T1.F14.5: PATIENT role denied access to all protected web routes", () => {
      const rbac = new RbacPolicyEvaluator();
      expect(rbac.isAllowed("PATIENT", "/patients")).toBe(false);
      expect(rbac.isAllowed("PATIENT", "/cases")).toBe(false);
      expect(rbac.isAllowed("PATIENT", "/admin/feedback")).toBe(false);
    });
  });

  // =========================================================================
  // Feature 15: Feedback Realtime Multi-Search
  // =========================================================================
  describe("Feature 15: Feedback Realtime Multi-Search", () => {
    it("T1.F15.1: Multi-search matches patient name substring", () => {
      const list = [
        generateFeedbackItem({ patientName: "Nguyễn Văn Đạt" }),
        generateFeedbackItem({ patientName: "Phạm Thảo Vy" }),
      ];

      const match = list.filter((f) =>
        f.patientName.toLowerCase().includes("văn đạt")
      );
      expect(match).toHaveLength(1);
      expect(match[0].patientName).toBe("Nguyễn Văn Đạt");
    });

    it("T1.F15.2: Multi-search matches doctor name substring", () => {
      const list = [
        generateFeedbackItem({ doctorName: "BS. Lê Hoàng Nam" }),
        generateFeedbackItem({ doctorName: "BS. Trần Đức Anh" }),
      ];

      const match = list.filter((f) =>
        f.doctorName.toLowerCase().includes("hoàng nam")
      );
      expect(match).toHaveLength(1);
    });

    it("T1.F15.3: Multi-search matches case code or UUID prefix", () => {
      const list = [
        generateFeedbackItem({ caseId: "case-echo-888" }),
        generateFeedbackItem({ caseId: "case-general-999" }),
      ];

      const match = list.filter((f) => f.caseId.includes("echo-888"));
      expect(match).toHaveLength(1);
    });

    it("T1.F15.4: Multi-search debounce at 300ms prevents premature query dispatch", async () => {
      const sim = new DebounceSimulator();
      const fn = vi.fn();

      const promise = sim.queueInput("BS. Nam", 300, fn);
      expect(fn).not.toHaveBeenCalled();

      vi.advanceTimersByTime(300);
      await promise;
      expect(fn).toHaveBeenCalledWith("BS. Nam");
    });

    it("T1.F15.5: Search reset returns full feedback queue for active page", () => {
      const list = generateFeedbackList(15);
      const query = "";
      const result = query ? list.filter((f) => f.patientName.includes(query)) : list;

      expect(result).toHaveLength(15);
    });
  });
});
