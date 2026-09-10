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

describe("Tier 4: Real-World Application Scenarios", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // =========================================================================
  // Scenario 1: Nurse Daily Check-in & Historical Shift Reconciliation
  // =========================================================================
  it("Scenario 1: Nurse Daily Check-in & Historical Shift Reconciliation", async () => {
    // 1. Nurse arrives for morning shift and accesses /checkin
    const fixedToday = "2026-09-10";
    const dateRange = new DateRangeMachine(fixedToday);
    expect(dateRange.getState().startDate).toBe(fixedToday);
    expect(dateRange.getState().endDate).toBe(fixedToday);

    // 2. Load today's initial queue (20 appointments: 15 Booked, 5 Approved)
    const initialQueue = generateCheckinQueue(20, {
      BOOKED: 15,
      APPROVED: 5,
      COMPLETED: 0,
      CANCELLED: 0,
    });
    const kpiMorning = KpiCalculator.summarizeQueue(initialQueue);
    expect(kpiMorning.total).toBe(20);
    expect(kpiMorning.checkedIn).toBe(5);
    expect(kpiMorning.pending).toBe(15);
    expect(kpiMorning.progressPercentage).toBe(25);

    // 3. Nurse filters by "BOOKED" status to see upcoming arrivals
    const pendingQueue = initialQueue.filter((it) => it.status === "BOOKED");
    const pendingPager = new PaginationMachine(pendingQueue, 15);
    expect(pendingPager.getPagedSlice()).toHaveLength(15);

    // 4. Patient arrives at reception: Nurse searches by phone number with debounce
    const targetPatientPhone = "0901234567";
    pendingQueue[0].patientPhone = targetPatientPhone;
    pendingQueue[0].patientFullName = "Nguyễn Thu Thảo";

    const sim = new DebounceSimulator();
    const searchPromise = sim.queueInput(targetPatientPhone, 300);
    vi.advanceTimersByTime(300);
    const searchResult = await searchPromise;
    expect(searchResult).toBe(targetPatientPhone);

    const matchedPatient = pendingQueue.find((it) => it.patientPhone === searchResult);
    expect(matchedPatient).toBeDefined();

    // 5. Nurse executes check-in action
    matchedPatient!.status = "APPROVED";
    expect(matchedPatient!.status).toBe("APPROVED");

    // 6. Walk-in emergency patient arrives without prior case (caseId is null / empty)
    const walkInPatient = generateCheckinItem({
      caseId: null,
      patientFullName: "Võ Văn Cường (Cấp cứu)",
      status: "BOOKED",
    });
    initialQueue.push(walkInPatient);

    // Nurse executes direct check-in with empty GUID endpoint routing
    const targetCheckinEndpoint = walkInPatient.caseId
      ? `/api/v1/cases/${walkInPatient.caseId}/appointment/checkin`
      : `/api/v1/cases/appointment/${walkInPatient.appointmentId}/checkin`;
    expect(targetCheckinEndpoint).toContain(`/cases/appointment/${walkInPatient.appointmentId}/checkin`);
    walkInPatient.status = "APPROVED";

    // 7. End of shift historical reconciliation: Nurse selects preset "7 ngày qua"
    const presetRange = dateRange.applyPreset("last7days", new Date("2026-09-10T12:00:00Z"));
    expect(presetRange.startDate).toBe("2026-09-04");
    expect(presetRange.endDate).toBe("2026-09-10");

    // Load multi-day queue (45 historical items)
    const historicalQueue = generateCheckinQueue(45);
    const historyPager = new PaginationMachine(historicalQueue, 15);
    expect(historyPager.getTotalPages()).toBe(3);

    // Nurse reviews pages 1, 2, and 3
    expect(historyPager.getPagedSlice()).toHaveLength(15);
    historyPager.nextPage();
    expect(historyPager.getCurrentPage()).toBe(2);
    expect(historyPager.getPagedSlice()).toHaveLength(15);
    historyPager.nextPage();
    expect(historyPager.getCurrentPage()).toBe(3);
    expect(historyPager.getPagedSlice()).toHaveLength(15);
    expect(historyPager.isNextDisabled()).toBe(true);

    // Verify KPI calculations across historical data
    const historyKpi = KpiCalculator.summarizeQueue(historicalQueue);
    expect(historyKpi.total).toBe(45);
    expect(Number.isFinite(historyKpi.progressPercentage)).toBe(true);
  });

  // =========================================================================
  // Scenario 2: Admin Security & Forensic Audit Trail Investigation
  // =========================================================================
  it("Scenario 2: Admin Security & Forensic Audit Trail Investigation", async () => {
    // 1. Security Admin accesses /admin/audit-logs
    const rbac = new RbacPolicyEvaluator();
    expect(rbac.isAllowed("ADMIN", "/admin/audit-logs")).toBe(true);
    expect(rbac.isAllowed("NURSE", "/admin/audit-logs")).toBe(false);

    // 2. Generate large audit dataset (120 events)
    const auditLogs = generateAuditLogList(120);
    expect(auditLogs).toHaveLength(120);

    // 3. Security alert: Admin filters by category "Tài khoản quản trị" (Account management)
    const accountActions = new Set<string>([
      AUDIT_ACTIONS.ACCOUNT_CREATE,
      AUDIT_ACTIONS.ACCOUNT_UPDATE,
      AUDIT_ACTIONS.ACCOUNT_DEACTIVATE,
      AUDIT_ACTIONS.ACCOUNT_REACTIVATE,
      AUDIT_ACTIONS.ADMIN_RESET_PASSWORD,
      AUDIT_ACTIONS.SELF_RESET_PASSWORD,
    ]);

    const accountLogs = auditLogs.filter((l) => accountActions.has(l.action));
    expect(accountLogs.length).toBeGreaterThan(0);

    // 4. Admin performs realtime debounced search for actor name "Admin System"
    const sim = new DebounceSimulator();
    const searchPromise = sim.queueInput("Admin System", 300);
    vi.advanceTimersByTime(300);
    const query = await searchPromise;

    const filteredLogs = accountLogs.filter((l) => l.actorName.includes(query));
    expect(filteredLogs.length).toBeGreaterThan(0);

    // 5. Admin paginates through results (15 items/page)
    const pager = new PaginationMachine(filteredLogs, 15);
    expect(pager.getCurrentPage()).toBe(1);
    expect(pager.isPrevDisabled()).toBe(true);
    expect(pager.getPagedSlice().length).toBeLessThanOrEqual(15);

    if (pager.getTotalPages() > 1) {
      pager.nextPage();
      expect(pager.getCurrentPage()).toBe(2);
      expect(pager.isPrevDisabled()).toBe(false);
    }

    // 6. Admin inspects audit record details with long stack trace / JSON
    const sampleLog = filteredLogs[0];
    sampleLog.detail = JSON.stringify({
      ipAddress: "192.168.1.100",
      userAgent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
      previousRole: "DOCTOR",
      newRole: "ADMIN",
    });

    const truncated = sampleLog.detail.length > 50 ? `${sampleLog.detail.slice(0, 47)}...` : sampleLog.detail;
    expect(truncated.endsWith("...")).toBe(true);
  });

  // =========================================================================
  // Scenario 3: Clinic Service Quality & Negative Feedback Resolution
  // =========================================================================
  it("Scenario 3: Clinic Service Quality & Negative Feedback Resolution", async () => {
    // 1. Clinic Quality Manager opens /admin/feedback
    const rbac = new RbacPolicyEvaluator();
    expect(rbac.isAllowed("ADMIN", "/admin/feedback")).toBe(true);

    // 2. Generate feedback data with varied ratings
    const feedbacks = generateFeedbackList(40);

    // Plant a specific critical complaint
    const criticalFeedback = generateFeedbackItem({
      id: "fb-critical-001",
      rating: 1,
      doctorName: "BS. Hoàng Văn Phúc",
      doctorId: "doc-phuc-777",
      patientProfileId: "pat-lan-888",
      patientName: "Bùi Thị Lan",
      caseId: "case-urgent-999",
      content: "Bác sĩ giải thích chưa rõ ràng, bệnh nhân chờ quá 2 tiếng.",
    });
    feedbacks.unshift(criticalFeedback);

    // 3. Admin filters for low ratings (minRating = 1, but specifically isolates ratings <= 2)
    const lowRatingFeedbacks = feedbacks.filter((f) => f.rating <= 2);
    expect(lowRatingFeedbacks.length).toBeGreaterThan(0);

    // 4. Admin searches for doctor name "Hoàng Văn Phúc"
    const matched = lowRatingFeedbacks.filter((f) => f.doctorName.includes("Hoàng Văn Phúc"));
    expect(matched).toHaveLength(1);
    const complaint = matched[0];

    // Verify 5-star visual representation (1 filled star, 4 muted stars)
    const visual = StarRatingCalculator.getVisualStars(complaint.rating);
    expect(visual.filledStars).toBe(1);
    expect(visual.mutedStars).toBe(4);
    expect(visual.badgeText).toBe("(1/5)");

    // 5. Admin performs cross-entity investigation:
    // a) Review Doctor profile:
    const doctorLink = `/admin/users/${complaint.doctorId}`;
    expect(doctorLink).toBe("/admin/users/doc-phuc-777");
    expect(rbac.isAllowed("ADMIN", doctorLink)).toBe(true);

    // b) Review Case clinical records:
    const caseLink = `/cases/${complaint.caseId}`;
    expect(caseLink).toBe("/cases/case-urgent-999");
    expect(rbac.isAllowed("ADMIN", caseLink)).toBe(true);

    // c) Review Patient contact details:
    const patientLink = `/patients/${complaint.patientProfileId}`;
    expect(patientLink).toBe("/patients/pat-lan-888");
    expect(rbac.isAllowed("ADMIN", patientLink)).toBe(true);
  });

  // =========================================================================
  // Scenario 4: Emergency Patient Search & Realtime Queue Handling
  // =========================================================================
  it("Scenario 4: Emergency Patient Search & Realtime Queue Handling", async () => {
    // 1. Receptionist manages emergency check-in queue
    const queue = generateCheckinQueue(30, { BOOKED: 30, APPROVED: 0, COMPLETED: 0, CANCELLED: 0 });
    queue[5].patientFullName = "Nguyễn Khắc Việt";
    queue[5].patientPhone = "0918765432";

    // 2. High-speed typing in search bar: simulates 10 rapid keystrokes within 200ms
    const sim = new DebounceSimulator();
    const keystrokes = ["N", "Ng", "Ngu", "Nguy", "Nguye", "Nguyen", "Nguyen K", "Nguyen Kh", "Nguyen Kha", "Nguyễn Khắc Việt"];

    for (const key of keystrokes) {
      sim.queueInput(key, 300);
      vi.advanceTimersByTime(20);
    }

    // Debounce suppresses intermediate executions
    expect(sim.getExecutedCalls()).toBe(0);

    // Allow debounce timer to fire
    vi.advanceTimersByTime(300);
    expect(sim.getExecutedCalls()).toBe(1);

    // 3. User typos a name that does not exist -> queue returns 0 results
    const nonExistentQuery = "Người Không Tồn Tại";
    const noResults = queue.filter((i) => i.patientFullName.includes(nonExistentQuery));
    expect(noResults).toHaveLength(0);

    // 4. Zero-division KPI safety verification on empty results
    const emptyKpi = KpiCalculator.summarizeQueue(noResults);
    expect(emptyKpi.total).toBe(0);
    expect(emptyKpi.checkedIn).toBe(0);
    expect(emptyKpi.progressPercentage).toBe(0);
    expect(Number.isNaN(emptyKpi.progressPercentage)).toBe(false);

    // 5. User corrects search query to valid phone number
    const validMatches = queue.filter((i) => i.patientPhone === "0918765432");
    expect(validMatches).toHaveLength(1);
    expect(validMatches[0].patientFullName).toBe("Nguyễn Khắc Việt");

    // Check in patient
    validMatches[0].status = "APPROVED";
    expect(validMatches[0].status).toBe("APPROVED");
  });

  // =========================================================================
  // Scenario 5: End-to-End Clinic Management Day Close-out
  // =========================================================================
  it("Scenario 5: End-to-End Clinic Management Day Close-out", async () => {
    const today = "2026-09-10";

    // 1. 17:00 Close-out: Nurse reviews check-in queue for today
    const dayQueue = generateCheckinQueue(25, {
      BOOKED: 0,
      APPROVED: 25,
      COMPLETED: 0,
      CANCELLED: 0,
    });

    const daySummary = KpiCalculator.summarizeQueue(dayQueue);
    expect(daySummary.total).toBe(25);
    expect(daySummary.checkedIn).toBe(25);
    expect(daySummary.pending).toBe(0);
    expect(daySummary.progressPercentage).toBe(100);

    // 2. Admin audits day operations in /admin/audit-logs
    const dayAuditLogs = [
      generateAuditLog({ action: "CHECKIN_APPOINTMENT", actorName: "Nurse Thao", performedAt: `${today}T08:30:00Z` }),
      generateAuditLog({ action: "CHECKIN_APPOINTMENT", actorName: "Nurse Thao", performedAt: `${today}T09:15:00Z` }),
      generateAuditLog({ action: AUDIT_ACTIONS.ACCOUNT_CREATE, actorName: "Admin Root", performedAt: `${today}T14:00:00Z` }),
    ];

    const pager = new PaginationMachine(dayAuditLogs, 15);
    expect(pager.getTotalItems()).toBe(3);
    expect(pager.getPagedSlice()).toHaveLength(3);

    // 3. Admin reviews patient feedback submitted during the day
    const dayFeedbacks = [
      generateFeedbackItem({ rating: 5, submittedAt: `${today}T11:00:00Z`, doctorName: "BS. Minh" }),
      generateFeedbackItem({ rating: 5, submittedAt: `${today}T15:30:00Z`, doctorName: "BS. Nam" }),
      generateFeedbackItem({ rating: 4, submittedAt: `${today}T16:45:00Z`, doctorName: "BS. Minh" }),
    ];

    const feedbackPager = new PaginationMachine(dayFeedbacks, 15);
    expect(feedbackPager.getTotalItems()).toBe(3);

    // Verify all 5-star ratings have complete visual stars
    const highRatings = dayFeedbacks.filter((f) => f.rating === 5);
    expect(highRatings).toHaveLength(2);
    for (const fb of highRatings) {
      const visual = StarRatingCalculator.getVisualStars(fb.rating);
      expect(visual.filledStars).toBe(5);
      expect(visual.mutedStars).toBe(0);
    }

    // 4. Verify Admin RBAC access to navigate through all clinical records
    const rbac = new RbacPolicyEvaluator();
    for (const fb of dayFeedbacks) {
      expect(rbac.isAllowed("ADMIN", `/cases/${fb.caseId}`)).toBe(true);
      expect(rbac.isAllowed("ADMIN", `/patients/${fb.patientProfileId}`)).toBe(true);
    }
  });
});
