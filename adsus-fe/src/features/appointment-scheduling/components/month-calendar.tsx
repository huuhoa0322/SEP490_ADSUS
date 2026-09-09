import {
  startOfMonth,
  endOfMonth,
  eachDayOfInterval,
  format,
  isSameMonth,
  isToday,
  startOfWeek,
  endOfWeek,
} from 'date-fns';
import { vi } from 'date-fns/locale';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { DayShiftSummary, ShiftInfo } from '../types/shift-request.types';
import { cn } from '@/lib/utils';

interface MonthCalendarProps {
  currentDate: Date;
  onPrevMonth: () => void;
  onNextMonth: () => void;
  summaries: DayShiftSummary[];
  onDayClick: (day: DayShiftSummary | undefined, date: Date) => void;
}

// Trạng thái ca làm việc dùng đúng bộ token của app (không phải màu Tailwind rời rạc):
// WORKING = teal accent (đang làm), HAS_BOOKINGS = xanh dương chart-3 (có lịch hẹn),
// OFF/PAST = trung tính. Nền tô 12% để chữ trạng thái vẫn đọc rõ trên ô lịch nhỏ.
const getShiftColor = (info?: ShiftInfo) => {
  if (!info) return '';
  switch (info.status) {
    case 'WORKING':
      return 'bg-[var(--success)]/12 text-[var(--success)] border-[var(--success)]/25';
    case 'OFF':
    case 'PAST':
      return 'bg-muted text-muted-foreground border-border';
    case 'HAS_BOOKINGS':
      return 'bg-[var(--chart-3)]/12 text-[var(--chart-3)] border-[var(--chart-3)]/25';
    default:
      return 'bg-muted text-muted-foreground border-border';
  }
};

const getShiftLabel = (type: 'MORNING' | 'AFTERNOON' | 'EVENING') => {
  switch (type) {
    case 'MORNING': return 'S';
    case 'AFTERNOON': return 'C';
    case 'EVENING': return 'T';
  }
};

const ShiftBlock = ({ info, type }: { info?: ShiftInfo; type: 'MORNING' | 'AFTERNOON' | 'EVENING' }) => {
  if (!info) return null;
  const isPending = info.pendingRequestType === 'LEAVE' || info.pendingRequestType === 'OVERTIME';

  return (
    <div
      className={cn(
        'text-[10px] px-1 rounded-sm border flex items-center justify-between mb-0.5',
        getShiftColor(info),
        // Ca tối đang làm = tăng ca — mượn màu status-warning (amber) đã dùng cho mọi
        // trạng thái "cần chú ý" khác trong app, thay vì một amber rời rạc riêng ở đây.
        type === 'EVENING' && info.status === 'WORKING' && 'bg-[var(--status-warning)]/12 text-[var(--status-warning)] border-[var(--status-warning)]/25'
      )}
      title={`${info.totalSlots} slot, ${info.bookedSlots} đã đặt, ${info.closedSlots} đã đóng`}
    >
      <span className="font-semibold">{getShiftLabel(type)}</span>
      <span className="flex gap-0.5 items-center">
        {info.status === 'HAS_BOOKINGS' && <span className="font-medium">{info.bookedSlots}/{info.totalSlots}</span>}
        {info.status === 'OFF' && '✖'}
        {info.status === 'WORKING' && '■'}
        {isPending && <span className="ml-1 opacity-70">(...)</span>}
      </span>
    </div>
  );
};

export function MonthCalendar({
  currentDate,
  onPrevMonth,
  onNextMonth,
  summaries,
  onDayClick,
}: MonthCalendarProps) {
  const monthStart = startOfMonth(currentDate);
  const monthEnd = endOfMonth(monthStart);
  
  // Lấy đầu tuần của ngày đầu tháng (thứ 2) và cuối tuần của ngày cuối tháng (CN)
  const startDate = startOfWeek(monthStart, { weekStartsOn: 1 });
  const endDate = endOfWeek(monthEnd, { weekStartsOn: 1 });

  const dateFormat = 'd';
  const days = eachDayOfInterval({
    start: startDate,
    end: endDate,
  });

  const weekDays = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];

  return (
    <div className="bg-background rounded-xl border border-border shadow-sm overflow-hidden">
      <div className="flex items-center justify-between px-6 py-4 border-b border-border">
        <h2 className="font-heading text-lg font-bold text-foreground capitalize tracking-[-0.01em]">
          {format(currentDate, 'MMMM yyyy', { locale: vi })}
        </h2>
        <div className="flex space-x-2">
          <Button variant="outline" size="icon" onClick={onPrevMonth}>
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <Button variant="outline" size="icon" onClick={onNextMonth}>
            <ChevronRight className="h-4 w-4" />
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-7 border-b border-border bg-muted">
        {weekDays.map((day) => (
          <div key={day} className="py-2 text-center text-sm font-medium text-muted-foreground border-r border-border last:border-0">
            {day}
          </div>
        ))}
      </div>

      <div className="grid grid-cols-7 grid-rows-5 lg:grid-rows-6">
        {days.map((day) => {
          const dateStr = format(day, 'yyyy-MM-dd');
          const summary = summaries.find((s) => { if (!s || !s.date) return false; const sDate = typeof s.date === 'string' ? s.date.split('T')[0] : s.date; return sDate === dateStr; });
          const isCurrentMonth = isSameMonth(day, monthStart);

          return (
            <div
              key={day.toString()}
              role="button"
              tabIndex={0}
              onClick={() => onDayClick(summary, day)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                  e.preventDefault();
                  onDayClick(summary, day);
                }
              }}
              className={cn(
                'min-h-[100px] border-r border-b border-border p-2 transition-colors cursor-pointer hover:bg-muted/60',
                !isCurrentMonth && 'bg-muted/40 opacity-50',
                isToday(day) && 'bg-[var(--success)]/8'
              )}
            >
              <div className="flex justify-between items-center mb-1">
                <span
                  className={cn(
                    'text-sm font-medium',
                    isToday(day) ? 'bg-primary text-primary-foreground w-6 h-6 rounded-full flex items-center justify-center' : 'text-foreground'
                  )}
                >
                  {format(day, dateFormat)}
                </span>
              </div>
              
              <div className="flex flex-col space-y-0.5 mt-2">
                {summary && (
                  <>
                    <ShiftBlock info={summary.morning} type="MORNING" />
                    <ShiftBlock info={summary.afternoon} type="AFTERNOON" />
                    {summary.evening && <ShiftBlock info={summary.evening} type="EVENING" />}
                  </>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
