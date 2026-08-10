import { apiClient } from "@/lib/api-client";
import type { ApiResponse, PagedResult } from "@/types/api.types";

import type {
  CloseSlotImpactResponse,
  CreateScheduleSlotRequest,
  ListSlotsParams,
  ScheduleSlotResponse,
  UpdateScheduleSlotRequest,
} from "../types/schedule-slot.types";

/**
 * UC-15 — ScheduleSlot API.
 * Roles: Doctor only. Doctor tự quản lý lịch của chính mình (DoctorId lấy từ JWT).
 */

function normalizeStatus(status: unknown): "OPEN" | "BOOKED" | "CLOSED" {
  if (typeof status === "string") {
    const upper = status.toUpperCase();
    if (upper === "OPEN" || upper === "BOOKED" || upper === "CLOSED") return upper as "OPEN" | "BOOKED" | "CLOSED";
  }
  if (typeof status === "number") {
    // SlotStatus enum: OPEN=0, BOOKED=1, CLOSED=2
    if (status === 0) return "OPEN";
    if (status === 1) return "BOOKED";
    if (status === 2) return "CLOSED";
  }
  return "OPEN";
}

/** GET /api/v1/schedule-slots — Danh sách slot của Doctor đang đăng nhập. */
export async function getScheduleSlots(
  params: ListSlotsParams = {},
): Promise<ScheduleSlotResponse[]> {
  const cleanParams: Record<string, string> = {};
  if (params.fromDate) cleanParams.fromDate = params.fromDate;
  if (params.toDate) cleanParams.toDate = params.toDate;
  if (params.status) cleanParams.status = params.status;
  if (params.pageSize) cleanParams.pageSize = String(params.pageSize);

  // Backend trả về paginated format: { items: [], page, pageSize, totalItems, totalPages }
  const { data } = await apiClient.get<ApiResponse<PagedResult<ScheduleSlotResponse>>>(
    "/api/v1/schedule-slots",
    { params: cleanParams },
  );

  if (!data.data?.items) {
    throw new Error(data.message || "Không tải được danh sách khung giờ.");
  }
  return data.data.items.map((s) => ({ ...s, status: normalizeStatus(s.status) }));
}

/** GET /api/v1/schedule-slots/{id} */
export async function getScheduleSlot(id: string): Promise<ScheduleSlotResponse> {
  const { data } = await apiClient.get<ApiResponse<ScheduleSlotResponse>>(
    `/api/v1/schedule-slots/${id}`,
  );
  if (!data.data) {
    throw new Error(data.message || "Không tải được khung giờ.");
  }
  return { ...data.data, status: normalizeStatus(data.data.status) };
}

/** POST /api/v1/schedule-slots — Tạo slot cho chính mình. */
export async function createScheduleSlot(
  payload: CreateScheduleSlotRequest,
): Promise<ScheduleSlotResponse> {
  const { data } = await apiClient.post<ApiResponse<ScheduleSlotResponse>>(
    "/api/v1/schedule-slots",
    payload,
  );
  if (!data.data) {
    throw new Error(data.message || "Không tạo được khung giờ.");
  }
  return { ...data.data, status: normalizeStatus(data.data.status) };
}

/** PUT /api/v1/schedule-slots/{id} — Sửa giờ slot (tách ca). */
export async function updateScheduleSlot(
  id: string,
  payload: UpdateScheduleSlotRequest,
): Promise<ScheduleSlotResponse> {
  const { data } = await apiClient.put<ApiResponse<ScheduleSlotResponse>>(
    `/api/v1/schedule-slots/${id}`,
    payload,
  );
  if (!data.data) {
    throw new Error(data.message || "Không cập nhật được khung giờ.");
  }
  return { ...data.data, status: normalizeStatus(data.data.status) };
}

/** PUT /api/v1/schedule-slots/{id}/close?force=... — Đóng slot. */
export async function closeScheduleSlot(
  id: string,
  force = false,
): Promise<CloseSlotImpactResponse> {
  const { data } = await apiClient.put<ApiResponse<CloseSlotImpactResponse>>(
    `/api/v1/schedule-slots/${id}/close`,
    null,
    { params: { force } },
  );
  if (!data.data) {
    throw new Error(data.message || "Không đóng được khung giờ.");
  }
  return data.data;
}

/** POST /api/v1/schedule-slots/ensure-default?weekStart=YYYY-MM-DD — Tự sinh ca mặc định tuần. */
export async function ensureDefaultSlots(weekStart: string): Promise<void> {
  await apiClient.post<ApiResponse<unknown>>(
    "/api/v1/schedule-slots/ensure-default",
    null,
    { params: { weekStart } },
  );
}

/** PUT /api/v1/schedule-slots/{id}/reopen — Mở lại slot đã đóng. */
export async function reopenScheduleSlot(id: string): Promise<ScheduleSlotResponse> {
  const { data } = await apiClient.put<ApiResponse<ScheduleSlotResponse>>(
    `/api/v1/schedule-slots/${id}/reopen`,
  );
  if (!data.data) {
    throw new Error(data.message || "Không mở lại được khung giờ.");
  }
  return { ...data.data, status: normalizeStatus(data.data.status) };
}
