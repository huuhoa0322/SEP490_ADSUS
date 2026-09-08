import { format, addDays, startOfDay } from 'date-fns';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { DayShiftSummary, ShiftInfo } from '../types/shift-request.types';
import { Badge } from '@/components/ui/badge';
import { Clock, Users, Ban } from 'lucide-react';

interface DayShiftDetailProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  date: Date | null;
  summary?: DayShiftSummary;
  onRequestClick?: (type: 'LEAVE' | 'OVERTIME', date: Date) => void;
}

const ShiftDetailBlock = ({ info, title }: { info?: ShiftInfo; title: string }) => {
  if (!info) return null;

  const getStatusBadge = (status: ShiftInfo['status']) => {
    switch (status) {
      case 'WORKING': return <Badge className="bg-accent/12 text-accent hover:bg-accent/12 border-none">Đang làm việc</Badge>;
      case 'OFF': return <Badge className="bg-muted text-muted-foreground hover:bg-muted border-none">Nghỉ</Badge>;
      case 'HAS_BOOKINGS': return <Badge className="bg-[var(--chart-3)]/12 text-[var(--chart-3)] hover:bg-[var(--chart-3)]/12 border-none">Có lịch hẹn</Badge>;
      case 'PAST': return <Badge className="bg-muted text-muted-foreground/70 hover:bg-muted border-none">Đã qua</Badge>;
    }
  };

  return (
    <div className="bg-muted/50 rounded-lg p-4 border border-border space-y-3">
      <div className="flex items-center justify-between">
        <h4 className="font-heading font-600 text-foreground">{title}</h4>
        {getStatusBadge(info.status)}
      </div>

      <div className="grid grid-cols-3 gap-2 mt-2 text-sm">
        <div className="flex flex-col items-center gap-1 p-2 bg-background rounded border border-border">
          <span className="flex size-7 items-center justify-center rounded-full bg-muted text-muted-foreground">
            <Clock className="h-3.5 w-3.5" />
          </span>
          <span className="font-semibold text-foreground tabular-nums">{info.totalSlots}</span>
          <span className="text-xs text-muted-foreground">Tổng slot</span>
        </div>
        <div className="flex flex-col items-center gap-1 p-2 bg-background rounded border border-[var(--chart-3)]/20">
          <span className="flex size-7 items-center justify-center rounded-full bg-[var(--chart-3)]/12 text-[var(--chart-3)]">
            <Users className="h-3.5 w-3.5" />
          </span>
          <span className="font-semibold text-[var(--chart-3)] tabular-nums">{info.bookedSlots}</span>
          <span className="text-xs text-muted-foreground">Đã đặt</span>
        </div>
        <div className="flex flex-col items-center gap-1 p-2 bg-background rounded border border-border">
          <span className="flex size-7 items-center justify-center rounded-full bg-muted text-muted-foreground">
            <Ban className="h-3.5 w-3.5" />
          </span>
          <span className="font-semibold text-foreground tabular-nums">{info.closedSlots}</span>
          <span className="text-xs text-muted-foreground">Đã đóng</span>
        </div>
      </div>

      {info.pendingRequestType && (
        <div className="flex items-center gap-2 rounded border border-[var(--status-warning)]/25 bg-[var(--status-warning)]/8 px-3 py-2 text-xs font-medium text-[var(--status-warning)]">
          <span className="relative flex size-1.5 shrink-0">
            <span className="absolute inline-flex size-full animate-ping rounded-full bg-[var(--status-warning)] opacity-75" />
            <span className="relative inline-flex size-1.5 rounded-full bg-[var(--status-warning)]" />
          </span>
          Đang chờ duyệt yêu cầu {info.pendingRequestType?.toUpperCase() === 'LEAVE' ? 'Xin nghỉ' : 'Tăng ca'}
        </div>
      )}
    </div>
  );
};

export function DayShiftDetail({ open, onOpenChange, date, summary, onRequestClick }: DayShiftDetailProps) {
  if (!date) return null;

  const minDate = startOfDay(addDays(new Date(), 2));

  const handleRequestClick = (type: 'LEAVE' | 'OVERTIME') => {
    if (onRequestClick) {
      onRequestClick(type, date);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[450px]">
        <DialogHeader>
          <DialogTitle className="text-xl">
            Chi tiết ngày {format(date, 'dd/MM/yyyy')}
          </DialogTitle>
        </DialogHeader>
        
        <div className="space-y-4 pt-4">
          {!summary ? (
            <div className="text-center py-8 text-muted-foreground">
              Không có dữ liệu ca làm việc cho ngày này
            </div>
          ) : (
            <>
              <ShiftDetailBlock info={summary.morning} title="Ca Sáng (08:00 - 12:00)" />
              <ShiftDetailBlock info={summary.afternoon} title="Ca Chiều (13:00 - 17:00)" />
              {summary.evening && (
                <ShiftDetailBlock info={summary.evening} title="Ca Tối (17:00 - 20:00)" />
              )}
            </>
          )}
        </div>
        
        {date >= minDate && onRequestClick && (
          <div className="flex gap-3 justify-end mt-4 pt-4 border-t">
            <Button variant="outline" onClick={() => handleRequestClick('LEAVE')}>
              Xin nghỉ phép
            </Button>
            <Button onClick={() => handleRequestClick('OVERTIME')}>
              Đăng ký tăng ca
            </Button>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
