/** Khớp DoctorPatientAppointmentResponse phía Backend — chỉ appointment còn Booked, Cancelled/Completed/khác bị lọc bỏ. Bác sĩ chỉ quản lý case khám, không theo dõi checkin qua Appointment. */
export interface DoctorPatientAppointment {
  appointmentId: string;
  slotDate: string; // yyyy-MM-dd
  startTime: string; // HH:mm:ss
  endTime: string;
  patientProfileId: string;
  patientFullName: string;
  reason: string | null;
}

export interface DoctorAppointmentQuery {
  fromDate: string;
  toDate: string;
}
