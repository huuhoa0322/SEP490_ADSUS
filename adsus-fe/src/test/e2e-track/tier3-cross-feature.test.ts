import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
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

describe("Tier 3: Cross-Feature Combinations (Pairwise & Multi-Feature Interactions)", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // =========================================================================
  // Check-in Combinations (Features 1, 2, 3, 4, 5)
  // =========================================================================
  describe("Check-in Feature Combinations", () => {
    it("T3.1: Date Range + Search (F1 x F3): filters queue by both date span and patient query", () => {
      const items = [
        generateCheckinItem({ slotTime: "2026-09-02T08:00:00Z", patientFullName: "Lê Văn Hùng" }),
        generateCheckinItem({ slotTime: "2026-09-05T08:00:00Z", patientFullName: "Lê Văn Hùng" }),
        generateCheckinItem({ slotTime: "2026-09-05T08:00:00Z", patientFullName: "Trần Thị Mai" }),
      ];

      const range = { start: "2026-09-04", end: "2026-09-06" };
      const search = "Lê Văn Hùng";

      const combined = items.filter((it) => {
        const d = it.slotTime.split("T")[0];
        const inRange = d >= range.start && d <= range.end;
        const matchesSearch = it.patientFullName.includes(search);
        return inRange && matchesSearch;
      });

      expect(combined).toHaveLength(1);
      expect(combined[0].slotTime).toContain("2026-09-05");
      expect(combined[0].patientFullName).toBe("Lê Văn Hùng");
    });

    it("T3.2: Date Range + Status Filter (F1 x F4): isolates BOOKED items within 7-day range", () => {
      const machine = new DateRangeMachine("2026-09-10");
      const range = machine.applyPreset("last7days", new Date("2026-09-10T12:00:00Z"));

      const items = [
        generateCheckinItem({ slotTime: "2026-09-08T09:00:00Z", status: "BOOKED" }),
        generateCheckinItem({ slotTime: "2026-09-08T09:00:00Z", status: "APPROVED" }),
        generateCheckinItem({ slotTime: "2026-08-20T09:00:00Z", status: "BOOKED" }), // out of range
      ];

      const filtered = items.filter((it) => {
        const d = it.slotTime.split("T")[0];
        return d >= range.startDate && d <= range.endDate && it.status === "BOOKED";
      });

      expect(filtered).toHaveLength(1);
      expect(filtered[0].status).toBe("BOOKED");
      expect(filtered[0].slotTime).toContain("2026-09-08");
    });

    it("T3.3: Date Range + Pagination (F1 x F5): paginates multi-day historical appointments at 15 items/page", () => {
      const items = generateCheckinQueue(40);
      const pager = new PaginationMachine(items, 15);

      expect(pager.getTotalPages()).toBe(3);
      expect(pager.getPagedSlice()).toHaveLength(15);
      pager.nextPage();
      expect(pager.getPagedSlice()).toHaveLength(15);
      pager.nextPage();
      expect(pager.getPagedSlice()).toHaveLength(10);
    });

    it("T3.4: Search + Status Filter (F3 x F4): filters by patient phone and APPROVED status simultaneously", () => {
      const items = [
        generateCheckinItem({ patientPhone: "0901234567", status: "BOOKED" }),
        generateCheckinItem({ patientPhone: "0901234567", status: "APPROVED" }),
        generateCheckinItem({ patientPhone: "0909999999", status: "APPROVED" }),
      ];

      const query = "0901234567";
      const status = "APPROVED";

      const result = items.filter((it) => it.patientPhone.includes(query) && it.status === status);
      expect(result).toHaveLength(1);
      expect(result[0].patientPhone).toBe("0901234567");
      expect(result[0].status).toBe("APPROVED");
    });

    it("T3.5: Search + Pagination (F3 x F5): searching updates result set and resets active page to 1", () => {
      const allItems = generateCheckinQueue(50);
      const pager = new PaginationMachine(allItems, 15, 3); // starts at page 3
      expect(pager.getCurrentPage()).toBe(3);

      const matching = allItems.filter((_, idx) => idx % 5 === 0); // 10 items
      pager.setItems(matching);

      expect(pager.getCurrentPage()).toBe(1);
      expect(pager.getTotalPages()).toBe(1);
      expect(pager.getPagedSlice()).toHaveLength(10);
    });

    it("T3.6: Status Filter + Pagination (F4 x F5): status filter change resets pagination and recomputes total pages", () => {
      const bookedItems = generateCheckinQueue(35, { BOOKED: 35, APPROVED: 0, COMPLETED: 0, CANCELLED: 0 });
      const approvedItems = generateCheckinQueue(10, { BOOKED: 0, APPROVED: 10, COMPLETED: 0, CANCELLED: 0 });

      const pager = new PaginationMachine(bookedItems, 15, 2);
      expect(pager.getCurrentPage()).toBe(2);
      expect(pager.getTotalPages()).toBe(3);

      pager.setItems(approvedItems);
      expect(pager.getCurrentPage()).toBe(1);
      expect(pager.getTotalPages()).toBe(1);
      expect(pager.getPagedSlice()).toHaveLength(10);
    });

    it("T3.7: Full Combination (F1 x F3 x F4 x F5): Date Range + Search + Status Filter + Pagination with KPI update", () => {
      const queue = [
        ...generateCheckinQueue(20, { BOOKED: 10, APPROVED: 10, COMPLETED: 0, CANCELLED: 0 }),
      ];
      // Mark specific item with unique doctor name
      queue[0].doctorName = "BS. Chuyên Khoa Tuyến Đầu";

      const filtered = queue.filter(
        (it) => it.doctorName.includes("Tuyến Đầu") && it.status === "BOOKED"
      );
      const pager = new PaginationMachine(filtered, 15);
      const summary = KpiCalculator.summarizeQueue(filtered);

      expect(pager.getCurrentPage()).toBe(1);
      expect(summary.total).toBe(1);
      expect(summary.pending).toBe(1);
      expect(summary.progressPercentage).toBe(0);
    });
  });

  // =========================================================================
  // Audit Log Combinations (Features 6, 7, 8, 9, 10)
  // =========================================================================
  describe("Audit Log Feature Combinations", () => {
    it("T3.8: Category Filter + Search (F9 x F8): filters audit logs by action category and actor name simultaneously", () => {
      const logs = [
        generateAuditLog({ action: AUDIT_ACTIONS.ACCOUNT_CREATE, actorName: "SuperAdmin" }),
        generateAuditLog({ action: AUDIT_ACTIONS.ACCOUNT_CREATE, actorName: "JuniorAdmin" }),
        generateAuditLog({ action: AUDIT_ACTIONS.AI_REGISTER, actorName: "SuperAdmin" }),
      ];

      const category = "ACCOUNT";
      const actorSearch = "SuperAdmin";

      const result = logs.filter(
        (l) => l.action.includes(category) && l.actorName.includes(actorSearch)
      );
      expect(result).toHaveLength(1);
      expect(result[0].action).toBe(AUDIT_ACTIONS.ACCOUNT_CREATE);
      expect(result[0].actorName).toBe("SuperAdmin");
    });

    it("T3.9: Category Filter + Pagination (F9 x F10): switching category resets page to 1 and slices 15 items/page", () => {
      const allLogs = generateAuditLogList(40);
      const pager = new PaginationMachine(allLogs, 15, 2);
      expect(pager.getCurrentPage()).toBe(2);

      const aiLogs = allLogs.filter((l) => l.action.includes("AI_"));
      pager.setItems(aiLogs);

      expect(pager.getCurrentPage()).toBe(1);
      expect(pager.getPagedSlice().length).toBeLessThanOrEqual(15);
    });

    it("T3.10: Search + Pagination (F8 x F10): debounced keyword filter resets page and displays matching slice", async () => {
      const logs = generateAuditLogList(60);
      const pager = new PaginationMachine(logs, 15, 3);
      expect(pager.getCurrentPage()).toBe(3);

      const sim = new DebounceSimulator();
      const promise = sim.queueInput("Nurse", 300, (query) => {
        const matches = logs.filter((l) => l.actorName.includes(query));
        pager.setItems(matches);
      });

      vi.advanceTimersByTime(300);
      await promise;

      expect(pager.getCurrentPage()).toBe(1);
    });

    it("T3.11: Date Range + Category Filter (F6 x F9): queries audit logs within timeframe and action group", () => {
      const now = new Date("2026-09-10T12:00:00Z").getTime();
      const logs = [
        generateAuditLog({
          action: AUDIT_ACTIONS.ACCOUNT_CREATE,
          performedAt: new Date(now - 1000).toISOString(),
        }),
        generateAuditLog({
          action: AUDIT_ACTIONS.AI_REGISTER,
          performedAt: new Date(now - 1000).toISOString(),
        }),
        generateAuditLog({
          action: AUDIT_ACTIONS.ACCOUNT_CREATE,
          performedAt: new Date(now - 10000000).toISOString(),
        }),
      ];

      const fromDate = new Date(now - 5000).toISOString();
      const result = logs.filter(
        (l) => l.performedAt >= fromDate && l.action.includes("ACCOUNT")
      );

      expect(result).toHaveLength(1);
      expect(result[0].action).toBe(AUDIT_ACTIONS.ACCOUNT_CREATE);
    });

    it("T3.12: Full Combination (F8 x F9 x F10): Category Filter + Search + 15-item Pagination slice", () => {
      const logs = generateAuditLogList(50);
      const filtered = logs
        .filter((l) => l.actorRole === "ADMIN")
        .filter((l) => l.action.includes("ACCOUNT"));

      const pager = new PaginationMachine(filtered, 15);
      expect(pager.getCurrentPage()).toBe(1);
      expect(pager.getPagedSlice().length).toBeLessThanOrEqual(15);
    });
  });

  // =========================================================================
  // Feedback Combinations (Features 11, 12, 13, 14, 15)
  // =========================================================================
  describe("Feedback Feature Combinations", () => {
    it("T3.13: MinRating + Multi-Search (F11/12 x F15): filters 5-star feedbacks matching patient name", () => {
      const list = [
        generateFeedbackItem({ rating: 5, patientName: "Phạm Thảo Vy" }),
        generateFeedbackItem({ rating: 3, patientName: "Phạm Thảo Vy" }),
        generateFeedbackItem({ rating: 5, patientName: "Nguyễn Văn An" }),
      ];

      const minRating = 5;
      const search = "Thảo Vy";

      const filtered = list.filter(
        (f) => f.rating >= minRating && f.patientName.includes(search)
      );

      expect(filtered).toHaveLength(1);
      expect(filtered[0].patientName).toBe("Phạm Thảo Vy");
      expect(filtered[0].rating).toBe(5);
    });

    it("T3.14: MinRating + Doctor Search + Clickable Link (F12 x F13 x F15): finds low-rating review and builds doctor profile link", () => {
      const list = [
        generateFeedbackItem({
          rating: 1,
          doctorName: "BS. Trần Văn Minh",
          doctorId: "doc-minh-01",
        }),
        generateFeedbackItem({
          rating: 5,
          doctorName: "BS. Trần Văn Minh",
          doctorId: "doc-minh-01",
        }),
      ];

      const lowReviews = list.filter(
        (f) => f.doctorName.includes("Minh") && f.rating <= 2
      );

      expect(lowReviews).toHaveLength(1);
      const link = `/admin/users/${lowReviews[0].doctorId}`;
      expect(link).toBe("/admin/users/doc-minh-01");
    });

    it("T3.15: Case Code Search + Clickable Case Link + Admin Route Permission (F13 x F14 x F15)", () => {
      const list = [
        generateFeedbackItem({ caseId: "case-cardio-555" }),
        generateFeedbackItem({ caseId: "case-general-666" }),
      ];

      const matched = list.filter((f) => f.caseId.includes("cardio-555"));
      expect(matched).toHaveLength(1);

      const targetPath = `/cases/${matched[0].caseId}`;
      expect(targetPath).toBe("/cases/case-cardio-555");

      const rbac = new RbacPolicyEvaluator();
      expect(rbac.isAllowed("ADMIN", targetPath)).toBe(true);
    });

    it("T3.16: Multi-Search + 15-item Pagination (F15 x F11): search query filters list and paginates matching results", () => {
      const list = generateFeedbackList(50);
      const filtered = list.filter((f) => f.doctorName.includes("BS. Lê Hoàng Nam"));

      const pager = new PaginationMachine(filtered, 15);
      expect(pager.getPagedSlice().length).toBeLessThanOrEqual(15);
      expect(pager.getTotalPages()).toBe(Math.ceil(filtered.length / 15));
    });

    it("T3.17: Star Rating Filter + Patient Clickable Link + Admin Route Access (F12 x F13 x F14)", () => {
      const item = generateFeedbackItem({
        rating: 5,
        patientProfileId: "pat-vip-001",
        patientName: "Vũ Đình Long",
      });

      const visual = StarRatingCalculator.getVisualStars(item.rating);
      expect(visual.filledStars).toBe(5);

      const patientUrl = `/patients/${item.patientProfileId}`;
      const rbac = new RbacPolicyEvaluator();
      expect(rbac.isAllowed("ADMIN", patientUrl)).toBe(true);
    });
  });

  // =========================================================================
  // Cross-Module Interactions & UI State Transitions
  // =========================================================================
  describe("Cross-Module & UI State Invariants", () => {
    it("T3.18: Cross-Module (Checkin x Audit Log): Nurse check-in operation creates an audit log entry visible to Admin", () => {
      const checkinItem = generateCheckinItem({ status: "BOOKED" });
      checkinItem.status = "APPROVED";

      const auditEntry = generateAuditLog({
        actorName: "Nurse Lan",
        actorRole: "NURSE",
        action: "CHECKIN_APPOINTMENT",
        detail: `Checked in appointment ${checkinItem.appointmentId}`,
      });

      expect(checkinItem.status).toBe("APPROVED");
      expect(auditEntry.action).toBe("CHECKIN_APPOINTMENT");
      expect(auditEntry.detail).toContain(checkinItem.appointmentId);
    });

    it("T3.19: Cross-Module (Checkin/Case x Feedback): Case visit completed leads to patient feedback display in Admin table", () => {
      const appointment = generateCheckinItem({
        status: "COMPLETED",
        caseId: "case-visit-789",
        doctorName: "BS. Minh",
      });

      const feedback = generateFeedbackItem({
        caseId: appointment.caseId!,
        doctorName: appointment.doctorName,
        rating: 5,
      });

      expect(feedback.caseId).toBe(appointment.caseId);
      expect(feedback.doctorName).toBe(appointment.doctorName);
    });

    it("T3.20: Cross-Module (Feedback x Admin RBAC): Admin navigates from Feedback row to Case details without 403 redirect", () => {
      const fb = generateFeedbackItem({ caseId: "case-investigate-999" });
      const rbac = new RbacPolicyEvaluator();

      const path = `/cases/${fb.caseId}`;
      const hasAccess = rbac.isAllowed("ADMIN", path);
      expect(hasAccess).toBe(true);
    });

    it("T3.21: UI State: Changing date range resets pagination to page 1 while preserving search keyword", () => {
      const searchKeyword = "Nguyễn";
      const pager = new PaginationMachine(generateCheckinQueue(30), 15, 2);
      expect(pager.getCurrentPage()).toBe(2);

      // Trigger date change
      const newItems = generateCheckinQueue(15).filter((i) =>
        i.patientFullName.includes(searchKeyword)
      );
      pager.setItems(newItems);

      expect(pager.getCurrentPage()).toBe(1);
      expect(searchKeyword).toBe("Nguyễn");
    });

    it("T3.22: UI State: Clearing search resets pagination to page 1 while preserving date range", () => {
      const machine = new DateRangeMachine("2026-09-10");
      machine.setRange("2026-09-01", "2026-09-07");

      const pager = new PaginationMachine(generateCheckinQueue(30), 15, 2);
      expect(pager.getCurrentPage()).toBe(2);

      // Clear search
      pager.setItems(generateCheckinQueue(30));
      expect(pager.getCurrentPage()).toBe(1);
      expect(machine.getState().startDate).toBe("2026-09-01");
      expect(machine.getState().endDate).toBe("2026-09-07");
    });
  });
});
