import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";

import type {
  AppointmentSummary,
  CreateScheduleSlotRequest,
  PagedResult,
  ScheduleSlot,
  ScheduleSlotSearch,
  UpdateScheduleSlotStatusRequest,
} from "../types/schedule-slot.types";

export async function searchScheduleSlots(
  q: ScheduleSlotSearch,
): Promise<PagedResult<ScheduleSlot>> {
  const { data } = await apiClient.get<ApiResponse<PagedResult<ScheduleSlot>>>(
    "/api/v1/schedule-slots",
    { params: { ...q, status: q.status } },
  );
  if (!data.data) throw new Error(data.message || "Không tải được danh sách khung giờ.");
  return data.data;
}

export async function getScheduleSlot(id: string): Promise<ScheduleSlot> {
  const { data } = await apiClient.get<ApiResponse<ScheduleSlot>>(
    `/api/v1/schedule-slots/${id}`,
  );
  if (!data.data) throw new Error(data.message || "Không tìm thấy khung giờ.");
  return data.data;
}

export async function createScheduleSlot(
  payload: CreateScheduleSlotRequest,
): Promise<ScheduleSlot> {
  const { data } = await apiClient.post<ApiResponse<ScheduleSlot>>(
    "/api/v1/schedule-slots",
    payload,
  );
  if (!data.data) throw new Error(data.message || "Tạo khung giờ thất bại.");
  return data.data;
}

export async function closeScheduleSlot(id: string): Promise<ScheduleSlot> {
  const { data } = await apiClient.patch<ApiResponse<ScheduleSlot>>(
    `/api/v1/schedule-slots/${id}`,
    { status: "CLOSED" } satisfies UpdateScheduleSlotStatusRequest,
  );
  if (!data.data) throw new Error(data.message || "Đóng khung giờ thất bại.");
  return data.data;
}

export async function listAppointmentsBySlot(
  slotId: string,
  page = 1,
  pageSize = 20,
): Promise<PagedResult<AppointmentSummary>> {
  const { data } = await apiClient.get<ApiResponse<PagedResult<AppointmentSummary>>>(
    `/api/v1/schedule-slots/${slotId}/appointments`,
    { params: { page, pageSize } },
  );
  if (!data.data) throw new Error(data.message || "Không tải được danh sách bệnh nhân.");
  return data.data;
}