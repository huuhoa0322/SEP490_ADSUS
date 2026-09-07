import { apiClient } from '@/lib/api-client';
import { ApiResponse, PagedResult } from '@/types/api.types';
import {
  CreateShiftRequestDto,
  ReviewShiftRequestDto,
  ShiftRequestResponse,
  DayShiftSummary,
  ShiftRequestStatus,
} from '../types/shift-request.types';

export const shiftRequestApi = {
  // Doctor APIs
  createRequest: async (payload: CreateShiftRequestDto): Promise<ShiftRequestResponse> => {
    const { data } = await apiClient.post<ApiResponse<ShiftRequestResponse>>('/api/v1/shift-requests', payload);
    if (!data.data) {
      throw new Error(data.message || 'Không gửi được yêu cầu.');
    }
    return data.data;
  },

  getMyRequests: async (params: { status?: ShiftRequestStatus; page?: number; pageSize?: number }): Promise<PagedResult<ShiftRequestResponse>> => {
    const { data } = await apiClient.get<ApiResponse<PagedResult<ShiftRequestResponse>>>('/api/v1/shift-requests/my', { params });
    if (!data.data) {
      throw new Error(data.message || 'Không tải được danh sách yêu cầu.');
    }
    return data.data;
  },

  getMonthSummary: async (year: number, month: number): Promise<DayShiftSummary[]> => {
    const { data } = await apiClient.get<ApiResponse<DayShiftSummary[]>>('/api/v1/shift-requests/month-summary', { params: { year, month } });
    if (!data.data) {
      throw new Error(data.message || 'Không tải được dữ liệu ca làm việc.');
    }
    return data.data;
  },

  // Admin APIs
  getAllRequests: async (params: { status?: ShiftRequestStatus; doctorId?: string; page?: number; pageSize?: number }): Promise<PagedResult<ShiftRequestResponse>> => {
    const { data } = await apiClient.get<ApiResponse<PagedResult<ShiftRequestResponse>>>('/api/v1/admin/shift-requests', { params });
    if (!data.data) {
      throw new Error(data.message || 'Không tải được danh sách yêu cầu.');
    }
    return data.data;
  },

  reviewRequest: async (requestId: string, payload: ReviewShiftRequestDto): Promise<ShiftRequestResponse> => {
    const { data } = await apiClient.put<ApiResponse<ShiftRequestResponse>>(`/api/v1/admin/shift-requests/${requestId}/review`, payload);
    if (!data.data) {
      throw new Error(data.message || 'Không xử lý được yêu cầu.');
    }
    return data.data;
  },
};
