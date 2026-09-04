import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { shiftRequestApi } from '../api/shift-request.api';
import { CreateShiftRequestDto, ReviewShiftRequestDto, ShiftRequestStatus } from '../types/shift-request.types';
import { getApiErrorMessage } from '@/lib/api-client';
import toast from 'react-hot-toast';

export const shiftRequestKeys = {
  all: ['shift-requests'] as const,
  my: (status?: ShiftRequestStatus, page?: number) => ['shift-requests', 'my', status, page] as const,
  monthSummary: (year: number, month: number) => ['shift-requests', 'month', year, month] as const,
  adminAll: (status?: ShiftRequestStatus, doctorId?: string, page?: number) =>
    ['shift-requests', 'admin', status, doctorId, page] as const,
};

// --- DOCTOR HOOKS ---

export const useCreateShiftRequest = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateShiftRequestDto) => shiftRequestApi.createRequest(data),
    onSuccess: () => {
      toast.success('Gửi yêu cầu thành công!');
      queryClient.invalidateQueries({ queryKey: shiftRequestKeys.all });
    },
    onError: (error: unknown) => {
      toast.error(getApiErrorMessage(error, 'Lỗi khi gửi yêu cầu'));
    },
  });
};

export const useMyShiftRequests = (status?: ShiftRequestStatus, page: number = 1, pageSize: number = 20) => {
  return useQuery({
    queryKey: shiftRequestKeys.my(status, page),
    queryFn: () => shiftRequestApi.getMyRequests({ status, page, pageSize }),
  });
};

export const useMonthSummary = (year: number, month: number) => {
  return useQuery({
    queryKey: shiftRequestKeys.monthSummary(year, month),
    queryFn: () => shiftRequestApi.getMonthSummary(year, month),
  });
};

// --- ADMIN HOOKS ---

export const useAdminShiftRequests = (
  status?: ShiftRequestStatus,
  doctorId?: string,
  page: number = 1,
  pageSize: number = 20
) => {
  return useQuery({
    queryKey: shiftRequestKeys.adminAll(status, doctorId, page),
    queryFn: () => shiftRequestApi.getAllRequests({ status, doctorId, page, pageSize }),
  });
};

export const useReviewShiftRequest = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ requestId, data }: { requestId: string; data: ReviewShiftRequestDto }) =>
      shiftRequestApi.reviewRequest(requestId, data),
    onSuccess: () => {
      toast.success('Duyệt yêu cầu thành công!');
      queryClient.invalidateQueries({ queryKey: shiftRequestKeys.all });
    },
    onError: (error: unknown) => {
      toast.error(getApiErrorMessage(error, 'Lỗi khi duyệt yêu cầu'));
    },
  });
};
