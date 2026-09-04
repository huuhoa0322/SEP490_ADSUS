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
      case 'WORKING': return <Badge className="bg-emerald-100 text-emerald-800 hover:bg-emerald-100 border-none">Đang làm việc</Badge>;
      case 'OFF': return <Badge className="bg-slate-100 text-slate-800 hover:bg-slate-100 border-none">Nghỉ</Badge>;
      case 'HAS_BOOKINGS': return <Badge className="bg-blue-100 text-blue-800 hover:bg-blue-100 border-none">Có lịch hẹn</Badge>;
      case 'PAST': return <Badge className="bg-gray-100 text-gray-500 hover:bg-gray-100 border-none">Đã qua</Badge>;
    }
  };

  return (
    <div className="bg-slate-50 rounded-lg p-4 border space-y-3">
      <div className="flex items-center justify-between">
        <h4 className="font-semibold text-slate-700">{title}</h4>
        {getStatusBadge(info.status)}
      </div>

      <div className="grid grid-cols-3 gap-2 mt-2 text-sm text-slate-600">
        <div className="flex flex-col items-center p-2 bg-white rounded border">
          <Clock className="h-4 w-4 text-slate-400 mb-1" />
          <span className="font-medium text-slate-700">{info.totalSlots}</span>
          <span className="text-xs">Tổng slot</span>
        </div>
        <div className="flex flex-col items-center p-2 bg-white rounded border border-blue-100">
          <Users className="h-4 w-4 text-blue-400 mb-1" />
          <span className="font-medium text-blue-700">{info.bookedSlots}</span>
          <span className="text-xs">Đã đặt</span>
        </div>
        <div className="flex flex-col items-center p-2 bg-white rounded border border-slate-200">
          <Ban className="h-4 w-4 text-slate-400 mb-1" />
          <span className="font-medium text-slate-700">{info.closedSlots}</span>
          <span className="text-xs">Đã đóng</span>
        </div>
      </div>

      {info.pendingRequestType && (
        <div className="text-xs font-medium text-amber-600 bg-amber-50 px-3 py-2 rounded border border-amber-100 flex items-center gap-2">
          <span className="w-1.5 h-1.5 rounded-full bg-amber-500 animate-pulse" />
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
            <div className="text-center py-8 text-slate-500">
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
