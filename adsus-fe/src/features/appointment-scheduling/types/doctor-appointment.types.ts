/** Khớp DoctorPatientAppointmentResponse phía Backend — appointment còn Booked, Cancelled/Completed bị lọc bỏ. Lưu ý: APPROVED không còn được sử dụng (giữ lại cho backward compatibility với database). */
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
