import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";
import type {
  CheckinQueueParams,
  CheckinQueueResponse,
  RescheduleRequest,
  AvailableSlot,
  DoctorSummary,
} from "../types/checkin.types";

const BASE = "/api/v1/appointments";

// Empty GUID constant
const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";

export async function getCheckinQueue(
  params?: string | CheckinQueueParams
): Promise<CheckinQueueResponse> {
  let queryParams: Record<string, unknown> = {};

  if (typeof params === "string") {
    queryParams = { date: params || undefined };
  } else if (params) {
    queryParams = {
      fromDate: params.fromDate || undefined,
      toDate: params.toDate || undefined,
      date: params.date || undefined,
      status: params.status || undefined,
      search: params.search || undefined,
      page: params.page,
      pageSize: params.pageSize,
    };
  }

  const { data } = await apiClient.get<ApiResponse<CheckinQueueResponse>>(
    `${BASE}/checkin-queue`,
    {
      params: queryParams,
    }
  );

  if (!data.data) {
    throw new Error(data.message || "Failed to fetch check-in queue");
  }

  return data.data;
}

/**
 * Check-in appointment dựa trên caseId hoặc appointmentId.
 * - Nếu caseId là empty GUID → gọi endpoint appointmentId (appointment không có case)
 * - Nếu caseId là GUID hợp lệ → gọi endpoint caseId (appointment có case)
 */
export async function checkinAppointment(
  appointmentId: string,
  caseId: string
): Promise<ApiResponse<null>> {
  let url: string;
  let id: string;

  if (caseId === EMPTY_GUID) {
    // Appointment không có case → dùng appointmentId endpoint
    url = `/api/v1/cases/appointment/${appointmentId}/checkin`;
    id = appointmentId;
  } else {
    // Appointment có case → dùng caseId endpoint
    url = `/api/v1/cases/${caseId}/appointment/checkin`;
    id = caseId;
  }

  const { data } = await apiClient.post<ApiResponse<null>>(url);

  if (data.code !== 200 && data.code !== 201) {
    throw new Error(data.message || `Check-in failed for ${id}`);
  }

  return data;
}

// Keep for backward compatibility
export async function checkinByCaseId(caseId: string): Promise<void> {
  const { data } = await apiClient.post<ApiResponse<null>>(
    `/api/v1/cases/${caseId}/appointment/checkin`
  );

  if (data.code !== 200 && data.code !== 201) {
    throw new Error(data.message || "Check-in failed");
  }
}

/**
 * Đổi lịch hẹn / tái đặt lịch hẹn cho Nurse
 * Endpoint: POST /api/v1/appointments/{appointmentId}/reschedule
 */
export async function rescheduleAppointment(
  appointmentId: string,
  request: RescheduleRequest
): Promise<ApiResponse<unknown>> {
  const { data } = await apiClient.post<ApiResponse<unknown>>(
    `${BASE}/${appointmentId}/reschedule`,
    request
  );

  if (data.code !== 200 && data.code !== 201) {
    throw new Error(data.message || "Đổi lịch thất bại");
  }

  return data;
}

/**
 * Lấy danh sách khung giờ trống (OPEN) cho Nurse
 * Endpoint: GET /api/v1/appointments/available-slots
 */
export async function getAvailableSlots(params: {
  doctorId?: string;
  fromDate?: string;
  toDate?: string;
}): Promise<AvailableSlot[]> {
  const { data } = await apiClient.get<ApiResponse<AvailableSlot[]>>(
    `${BASE}/available-slots`,
    { params }
  );

  if (!data.data) {
    throw new Error(data.message || "Không tải được danh sách slot trống");
  }

  return data.data;
}

/**
 * Lấy danh sách bác sĩ
 * Endpoint: GET /api/v1/doctors
 */
export async function getDoctorList(): Promise<DoctorSummary[]> {
  const { data } = await apiClient.get<ApiResponse<DoctorSummary[]>>("/api/v1/doctors");

  if (!data.data) {
    throw new Error(data.message || "Không tải được danh sách bác sĩ");
  }

  return data.data;
}
