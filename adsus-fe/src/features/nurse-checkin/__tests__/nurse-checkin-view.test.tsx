import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, act } from "@testing-library/react";
import { format } from "date-fns";
import { NurseCheckinView } from "../components/nurse-checkin-view";
import { useCheckinQueue } from "../hooks/use-checkin-queue";
import { useCheckin } from "../hooks/use-checkin";

vi.mock("../hooks/use-checkin-queue", () => ({
  useCheckinQueue: vi.fn(),
}));

vi.mock("../hooks/use-checkin", () => ({
  useCheckin: vi.fn(),
}));

describe("NurseCheckinView", () => {
  const mockRefetch = vi.fn();
  const mockMutate = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useCheckin).mockReturnValue({
      mutate: mockMutate,
      isPending: false,
    } as unknown as ReturnType<typeof useCheckin>);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("should initialize date pickers to today", () => {
    const todayStr = format(new Date(), "yyyy-MM-dd");
    vi.mocked(useCheckinQueue).mockReturnValue({
      data: { items: [], totalCount: 0, page: 1, pageSize: 15, totalPages: 0 },
      isLoading: false,
      refetch: mockRefetch,
      isRefetching: false,
    } as unknown as ReturnType<typeof useCheckinQueue>);

    render(<NurseCheckinView />);

    expect(useCheckinQueue).toHaveBeenCalledWith(
      expect.objectContaining({
        fromDate: todayStr,
        toDate: todayStr,
        status: "ALL",
        page: 1,
        pageSize: 15,
      })
    );
  });

  it("should handle date presets 'Hôm nay', '7 ngày qua', '30 ngày qua' and reset page to 1", () => {
    vi.mocked(useCheckinQueue).mockReturnValue({
      data: { items: [], totalCount: 0, page: 1, pageSize: 15, totalPages: 0 },
      isLoading: false,
      refetch: mockRefetch,
      isRefetching: false,
    } as unknown as ReturnType<typeof useCheckinQueue>);

    render(<NurseCheckinView />);

    const btn7Days = screen.getByRole("button", { name: "7 ngày qua" });
    fireEvent.click(btn7Days);

    const now = new Date();
    const past7 = new Date();
    past7.setDate(now.getDate() - 6);

    expect(useCheckinQueue).toHaveBeenLastCalledWith(
      expect.objectContaining({
        fromDate: format(past7, "yyyy-MM-dd"),
        toDate: format(now, "yyyy-MM-dd"),
        page: 1,
      })
    );

    const btn30Days = screen.getByRole("button", { name: "30 ngày qua" });
    fireEvent.click(btn30Days);

    const past30 = new Date();
    past30.setDate(now.getDate() - 29);

    expect(useCheckinQueue).toHaveBeenLastCalledWith(
      expect.objectContaining({
        fromDate: format(past30, "yyyy-MM-dd"),
        toDate: format(now, "yyyy-MM-dd"),
        page: 1,
      })
    );

    const btnToday = screen.getByRole("button", { name: "Hôm nay" });
    fireEvent.click(btnToday);

    expect(useCheckinQueue).toHaveBeenLastCalledWith(
      expect.objectContaining({
        fromDate: format(now, "yyyy-MM-dd"),
        toDate: format(now, "yyyy-MM-dd"),
        page: 1,
      })
    );
  });

  it("should auto-swap start and end dates when start date is after end date", () => {
    vi.mocked(useCheckinQueue).mockReturnValue({
      data: { items: [], totalCount: 0, page: 1, pageSize: 15, totalPages: 0 },
      isLoading: false,
      refetch: mockRefetch,
      isRefetching: false,
    } as unknown as ReturnType<typeof useCheckinQueue>);

    render(<NurseCheckinView />);

    const inputs = screen.getAllByPlaceholderText("dd/mm/yyyy");
    const fromInput = inputs[0];

    fireEvent.change(fromInput, { target: { value: "25/09/2026" } });

    expect(useCheckinQueue).toHaveBeenLastCalledWith(
      expect.objectContaining({
        toDate: "2026-09-25",
        page: 1,
      })
    );
  });


  it("should filter by status category and reset page to 1", () => {
    vi.mocked(useCheckinQueue).mockReturnValue({
      data: { items: [], totalCount: 0, page: 1, pageSize: 15, totalPages: 0 },
      isLoading: false,
      refetch: mockRefetch,
      isRefetching: false,
    } as unknown as ReturnType<typeof useCheckinQueue>);

    render(<NurseCheckinView />);

    const select = screen.getByRole("combobox");
    fireEvent.change(select, { target: { value: "BOOKED" } });

    expect(useCheckinQueue).toHaveBeenLastCalledWith(
      expect.objectContaining({
        status: "BOOKED",
        page: 1,
      })
    );

    fireEvent.change(select, { target: { value: "APPROVED" } });
    expect(useCheckinQueue).toHaveBeenLastCalledWith(
      expect.objectContaining({
        status: "APPROVED",
        page: 1,
      })
    );
  });

  it("should debounce realtime search with 300ms timer", () => {
    vi.useFakeTimers();

    vi.mocked(useCheckinQueue).mockReturnValue({
      data: { items: [], totalCount: 0, page: 1, pageSize: 15, totalPages: 0 },
      isLoading: false,
      refetch: mockRefetch,
      isRefetching: false,
    } as unknown as ReturnType<typeof useCheckinQueue>);

    render(<NurseCheckinView />);

    const searchInput = screen.getByPlaceholderText(/Tìm kiếm bệnh nhân/i);
    fireEvent.change(searchInput, { target: { value: "Nguyễn" } });

    // Immediately, search is not dispatched yet
    expect(useCheckinQueue).not.toHaveBeenLastCalledWith(
      expect.objectContaining({
        search: "Nguyễn",
      })
    );

    // Fast-forward 300ms
    act(() => {
      vi.advanceTimersByTime(300);
    });

    expect(useCheckinQueue).toHaveBeenLastCalledWith(
      expect.objectContaining({
        search: "Nguyễn",
        page: 1,
      })
    );

    vi.useRealTimers();
  });

  it("should safely handle zero-division in progress bar and KPI counters when totalCount is 0", () => {
    vi.mocked(useCheckinQueue).mockReturnValue({
      data: {
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 15,
        totalPages: 0,
        bookedCount: 0,
        checkedInCount: 0,
        cancelledCount: 0,
      },
      isLoading: false,
      refetch: mockRefetch,
      isRefetching: false,
    } as unknown as ReturnType<typeof useCheckinQueue>);

    render(<NurseCheckinView />);

    // Checked in count = 0, Pending count = 0
    expect(screen.getAllByText("Đã check-in").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Đang chờ check-in").length).toBeGreaterThan(0);

    // Progress bar must show 0 / 0 (0%) without NaN
    expect(screen.getByText(/0 \/ 0 bệnh nhân \(0%\)/)).toBeInTheDocument();
  });

  it("should calculate correct KPI numbers and progress bar width when items exist", () => {
    const items = [
      {
        appointmentId: "app-1",
        slotTime: "2026-09-10T08:00:00Z",
        patientFullName: "Bệnh nhân A",
        patientPhone: "0901111111",
        patientProfileId: "prof-1",
        caseId: "case-1",
        reason: null,
        doctorName: "BS. Minh",
        status: "Approved",
      },
      {
        appointmentId: "app-2",
        slotTime: "2026-09-10T08:30:00Z",
        patientFullName: "Bệnh nhân B",
        patientPhone: "0902222222",
        patientProfileId: "prof-2",
        caseId: "case-2",
        reason: null,
        doctorName: "BS. Minh",
        status: "Booked",
      },
    ];

    vi.mocked(useCheckinQueue).mockReturnValue({
      data: {
        items,
        totalCount: 2,
        page: 1,
        pageSize: 15,
        totalPages: 1,
        bookedCount: 1,
        checkedInCount: 1,
        cancelledCount: 0,
      },
      isLoading: false,
      refetch: mockRefetch,
      isRefetching: false,
    } as unknown as ReturnType<typeof useCheckinQueue>);

    render(<NurseCheckinView />);

    // 1 checked in out of 2 total -> 50%
    expect(screen.getByText(/1 \/ 2 bệnh nhân \(50%\)/)).toBeInTheDocument();
    expect(screen.getByText("Đang xem 2 / 2 kết quả")).toBeInTheDocument();
  });

  it("should render pagination control and trigger page change", () => {
    vi.mocked(useCheckinQueue).mockReturnValue({
      data: {
        items: new Array(15).fill(null).map((_, i) => ({
          appointmentId: `app-${i}`,
          slotTime: "2026-09-10T08:00:00Z",
          patientFullName: `Bệnh nhân ${i}`,
          patientPhone: "0900000000",
          patientProfileId: `prof-${i}`,
          caseId: `case-${i}`,
          reason: null,
          doctorName: "BS. Lan",
          status: "Booked",
        })),
        totalCount: 30,
        page: 1,
        pageSize: 15,
        totalPages: 2,
      },
      isLoading: false,
      refetch: mockRefetch,
      isRefetching: false,
    } as unknown as ReturnType<typeof useCheckinQueue>);

    render(<NurseCheckinView />);

    expect(screen.getByText("Đang xem 15 / 30 kết quả")).toBeInTheDocument();
    const nextBtn = screen.getByRole("button", { name: "Sau" });
    fireEvent.click(nextBtn);

    expect(useCheckinQueue).toHaveBeenLastCalledWith(
      expect.objectContaining({
        page: 2,
      })
    );
  });
});
