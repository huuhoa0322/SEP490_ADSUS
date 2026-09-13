import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";

import type {
  AppointmentResponse,
  AppointmentSummaryResponse,
  BookAppointmentRequest,
  OpenSlotResponse,
} from "../types/booking.types";

/** GET /api/v1/appointments/slots — Danh sách slot còn trống (UC-13). */
export async function getOpenSlots(params?: {
  doctorId?: string;
  fromDate?: string; // YYYY-MM-DD
  toDate?: string; // YYYY-MM-DD
}): Promise<OpenSlotResponse[]> {
  const { data } = await apiClient.get<ApiResponse<OpenSlotResponse[]>>(
    "/api/v1/appointments/slots",
    { params },
  );
  if (!data.data) throw new Error(data.message || "Không tải được danh sách khung giờ trống.");
  return data.data;
}

/** GET /api/v1/appointments — Danh sách lịch hẹn của tôi (UC-14). */
export async function getMyAppointments(params?: {
  status?: string;
}): Promise<AppointmentSummaryResponse[]> {
  const { data } = await apiClient.get<ApiResponse<AppointmentSummaryResponse[]>>(
    "/api/v1/appointments",
    { params },
  );
  if (!data.data) throw new Error(data.message || "Không tải được lịch hẹn.");
  return data.data;
}

/** POST /api/v1/appointments — Đặt lịch hẹn mới (UC-13). */
export async function bookAppointment(
  request: BookAppointmentRequest,
): Promise<AppointmentResponse> {
  const { data } = await apiClient.post<ApiResponse<AppointmentResponse>>(
    "/api/v1/appointments",
    request,
  );
  if (!data.data) throw new Error(data.message || "Không thể đặt lịch hẹn.");
  return data.data;
}

/** POST /api/v1/appointments/{id}/cancel — Hủy lịch hẹn (UC-14). */
export async function cancelAppointment(
  appointmentId: string,
  cancellationReason: string,
): Promise<AppointmentResponse> {
  const { data } = await apiClient.post<ApiResponse<AppointmentResponse>>(
    `/api/v1/appointments/${appointmentId}/cancel`,
    { cancellationReason },
  );
  if (!data.data) throw new Error(data.message || "Không thể hủy lịch hẹn.");
  return data.data;
}
