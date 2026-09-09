import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { MonthCalendar } from '@/features/appointment-scheduling/components/month-calendar';
import { format } from 'date-fns';
import { vi as dateVi } from 'date-fns/locale';
import { DayShiftSummary } from '@/features/appointment-scheduling/types/shift-request.types';

const mockSummaries: DayShiftSummary[] = [
  {
    date: '2023-10-15',
    morning: { status: 'WORKING', totalSlots: 4, bookedSlots: 2, closedSlots: 0 },
    afternoon: { status: 'OFF', totalSlots: 4, bookedSlots: 0, closedSlots: 0 },
  }
];

describe('MonthCalendar', () => {
  const mockOnPrevMonth = vi.fn();
  const mockOnNextMonth = vi.fn();
  const mockOnDayClick = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the calendar with the current month and year', () => {
    const currentDate = new Date('2023-10-15T00:00:00Z');
    
    render(
      <MonthCalendar
        currentDate={currentDate}
        onPrevMonth={mockOnPrevMonth}
        onNextMonth={mockOnNextMonth}
        summaries={mockSummaries}
        onDayClick={mockOnDayClick}
      />
    );

    const monthString = format(currentDate, 'MMMM yyyy', { locale: dateVi });
    expect(screen.getByText(monthString)).toBeInTheDocument();
  });

  it('calls onPrevMonth and onNextMonth when buttons are clicked', () => {
    const currentDate = new Date('2023-10-15T00:00:00Z');
    
    render(
      <MonthCalendar
        currentDate={currentDate}
        onPrevMonth={mockOnPrevMonth}
        onNextMonth={mockOnNextMonth}
        summaries={[]}
        onDayClick={mockOnDayClick}
      />
    );

    const buttons = screen.getAllByRole('button');
    // First two buttons are usually Prev and Next month based on our component structure
    
    fireEvent.click(buttons[0]);
    expect(mockOnPrevMonth).toHaveBeenCalledTimes(1);

    fireEvent.click(buttons[1]);
    expect(mockOnNextMonth).toHaveBeenCalledTimes(1);
  });

  it('calls onDayClick when a day cell is clicked', () => {
    const currentDate = new Date('2023-10-15T00:00:00Z');
    
    render(
      <MonthCalendar
        currentDate={currentDate}
        onPrevMonth={mockOnPrevMonth}
        onNextMonth={mockOnNextMonth}
        summaries={mockSummaries}
        onDayClick={mockOnDayClick}
      />
    );

    const dayCell = screen.getByText('15').closest('[role="button"]');
    expect(dayCell).toBeInTheDocument();

    if (dayCell) {
      fireEvent.click(dayCell);
      expect(mockOnDayClick).toHaveBeenCalledTimes(1);
    }
  });

  it('renders shift summaries correctly', () => {
    const currentDate = new Date('2023-10-15T00:00:00Z');
    
    render(
      <MonthCalendar
        currentDate={currentDate}
        onPrevMonth={mockOnPrevMonth}
        onNextMonth={mockOnNextMonth}
        summaries={mockSummaries}
        onDayClick={mockOnDayClick}
      />
    );

    expect(screen.getAllByText('S').length).toBeGreaterThan(0);
    expect(screen.getAllByText('C').length).toBeGreaterThan(0);
  });
});
