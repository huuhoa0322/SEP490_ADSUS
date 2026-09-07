import { useEffect, useMemo } from 'react';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { format, addDays, startOfDay } from 'date-fns';
import { CalendarIcon } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Calendar } from '@/components/ui/calendar';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { cn } from '@/lib/utils';
import { useCreateShiftRequest } from '../hooks/use-shift-request';
import { ShiftRequestType, ShiftType } from '../types/shift-request.types';

const shiftRequestSchema = z
  .object({
    requestType: z.enum(['LEAVE', 'OVERTIME']),
    requestDate: z.date({
      required_error: 'Vui lòng chọn ngày',
    }),
    shiftType: z.enum(['MORNING', 'AFTERNOON', 'EVENING', 'FULL_DAY']),
    reason: z
      .string()
      .min(5, 'Lý do quá ngắn')
      .max(500, 'Lý do không được vượt quá 500 ký tự'),
  })
  .refine(
    (data) => {
      if (data.requestType === 'OVERTIME' && data.shiftType !== 'EVENING') {
        return false;
      }
      return true;
    },
    {
      message: 'Ca tăng ca chỉ được chọn Ca Tối',
      path: ['shiftType'],
    }
  )
  .refine(
    (data) => {
      if (data.requestType === 'LEAVE' && data.shiftType === 'EVENING') {
        return false;
      }
      return true;
    },
    {
      message: 'Không thể xin nghỉ Ca Tối (đây là ca tăng thêm)',
      path: ['shiftType'],
    }
  )
  .refine(
    (data) => {
      if (data.requestType === 'OVERTIME' && data.shiftType === 'FULL_DAY') {
        return false;
      }
      return true;
    },
    {
      message: 'Không thể chọn Cả ngày cho yêu cầu tăng ca',
      path: ['shiftType'],
    }
  );

type ShiftRequestFormValues = z.infer<typeof shiftRequestSchema>;

interface ShiftRequestFormProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  defaultDate?: Date;
  defaultRequestType?: 'LEAVE' | 'OVERTIME';
}

export function ShiftRequestForm({
  open,
  onOpenChange,
  defaultDate,
  defaultRequestType = 'LEAVE',
}: ShiftRequestFormProps) {
  const { mutateAsync: createRequest, isPending } = useCreateShiftRequest();

  // Yêu cầu nghỉ/tăng ca phải báo trước 2 ngày
  const minDate = useMemo(() => startOfDay(addDays(new Date(), 2)), []);

  const {
    control,
    handleSubmit,
    watch,
    reset,
    formState: { errors },
  } = useForm<ShiftRequestFormValues>({
    resolver: zodResolver(shiftRequestSchema),
    defaultValues: {
      requestType: defaultRequestType,
      requestDate: defaultDate && defaultDate >= minDate ? defaultDate : undefined,
      shiftType: defaultRequestType === 'OVERTIME' ? 'EVENING' : 'MORNING',
      reason: '',
    },
  });

  useEffect(() => {
    if (open) {
      reset({
        requestType: defaultRequestType,
        requestDate: defaultDate && defaultDate >= minDate ? defaultDate : undefined,
        shiftType: defaultRequestType === 'OVERTIME' ? 'EVENING' : 'MORNING',
        reason: '',
      });
    }
  }, [open, defaultDate, defaultRequestType, minDate, reset]);

  const requestType = watch('requestType');

  const onSubmit = async (data: ShiftRequestFormValues) => {
    try {
      await createRequest({
        requestType: data.requestType,
        requestDate: format(data.requestDate, 'yyyy-MM-dd'),
        shiftType: data.shiftType,
        reason: data.reason,
      });
      reset();
      onOpenChange(false);
    } catch (error) {
      // Error is handled by hook
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[425px]">
        <DialogHeader>
          <DialogTitle>Gửi yêu cầu Nghỉ / Tăng ca</DialogTitle>
          <DialogDescription>
            Điền thông tin bên dưới để gửi yêu cầu cho quản trị viên phê duyệt.
            Vui lòng gửi trước ít nhất 2 ngày.
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4 pt-4">
          <div className="space-y-2">
            <Label>Loại yêu cầu</Label>
            <Controller
              name="requestType"
              control={control}
              render={({ field }) => (
                <Select
                  onValueChange={field.onChange}
                  value={field.value}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Chọn loại" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="LEAVE">Xin nghỉ phép</SelectItem>
                    <SelectItem value="OVERTIME">Đăng ký tăng ca</SelectItem>
                  </SelectContent>
                </Select>
              )}
            />
            {errors.requestType && (
              <p className="text-sm text-red-500">
                {errors.requestType.message}
              </p>
            )}
          </div>

          <div className="space-y-2 flex flex-col">
            <Label>Ngày</Label>
            <Controller
              name="requestDate"
              control={control}
              render={({ field }) => (
                <Popover>
                  <PopoverTrigger asChild>
                    <Button
                      variant={'outline'}
                      className={cn(
                        'w-full justify-start text-left font-normal',
                        !field.value && 'text-muted-foreground'
                      )}
                    >
                      <CalendarIcon className="mr-2 h-4 w-4" />
                      {field.value ? (
                        format(field.value, 'dd/MM/yyyy')
                      ) : (
                        <span>Chọn ngày</span>
                      )}
                    </Button>
                  </PopoverTrigger>
                  <PopoverContent className="w-auto p-0" align="start">
                    <Calendar
                      mode="single"
                      selected={field.value}
                      onSelect={field.onChange}
                      disabled={(date) => date < minDate}
                    />
                  </PopoverContent>
                </Popover>
              )}
            />
            {errors.requestDate && (
              <p className="text-sm text-red-500">
                {errors.requestDate.message}
              </p>
            )}
          </div>

          <div className="space-y-2">
            <Label>Ca áp dụng</Label>
            <Controller
              name="shiftType"
              control={control}
              render={({ field }) => (
                <Select
                  onValueChange={field.onChange}
                  value={field.value}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Chọn ca" />
                  </SelectTrigger>
                  <SelectContent>
                    {requestType === 'LEAVE' && (
                      <>
                        <SelectItem value="MORNING">Ca Sáng (08:00 - 12:00)</SelectItem>
                        <SelectItem value="AFTERNOON">Ca Chiều (13:00 - 17:00)</SelectItem>
                        <SelectItem value="FULL_DAY">Cả ngày (08:00 - 17:00)</SelectItem>
                      </>
                    )}
                    {requestType === 'OVERTIME' && (
                      <SelectItem value="EVENING">Ca Tối (17:00 - 20:00)</SelectItem>
                    )}
                  </SelectContent>
                </Select>
              )}
            />
            {errors.shiftType && (
              <p className="text-sm text-red-500">{errors.shiftType.message}</p>
            )}
          </div>

          <div className="space-y-2">
            <Label>Lý do</Label>
            <Controller
              name="reason"
              control={control}
              render={({ field }) => (
                <Textarea
                  {...field}
                  placeholder="Nhập lý do chi tiết..."
                  rows={3}
                />
              )}
            />
            {errors.reason && (
              <p className="text-sm text-red-500">{errors.reason.message}</p>
            )}
          </div>

          <div className="flex justify-end gap-3 pt-4">
            <Button
              type="button"
              variant="outline"
              onClick={() => onOpenChange(false)}
            >
              Hủy
            </Button>
            <Button type="submit" disabled={isPending}>
              {isPending ? 'Đang gửi...' : 'Gửi yêu cầu'}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
