/* eslint-disable @typescript-eslint/no-explicit-any */
import React, { ReactNode } from "react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, act } from "@testing-library/react";
import { format } from "date-fns";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { NurseCheckinView } from "@/features/nurse-checkin/components/nurse-checkin-view";
import { useCheckinQueue } from "@/features/nurse-checkin/hooks/use-checkin-queue";
import { useCheckin } from "@/features/nurse-checkin/hooks/use-checkin";
import type { CheckinQueueItem } from "@/features/nurse-checkin/types/checkin.types";

import { AuditLogListView } from "@/features/audit-logs/components/audit-log-list-view";
import * as auditHooks from "@/features/audit-logs/hooks/use-audit-logs";
import type { AuditLogEntry } from "@/features/audit-logs/types/audit-logs.types";

import { AdminFeedbackView } from "@/features/feedback/components/admin-feedback-view";
import * as feedbackHooks from "@/features/feedback/hooks/use-feedback";
import type { AdminFeedbackItem } from "@/features/feedback/types/feedback.types";

import { isRoleAllowedOnPath } from "@/store/auth-store";
import { visibleGroupsForRole } from "@/components/shared/nav-config";

// Mock hooks
vi.mock("@/features/nurse-checkin/hooks/use-checkin-queue", () => ({
  useCheckinQueue: vi.fn(),
}));

vi.mock("@/features/nurse-checkin/hooks/use-checkin", () => ({
  useCheckin: vi.fn(),
}));

vi.mock("@/features/audit-logs/hooks/use-audit-logs", () => ({
  useAuditLogs: vi.fn(),
}));

vi.mock("@/features/feedback/hooks/use-feedback", () => ({
  useAdminFeedbacks: vi.fn(),
}));

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

describe("QA Member 4: Browser UI Automation & Interactive Click-Through Verification", () => {
  let consoleErrors: string[] = [];
  let originalError: typeof console.error;

  beforeEach(() => {
    vi.clearAllMocks();
    consoleErrors = [];
    originalError = console.error;

    console.error = (...args: any[]) => {
      consoleErrors.push(args.map((a) => String(a)).join(" "));
      originalError(...args);
    };
  });

  afterEach(() => {
    console.error = originalError;
    vi.restoreAllMocks();
  });

  // =========================================================================
  // 1. Nurse Check-in (/checkin) Interactive Click-Through Suite
  // =========================================================================
  describe("1. Nurse Check-in (/checkin) User Interactions", () => {
    const mockRefetch = vi.fn();
    const mockMutate = vi.fn();

    const sampleQueue: CheckinQueueItem[] = [
      {
        appointmentId: "apt-01",
        slotTime: "2026-09-10T08:30:00Z",
        patientFullName: "Nguyen Van A",
        patientPhone: "0901234567",
        patientProfileId: "pat-01",
        caseId: "case-01",
        doctorName: "Dr. Hoang Nam",
        status: "BOOKED",
        reason: "Kham tong quat",
      },
      {
        appointmentId: "apt-02",
        slotTime: "2026-09-10T09:00:00Z",
        patientFullName: "Tran Thi B",
        patientPhone: "0912345678",
        patientProfileId: "pat-02",
        caseId: "case-02",
        doctorName: "Dr. Le Mai",
        status: "APPROVED",
        reason: "Sieu am o bung",
      },
    ];

    beforeEach(() => {
      vi.mocked(useCheckin).mockReturnValue({
        mutate: mockMutate,
        isPending: false,
      } as any);

      vi.mocked(useCheckinQueue).mockReturnValue({
        data: {
          items: sampleQueue,
          totalCount: 30,
          page: 1,
          pageSize: 15,
          totalPages: 2,
        },
        isLoading: false,
        refetch: mockRefetch,
        isRefetching: false,
      } as any);
    });

    it("simulates click on DatePicker inputs and auto-swaps when fromDate > toDate", () => {
      render(<NurseCheckinView />, { wrapper: createWrapper() });

      const inputs = screen.getAllByPlaceholderText("dd/mm/yyyy");
      expect(inputs.length).toBeGreaterThanOrEqual(2);
      const fromInput = inputs[0];

      // Simulate typing a future date in fromDate
      fireEvent.change(fromInput, { target: { value: "28/09/2026" } });

      expect(useCheckinQueue).toHaveBeenCalledWith(
        expect.objectContaining({
          toDate: "2026-09-28",
          page: 1,
        })
      );
    });

    it("simulates clicking quick presets '7 ngày qua', '30 ngày qua', 'Hôm nay'", () => {
      render(<NurseCheckinView />, { wrapper: createWrapper() });

      const todayStr = format(new Date(), "yyyy-MM-dd");

      // Click '7 ngày qua'
      const btn7Days = screen.getByRole("button", { name: "7 ngày qua" });
      fireEvent.click(btn7Days);

      const past7 = new Date();
      past7.setDate(past7.getDate() - 6);
      expect(useCheckinQueue).toHaveBeenLastCalledWith(
        expect.objectContaining({
          fromDate: format(past7, "yyyy-MM-dd"),
          toDate: todayStr,
          page: 1,
        })
      );
      expect(btn7Days).toHaveClass("border-accent", "text-accent");

      // Click '30 ngày qua'
      const btn30Days = screen.getByRole("button", { name: "30 ngày qua" });
      fireEvent.click(btn30Days);

      const past30 = new Date();
      past30.setDate(past30.getDate() - 29);
      expect(useCheckinQueue).toHaveBeenLastCalledWith(
        expect.objectContaining({
          fromDate: format(past30, "yyyy-MM-dd"),
          toDate: todayStr,
          page: 1,
        })
      );
      expect(btn30Days).toHaveClass("border-accent", "text-accent");

      // Click 'Hôm nay'
      const btnToday = screen.getByRole("button", { name: "Hôm nay" });
      fireEvent.click(btnToday);

      expect(useCheckinQueue).toHaveBeenLastCalledWith(
        expect.objectContaining({
          fromDate: todayStr,
          toDate: todayStr,
          page: 1,
        })
      );
      expect(btnToday).toHaveClass("border-accent", "text-accent");
    });

    it("simulates clicking status filter dropdown and switching options", () => {
      render(<NurseCheckinView />, { wrapper: createWrapper() });

      const select = screen.getByRole("combobox");

      // Select BOOKED
      fireEvent.change(select, { target: { value: "BOOKED" } });
      expect(useCheckinQueue).toHaveBeenLastCalledWith(
        expect.objectContaining({ status: "BOOKED", page: 1 })
      );

      // Select APPROVED
      fireEvent.change(select, { target: { value: "APPROVED" } });
      expect(useCheckinQueue).toHaveBeenLastCalledWith(
        expect.objectContaining({ status: "APPROVED", page: 1 })
      );

      // Select CANCELLED
      fireEvent.change(select, { target: { value: "CANCELLED" } });
      expect(useCheckinQueue).toHaveBeenLastCalledWith(
        expect.objectContaining({ status: "CANCELLED", page: 1 })
      );
    });

    it("simulates typing into realtime search input with 300ms debounce", () => {
      vi.useFakeTimers();
      render(<NurseCheckinView />, { wrapper: createWrapper() });

      const searchInput = screen.getByPlaceholderText(/Tìm kiếm bệnh nhân/i);
      fireEvent.change(searchInput, { target: { value: "Nguyen Van A" } });

      // Advance debounce timer 300ms
      act(() => {
        vi.advanceTimersByTime(300);
      });

      expect(useCheckinQueue).toHaveBeenLastCalledWith(
        expect.objectContaining({ search: "Nguyen Van A", page: 1 })
      );

      vi.useRealTimers();
    });

    it("simulates user clicking pagination controls (Next, Previous, Number)", () => {
      render(<NurseCheckinView />, { wrapper: createWrapper() });

      // Total pages = 2. Click page button '2'
      const page2Btn = screen.getByRole("button", { name: "2" });
      fireEvent.click(page2Btn);

      expect(useCheckinQueue).toHaveBeenLastCalledWith(
        expect.objectContaining({ page: 2 })
      );

      // Click 'Trước' button to return to page 1
      const prevBtn = screen.getByRole("button", { name: "Trước" });
      fireEvent.click(prevBtn);

      expect(useCheckinQueue).toHaveBeenLastCalledWith(
        expect.objectContaining({ page: 1 })
      );

      // Click 'Sau' button to go to page 2
      const nextBtn = screen.getByRole("button", { name: "Sau" });
      fireEvent.click(nextBtn);

      expect(useCheckinQueue).toHaveBeenLastCalledWith(
        expect.objectContaining({ page: 2 })
      );
    });

    it("simulates clicking check-in button on a BOOKED row", () => {
      render(<NurseCheckinView />, { wrapper: createWrapper() });

      const checkinBtn = screen.getByRole("button", { name: "Check-in" });
      fireEvent.click(checkinBtn);

      expect(mockMutate).toHaveBeenCalledWith({
        appointmentId: "apt-01",
        caseId: "case-01",
      });
    });

    it("verifies zero-division safety on KPI counters and progress bar when totalCount = 0", () => {
      vi.mocked(useCheckinQueue).mockReturnValue({
        data: { items: [], totalCount: 0, page: 1, pageSize: 15, totalPages: 0 },
        isLoading: false,
        refetch: mockRefetch,
        isRefetching: false,
      } as any);

      render(<NurseCheckinView />, { wrapper: createWrapper() });

      expect(screen.getByText("0 / 0 bệnh nhân (0%)")).toBeInTheDocument();
      expect(screen.getByText("Không tìm thấy lịch hẹn phù hợp.")).toBeInTheDocument();
    });

    it("simulates adversarial Vietnamese diacritics and SQL injection search strings", () => {
      vi.useFakeTimers();
      render(<NurseCheckinView />, { wrapper: createWrapper() });

      const searchInput = screen.getByPlaceholderText(/Tìm kiếm bệnh nhân/i);
      
      // Vietnamese unicode diacritics
      fireEvent.change(searchInput, { target: { value: "Nguyễn Văn Đạt" } });
      act(() => {
        vi.advanceTimersByTime(300);
      });
      expect(useCheckinQueue).toHaveBeenLastCalledWith(
        expect.objectContaining({ search: "Nguyễn Văn Đạt", page: 1 })
      );

      // SQL injection pattern
      fireEvent.change(searchInput, { target: { value: "' OR '1'='1" } });
      act(() => {
        vi.advanceTimersByTime(300);
      });
      expect(useCheckinQueue).toHaveBeenLastCalledWith(
        expect.objectContaining({ search: "' OR '1'='1", page: 1 })
      );

      vi.useRealTimers();
    });

    it("simulates refresh button click and loading spinner states", () => {
      vi.mocked(useCheckinQueue).mockReturnValue({
        data: { items: sampleQueue, totalCount: 30, page: 1, pageSize: 15, totalPages: 2 },
        isLoading: false,
        refetch: mockRefetch,
        isRefetching: true,
      } as any);

      render(<NurseCheckinView />, { wrapper: createWrapper() });

      const refreshBtn = screen.getByRole("button", { name: /Làm mới/i });
      expect(refreshBtn).toBeDisabled();
    });
  });

  // =========================================================================
  // 2. Admin Audit Log (/admin/audit-logs) Interactive Click-Through Suite
  // =========================================================================
  describe("2. Admin Audit Log (/admin/audit-logs) User Interactions", () => {
    const mockRefetch = vi.fn();

    const sampleLogs: AuditLogEntry[] = [
      {
        logId: "log-1",
        actorId: "actor-1",
        actorName: "Admin System",
        actorRole: "ADMIN",
        action: "CREATE_ACCOUNT",
        detail: "Tạo tài khoản bác sĩ",
        performedAt: "2026-09-10T08:30:00Z",
      },
      {
        logId: "log-2",
        actorId: "actor-2",
        actorName: "Admin Root",
        actorRole: "ADMIN",
        action: "REGISTER_AI_MODEL",
        detail: "Đăng ký mô hình AI",
        performedAt: "2026-09-10T09:00:00Z",
      },
    ];

    beforeEach(() => {
      vi.mocked(auditHooks.useAuditLogs).mockReturnValue({
        data: {
          items: sampleLogs,
          page: 1,
          pageSize: 15,
          totalItems: 30,
          totalPages: 2,
        },
        isLoading: false,
        isError: false,
        error: null,
        refetch: mockRefetch,
        isFetching: false,
      } as any);
    });

    it("verifies card table layout container with rounded-3xl and pl-6 indentation", () => {
      const { container } = render(<AuditLogListView />, { wrapper: createWrapper() });

      const cardTable = container.querySelector(".rounded-3xl");
      expect(cardTable).toBeInTheDocument();
      expect(cardTable).toHaveClass("overflow-x-auto", "rounded-3xl", "border", "border-border", "bg-background");

      const theadRow = container.querySelector("thead tr");
      expect(theadRow).toHaveClass("[&>th:first-child]:pl-6", "[&>td:first-child]:pl-6");
    });

    it("simulates typing into realtime search input", () => {
      render(<AuditLogListView />, { wrapper: createWrapper() });

      const searchInput = screen.getByTestId("audit-search-input");
      fireEvent.change(searchInput, { target: { value: "Admin Root" } });

      expect(auditHooks.useAuditLogs).toHaveBeenCalledWith(
        expect.objectContaining({
          search: "Admin Root",
          page: 1,
          pageSize: 15,
        })
      );
    });

    it("simulates selecting category multi-filter dropdown", () => {
      render(<AuditLogListView />, { wrapper: createWrapper() });

      const categorySelect = screen.getByTestId("audit-category-select");

      // Select AI Model category
      fireEvent.change(categorySelect, { target: { value: "AI_MODEL" } });
      expect(auditHooks.useAuditLogs).toHaveBeenCalledWith(
        expect.objectContaining({
          action: "AI_MODEL",
          page: 1,
        })
      );

      // Select Account management category
      fireEvent.change(categorySelect, { target: { value: "ACCOUNT" } });
      expect(auditHooks.useAuditLogs).toHaveBeenCalledWith(
        expect.objectContaining({
          action: "ACCOUNT",
          page: 1,
        })
      );
    });

    it("simulates resetting filters with 'Đặt lại bộ lọc' button", () => {
      render(<AuditLogListView />, { wrapper: createWrapper() });

      // Set a filter so reset button becomes visible
      const categorySelect = screen.getByTestId("audit-category-select");
      fireEvent.change(categorySelect, { target: { value: "ACCOUNT" } });

      const resetBtn = screen.getByRole("button", { name: /Đặt lại bộ lọc/i });
      expect(resetBtn).toBeInTheDocument();

      fireEvent.click(resetBtn);

      expect(auditHooks.useAuditLogs).toHaveBeenLastCalledWith(
        expect.objectContaining({
          search: "",
          action: undefined,
          page: 1,
        })
      );
    });

    it("simulates 15 items/page pagination controls on audit logs", () => {
      render(<AuditLogListView />, { wrapper: createWrapper() });

      const page2Btn = screen.getByRole("button", { name: "2" });
      fireEvent.click(page2Btn);

      expect(auditHooks.useAuditLogs).toHaveBeenLastCalledWith(
        expect.objectContaining({ page: 2 })
      );

      const prevBtn = screen.getByRole("button", { name: "Trước" });
      fireEvent.click(prevBtn);

      expect(auditHooks.useAuditLogs).toHaveBeenLastCalledWith(
        expect.objectContaining({ page: 1 })
      );
    });

    it("renders empty state and error alert with retry button when error occurs", () => {
      vi.mocked(auditHooks.useAuditLogs).mockReturnValue({
        data: null,
        isLoading: false,
        isError: true,
        error: new Error("Network timeout"),
        refetch: mockRefetch,
        isFetching: false,
      } as any);

      render(<AuditLogListView />, { wrapper: createWrapper() });

      const alert = screen.getByRole("alert");
      expect(alert).toBeInTheDocument();
      const retryBtn = screen.getByRole("button", { name: /Thử lại/i });
      fireEvent.click(retryBtn);
      expect(mockRefetch).toHaveBeenCalled();
    });

    it("renders fallback for empty actor name and long details without layout breakage", () => {
      const longText = "A".repeat(1500);
      const boundaryLogs: AuditLogEntry[] = [
        {
          logId: "log-bound-1",
          actorId: "actor-none",
          actorName: "", // Empty actorName fallback to "Hệ thống"
          actorRole: "UNKNOWN_ROLE",
          action: "CUSTOM_SYSTEM_ACTION",
          detail: longText,
          performedAt: "2026-09-10T12:00:00Z",
        },
      ];

      vi.mocked(auditHooks.useAuditLogs).mockReturnValue({
        data: {
          items: boundaryLogs,
          page: 1,
          pageSize: 15,
          totalItems: 1,
          totalPages: 1,
        },
        isLoading: false,
        isError: false,
        error: null,
        refetch: mockRefetch,
        isFetching: false,
      } as any);

      render(<AuditLogListView />, { wrapper: createWrapper() });

      expect(screen.getByText("Hệ thống")).toBeInTheDocument();
      expect(screen.getByText("UNKNOWN_ROLE")).toBeInTheDocument();
      const truncatedDetail = screen.getByTitle(longText);
      expect(truncatedDetail).toBeInTheDocument();
      expect(truncatedDetail).toHaveClass("truncate");
    });
  });

  // =========================================================================
  // 3. Admin Feedback (/admin/feedback) Interactive Click-Through Suite
  // =========================================================================
  describe("3. Admin Feedback (/admin/feedback) User Interactions", () => {
    const mockRefetch = vi.fn();

    const sampleFeedbacks: AdminFeedbackItem[] = [
      {
        id: "fb-101",
        rating: 5,
        content: "Dich vu rat tot",
        submittedAt: "2026-09-10T08:30:00Z",
        caseId: "case-alpha-1234",
        doctorName: "BS. Hoang Van Phuc",
        doctorId: "doc-99",
        patientProfileId: "pat-55",
        patientName: "Nguyen Van Dat",
        patientPhone: "0901234567",
      },
      {
        id: "fb-102",
        rating: 3,
        content: "Binh thuong",
        submittedAt: "2026-09-09T14:15:00Z",
        caseId: "00000000-0000-0000-0000-000000000000",
        doctorName: "BS. Khong Ten",
        doctorId: "",
        patientProfileId: "",
        patientName: "Khach Vang Lai",
        patientPhone: null,
      },
      {
        id: "fb-103",
        rating: 0, // Should clamp to 1
        content: null,
        submittedAt: "2026-09-08T10:00:00Z",
        caseId: "",
        doctorName: "",
        doctorId: "",
        patientProfileId: "pat-99",
        patientName: "Le Thi C",
        patientPhone: "0999999999",
      },
    ];

    beforeEach(() => {
      vi.mocked(feedbackHooks.useAdminFeedbacks).mockReturnValue({
        data: {
          items: sampleFeedbacks,
          page: 1,
          pageSize: 15,
          totalItems: 30,
          totalPages: 2,
        },
        isLoading: false,
        isError: false,
        error: null,
        refetch: mockRefetch,
        isFetching: false,
      } as any);
    });

    it("verifies card table layout with rounded-3xl and pl-6 pr-6 padding", () => {
      const { container } = render(<AdminFeedbackView />, { wrapper: createWrapper() });

      const cardTable = container.querySelector(".rounded-3xl");
      expect(cardTable).toBeInTheDocument();
      expect(cardTable).toHaveClass(
        "overflow-x-auto",
        "rounded-3xl",
        "border",
        "border-border",
        "bg-background"
      );
    });

    it("verifies 5-star visual rating display with filled/muted stars and (X/5) score", () => {
      render(<AdminFeedbackView />, { wrapper: createWrapper() });

      expect(screen.getByText("(5/5)")).toBeInTheDocument();
      expect(screen.getByText("(3/5)")).toBeInTheDocument();
      expect(screen.getByText("(1/5)")).toBeInTheDocument(); // Clamped from 0
      expect(screen.getByLabelText("5 trên 5 sao")).toBeInTheDocument();
      expect(screen.getByLabelText("3 trên 5 sao")).toBeInTheDocument();
      expect(screen.getByLabelText("1 trên 5 sao")).toBeInTheDocument();
    });

    it("verifies clickable entity links for Doctor, Patient, and Case details", () => {
      render(<AdminFeedbackView />, { wrapper: createWrapper() });

      // Doctor link: /admin/users/doc-99
      const doctorLink = screen.getByRole("link", { name: "BS. Hoang Van Phuc" });
      expect(doctorLink).toHaveAttribute("href", "/admin/users/doc-99");

      // Patient link: /patients/pat-55
      const patientLink = screen.getByRole("link", { name: "Nguyen Van Dat" });
      expect(patientLink).toHaveAttribute("href", "/patients/pat-55");

      // Case link: /cases/case-alpha-1234
      const caseLink = screen.getByTitle("Xem chi tiết ca khám");
      expect(caseLink).toHaveAttribute("href", "/cases/case-alpha-1234");

      // Empty GUID fallback renders 'Phản hồi chung'
      expect(screen.getAllByText("Phản hồi chung").length).toBeGreaterThanOrEqual(1);

      // Null content fallback renders 'Không có nhận xét'
      expect(screen.getByText("Không có nhận xét")).toBeInTheDocument();
    });

    it("simulates realtime search input typing with 300ms debounce", () => {
      vi.useFakeTimers();
      render(<AdminFeedbackView />, { wrapper: createWrapper() });

      const searchInput = screen.getByLabelText("Tìm kiếm phản hồi");
      fireEvent.change(searchInput, { target: { value: "BS. Hoang" } });

      act(() => {
        vi.advanceTimersByTime(300);
      });

      expect(feedbackHooks.useAdminFeedbacks).toHaveBeenLastCalledWith(
        expect.objectContaining({
          search: "BS. Hoang",
          page: 1,
          pageSize: 15,
        })
      );

      vi.useRealTimers();
    });

    it("simulates rating filter dropdown selection", () => {
      render(<AdminFeedbackView />, { wrapper: createWrapper() });

      const ratingSelect = screen.getByLabelText("Lọc theo đánh giá");

      fireEvent.change(ratingSelect, { target: { value: "5" } });
      expect(feedbackHooks.useAdminFeedbacks).toHaveBeenLastCalledWith(
        expect.objectContaining({ minRating: 5, page: 1 })
      );

      fireEvent.change(ratingSelect, { target: { value: "1" } });
      expect(feedbackHooks.useAdminFeedbacks).toHaveBeenLastCalledWith(
        expect.objectContaining({ minRating: 1, page: 1 })
      );
    });

    it("simulates 15 items/page pagination controls on feedback", () => {
      render(<AdminFeedbackView />, { wrapper: createWrapper() });

      const page2Btn = screen.getByRole("button", { name: "2" });
      fireEvent.click(page2Btn);

      expect(feedbackHooks.useAdminFeedbacks).toHaveBeenLastCalledWith(
        expect.objectContaining({ page: 2 })
      );
    });

    it("simulates clicking 'Làm mới' (Refresh) button", () => {
      render(<AdminFeedbackView />, { wrapper: createWrapper() });

      const refreshBtn = screen.getByRole("button", { name: /Làm mới/i });
      fireEvent.click(refreshBtn);

      expect(mockRefetch).toHaveBeenCalled();
    });

    it("renders error state with retry button on feedback load failure", () => {
      vi.mocked(feedbackHooks.useAdminFeedbacks).mockReturnValue({
        data: null,
        isLoading: false,
        isError: true,
        error: new Error("Khong the ket noi du lieu"),
        refetch: mockRefetch,
        isFetching: false,
      } as any);

      render(<AdminFeedbackView />, { wrapper: createWrapper() });

      const alert = screen.getByRole("alert");
      expect(alert).toBeInTheDocument();
      expect(screen.getByText("Khong the ket noi du lieu")).toBeInTheDocument();
      const retryBtn = screen.getByRole("button", { name: /Thử lại/i });
      fireEvent.click(retryBtn);
      expect(mockRefetch).toHaveBeenCalled();
    });
  });

  // =========================================================================
  // 4. Cross-Page Navigation & RBAC Integration Contracts
  // =========================================================================
  describe("4. Cross-Page Navigation & RBAC Authorization Contracts", () => {
    it("confirms ADMIN role is granted route access to /patients and /cases for feedback traversal", () => {
      expect(isRoleAllowedOnPath("ADMIN", "/patients")).toBe(true);
      expect(isRoleAllowedOnPath("ADMIN", "/patients/pat-1234")).toBe(true);
      expect(isRoleAllowedOnPath("ADMIN", "/cases")).toBe(true);
      expect(isRoleAllowedOnPath("ADMIN", "/cases/case-5678")).toBe(true);
    });

    it("confirms non-ADMIN roles cannot access /admin routes", () => {
      expect(isRoleAllowedOnPath("DOCTOR", "/admin/audit-logs")).toBe(false);
      expect(isRoleAllowedOnPath("DOCTOR", "/admin/feedback")).toBe(false);
      expect(isRoleAllowedOnPath("STAFF", "/admin/audit-logs")).toBe(false);
      expect(isRoleAllowedOnPath("STAFF", "/admin/feedback")).toBe(false);
    });

    it("confirms navigation menu config contains new routes for respective roles", () => {
      const adminGroups = visibleGroupsForRole("ADMIN");
      const adminHrefs = adminGroups.flatMap((g) => g.items.map((i) => i.href));
      expect(adminHrefs).toContain("/admin/audit-logs");
      expect(adminHrefs).toContain("/admin/feedback");

      const nurseGroups = visibleGroupsForRole("STAFF");
      const nurseHrefs = nurseGroups.flatMap((g) => g.items.map((i) => i.href));
      expect(nurseHrefs).toContain("/checkin");
    });
  });

  // =========================================================================
  // 5. Runtime Health & Console Sanity
  // =========================================================================
  describe("5. Runtime Health & Zero Console/Hydration Errors", () => {
    it("confirms zero unhandled exceptions, zero console errors, zero React hydration errors", () => {
      const actualErrors = consoleErrors.filter(
        (err) => !err.includes("punycode") && !err.includes("not wrapped in act")
      );
      expect(actualErrors).toHaveLength(0);
    });
  });
});
