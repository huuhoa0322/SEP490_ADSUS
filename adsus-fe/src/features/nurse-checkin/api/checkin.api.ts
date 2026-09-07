import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";
import type { CheckinQueueResponse } from "../types/checkin.types";

const BASE = "/api/v1/appointments";

// Empty GUID constant
const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";

export async function getCheckinQueue(date?: string): Promise<CheckinQueueResponse> {
  const { data } = await apiClient.get<ApiResponse<CheckinQueueResponse>>(
    `${BASE}/checkin-queue`,
    {
      params: {
        date: date || undefined,
      },
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
