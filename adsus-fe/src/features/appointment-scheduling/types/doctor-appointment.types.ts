/** Khớp DoctorPatientAppointmentResponse phía Backend — appointment còn Booked hoặc Approved (Approved = đã checkin), Cancelled/Completed bị lọc bỏ. */
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
