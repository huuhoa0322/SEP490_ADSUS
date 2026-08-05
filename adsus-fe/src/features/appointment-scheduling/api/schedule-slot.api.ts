import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";

import type {
  CloseSlotImpactResponse,
  CreateScheduleSlotRequest,
  ListSlotsParams,
  ScheduleSlotResponse,
} from "../types/schedule-slot.types";

/**
 * UC-15 — ScheduleSlot API.
 * Roles: Doctor, Nurse.
 */

/**
 * GET /api/v1/schedule-slots — Danh sách slot (filter theo range + doctor + status).
 */
export async function getScheduleSlots(
  params: ListSlotsParams = {},
): Promise<ScheduleSlotResponse[]> {
  const cleanParams: Record<string, string> = {};
  if (params.fromDate) cleanParams.fromDate = params.fromDate;
  if (params.toDate) cleanParams.toDate = params.toDate;
  if (params.doctorId) cleanParams.doctorId = params.doctorId;
  if (params.status) cleanParams.status = params.status;

  const { data } = await apiClient.get<ApiResponse<ScheduleSlotResponse[]>>(
    "/api/v1/schedule-slots",
    { params: cleanParams },
  );

  if (!data.data) {
    throw new Error(data.message || "Không tải được danh sách khung giờ.");
  }

  // Normalize SlotStatus: BE có thể trả số (0/1) hoặc string. Đảm bảo FE nhận "OPEN" / "CLOSED".
  return data.data.map((s) => ({ ...s, status: normalizeStatus(s.status) }));
}

function normalizeStatus(status: unknown): "OPEN" | "CLOSED" {
  if (typeof status === "string") {
    const upper = status.toUpperCase();
    if (upper === "OPEN" || upper === "CLOSED") return upper;
  }
  if (typeof status === "number") {
    return status === 0 ? "OPEN" : "CLOSED";
  }
  return "OPEN";
}

/**
 * GET /api/v1/schedule-slots/{id} — Chi tiết 1 slot.
 */
export async function getScheduleSlot(id: string): Promise<ScheduleSlotResponse> {
  const { data } = await apiClient.get<ApiResponse<ScheduleSlotResponse>>(
    `/api/v1/schedule-slots/${id}`,
  );
  if (!data.data) {
    throw new Error(data.message || "Không tải được khung giờ.");
  }
  return data.data;
}

/**
 * POST /api/v1/schedule-slots — Tạo slot mới.
 */
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
  return data.data;
}

/**
 * PUT /api/v1/schedule-slots/{id}/close?force=... — Đóng slot.
 * Nếu slot có booking và force=false, backend trả 409 với body là CloseSlotImpactResponse.
 */
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
