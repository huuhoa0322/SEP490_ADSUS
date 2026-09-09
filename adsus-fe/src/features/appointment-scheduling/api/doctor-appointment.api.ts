import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";

import type {
  DoctorAppointmentQuery,
  DoctorPatientAppointment,
} from "../types/doctor-appointment.types";

export interface FollowUpAppointmentRequest {
  patientProfileId: string;
  scheduleSlotId: string;
  reason: string;
}

/** UC-15 mở rộng — chỉ Doctor gọi được, backend chặn bằng [Authorize(Roles = "DOCTOR")]. */
export async function listDoctorAppointments(
  query: DoctorAppointmentQuery,
): Promise<DoctorPatientAppointment[]> {
  const { data } = await apiClient.get<ApiResponse<DoctorPatientAppointment[]>>(
    "/api/v1/appointments/doctor",
    { params: { fromDate: query.fromDate, toDate: query.toDate } },
  );

  if (!data.data) throw new Error(data.message || "Không tải được lịch bệnh nhân.");

  return data.data;
}

export async function createFollowUpAppointment(
  request: FollowUpAppointmentRequest,
): Promise<void> {
  const { data } = await apiClient.post<ApiResponse<unknown>>(
    "/api/v1/appointments/follow-up",
    request
  );
  if (!data.data && data.code !== 201 && data.code !== 200) {
    throw new Error(data.message || "Không thể tạo lịch tái khám.");
  }
}
