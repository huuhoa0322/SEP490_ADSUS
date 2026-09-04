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

const getShiftColor = (info?: ShiftInfo) => {
  if (!info) return '';
  switch (info.status) {
    case 'WORKING':
      return 'bg-emerald-100 text-emerald-800 border-emerald-200';
    case 'OFF':
    case 'PAST':
      return 'bg-slate-100 text-slate-500 border-slate-200';
    case 'HAS_BOOKINGS':
      return 'bg-blue-100 text-blue-800 border-blue-200';
    default:
      return 'bg-gray-100 text-gray-800 border-gray-200';
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
        type === 'EVENING' && info.status === 'WORKING' && 'bg-amber-100 text-amber-800 border-amber-200'
      )}
      title={`${info.totalSlots} slots, ${info.bookedSlots} booked, ${info.closedSlots} closed`}
    >
      <span className="font-semibold">{getShiftLabel(type)}</span>
      <span className="flex gap-0.5 items-center">
        {info.status === 'HAS_BOOKINGS' && <span className="w-1.5 h-1.5 rounded-full bg-blue-500" />}
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
    <div className="bg-white rounded-xl border shadow-sm overflow-hidden">
      <div className="flex items-center justify-between px-6 py-4 border-b">
        <h2 className="text-lg font-semibold text-slate-800 capitalize">
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

      <div className="grid grid-cols-7 border-b bg-slate-50">
        {weekDays.map((day) => (
          <div key={day} className="py-2 text-center text-sm font-medium text-slate-500 border-r last:border-0">
            {day}
          </div>
        ))}
      </div>

      <div className="grid grid-cols-7 grid-rows-5 lg:grid-rows-6">
        {days.map((day, idx) => {
          const dateStr = format(day, 'yyyy-MM-dd');
          const summary = summaries.find((s) => s.date === dateStr);
          const isCurrentMonth = isSameMonth(day, monthStart);

          return (
            <div
              key={day.toString()}
              onClick={() => onDayClick(summary, day)}
              className={cn(
                'min-h-[100px] border-r border-b p-2 transition-colors cursor-pointer hover:bg-slate-50',
                !isCurrentMonth && 'bg-slate-50 opacity-50',
                isToday(day) && 'bg-blue-50/30'
              )}
            >
              <div className="flex justify-between items-center mb-1">
                <span
                  className={cn(
                    'text-sm font-medium',
                    isToday(day) ? 'bg-blue-600 text-white w-6 h-6 rounded-full flex items-center justify-center' : 'text-slate-700'
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
