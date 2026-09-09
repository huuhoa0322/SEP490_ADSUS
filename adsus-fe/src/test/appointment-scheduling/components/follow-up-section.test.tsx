import { render, screen, fireEvent } from "@testing-library/react";
import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";

import { FollowUpSection } from "@/features/appointment-scheduling/components/follow-up-section";
import { format } from "date-fns";

const { useScheduleSlotsMock } = vi.hoisted(() => ({
  useScheduleSlotsMock: vi.fn(),
}));

vi.mock("@/features/appointment-scheduling/hooks/use-schedule-slot", () => ({
  useScheduleSlots: useScheduleSlotsMock,
}));

function buildSlot(overrides: Record<string, unknown> = {}) {
  return {
    slotId: "slot-1",
    slotDate: "2026-09-10",
    startTime: "08:00:00",
    endTime: "08:30:00",
    status: "OPEN",
    ...overrides,
  };
}

describe("FollowUpSection", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 8, 9)); // 2026-09-09
    useScheduleSlotsMock.mockClear();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("gọi useScheduleSlots với ngày hôm nay khi chưa chọn ngày", () => {
    useScheduleSlotsMock.mockReturnValue({ data: undefined, isLoading: true });

    render(
      <FollowUpSection
        isFollowUp={true}
        setIsFollowUp={vi.fn()}
        followUpDate=""
        setFollowUpDate={vi.fn()}
        followUpSlotId=""
        setFollowUpSlotId={vi.fn()}
        followUpReason=""
        setFollowUpReason={vi.fn()}
      />
    );

    const today = new Date(2026, 8, 9);

    expect(useScheduleSlotsMock).toHaveBeenCalledWith({
      fromDate: format(today, "yyyy-MM-dd"),
      toDate: format(today, "yyyy-MM-dd"),
      status: "OPEN",
      pageSize: 100,
    });
  });

  it("gọi useScheduleSlots với đúng ngày được chọn", () => {
    useScheduleSlotsMock.mockReturnValue({ data: undefined, isLoading: true });

    render(
      <FollowUpSection
        isFollowUp={true}
        setIsFollowUp={vi.fn()}
        followUpDate="2026-10-15"
        setFollowUpDate={vi.fn()}
        followUpSlotId=""
        setFollowUpSlotId={vi.fn()}
        followUpReason=""
        setFollowUpReason={vi.fn()}
      />
    );

    expect(useScheduleSlotsMock).toHaveBeenCalledWith({
      fromDate: "2026-10-15",
      toDate: "2026-10-15",
      status: "OPEN",
      pageSize: 100,
    });
  });

  it("ẩn form chọn ngày khi isFollowUp là false", () => {
    useScheduleSlotsMock.mockReturnValue({ data: undefined, isLoading: false });

    render(
      <FollowUpSection
        isFollowUp={false}
        setIsFollowUp={vi.fn()}
        followUpDate=""
        setFollowUpDate={vi.fn()}
        followUpSlotId=""
        setFollowUpSlotId={vi.fn()}
        followUpReason=""
        setFollowUpReason={vi.fn()}
      />
    );

    expect(screen.queryByText(/Ngày tái khám/i)).not.toBeInTheDocument();
  });

  it("hiển thị các trường khi isFollowUp là true", () => {
    useScheduleSlotsMock.mockReturnValue({
      data: [
        buildSlot({ slotId: "s1", slotDate: "2026-09-10" }),
      ],
      isLoading: false,
    });

    render(
      <FollowUpSection
        isFollowUp={true}
        setIsFollowUp={vi.fn()}
        followUpDate=""
        setFollowUpDate={vi.fn()}
        followUpSlotId=""
        setFollowUpSlotId={vi.fn()}
        followUpReason=""
        setFollowUpReason={vi.fn()}
      />
    );

    expect(screen.getByText(/Ngày tái khám/i)).toBeInTheDocument();
    expect(screen.getAllByText(/Ca khám/i).length).toBeGreaterThan(0);
    expect(screen.getByText(/Lý do tái khám/i)).toBeInTheDocument();
  });

  it("cho phép đổi trạng thái isFollowUp", () => {
    useScheduleSlotsMock.mockReturnValue({ data: undefined, isLoading: false });

    const setIsFollowUp = vi.fn();
    render(
      <FollowUpSection
        isFollowUp={false}
        setIsFollowUp={setIsFollowUp}
        followUpDate=""
        setFollowUpDate={vi.fn()}
        followUpSlotId=""
        setFollowUpSlotId={vi.fn()}
        followUpReason=""
        setFollowUpReason={vi.fn()}
      />
    );

    const checkbox = screen.getByRole("checkbox");
    fireEvent.click(checkbox);
    
    expect(setIsFollowUp).toHaveBeenCalledWith(true);
  });
});
