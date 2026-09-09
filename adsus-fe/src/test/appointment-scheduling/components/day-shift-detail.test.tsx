import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { DayShiftDetail } from '@/features/appointment-scheduling/components/day-shift-detail';
import { format } from 'date-fns';

// Mock Next.js Link
vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  )
}));

// Mock hooks
vi.mock('@/features/appointment-scheduling/hooks/use-doctor-appointments', () => ({
  useDoctorAppointments: vi.fn(() => ({
    data: [],
    isLoading: false,
    isError: false,
  }))
}));

const mockSummary = {
  date: '2023-10-15',
  morning: { status: 'WORKING' as const, totalSlots: 4, bookedSlots: 2, closedSlots: 0 },
  afternoon: { status: 'OFF' as const, totalSlots: 4, bookedSlots: 0, closedSlots: 0 },
};

describe('DayShiftDetail', () => {
  const mockOnOpenChange = vi.fn();
  const mockOnRequestClick = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders nothing when date is null', () => {
    const { container } = render(
      <DayShiftDetail
        open={true}
        onOpenChange={mockOnOpenChange}
        date={null}
      />
    );
    expect(container.firstChild).toBeNull();
  });

  it('renders details when open and date is provided', () => {
    const testDate = new Date('2023-10-15T00:00:00Z');
    
    render(
      <DayShiftDetail
        open={true}
        onOpenChange={mockOnOpenChange}
        date={testDate}
        summary={mockSummary}
        onRequestClick={mockOnRequestClick}
      />
    );

    const expectedTitle = `Chi tiết ngày ${format(testDate, 'dd/MM/yyyy')}`;
    expect(screen.getByText(expectedTitle)).toBeInTheDocument();

    expect(screen.getByText('Ca Sáng (08:00 - 12:00)')).toBeInTheDocument();
    expect(screen.getByText('Ca Chiều (13:00 - 17:00)')).toBeInTheDocument();
  });

  it('displays empty message when no patients are present', () => {
    const testDate = new Date('2023-10-15T00:00:00Z');
    
    render(
      <DayShiftDetail
        open={true}
        onOpenChange={mockOnOpenChange}
        date={testDate}
        summary={mockSummary}
        onRequestClick={mockOnRequestClick}
      />
    );

    expect(screen.getByText('Không có bệnh nhân đặt lịch')).toBeInTheDocument();
  });
  
  it('shows buttons for leave and overtime if date is in the future (>2 days)', () => {
    const futureDate = new Date();
    futureDate.setDate(futureDate.getDate() + 5);

    render(
      <DayShiftDetail
        open={true}
        onOpenChange={mockOnOpenChange}
        date={futureDate}
        summary={mockSummary}
        onRequestClick={mockOnRequestClick}
      />
    );

    const leaveButton = screen.getByText('Xin nghỉ phép');
    const overtimeButton = screen.getByText('Đăng ký tăng ca');

    expect(leaveButton).toBeInTheDocument();
    expect(overtimeButton).toBeInTheDocument();

    fireEvent.click(leaveButton);
    expect(mockOnRequestClick).toHaveBeenCalledWith('LEAVE', futureDate);

    fireEvent.click(overtimeButton);
    expect(mockOnRequestClick).toHaveBeenCalledWith('OVERTIME', futureDate);
  });
});
