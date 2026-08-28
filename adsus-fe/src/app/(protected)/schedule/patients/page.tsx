import type { Metadata } from "next";

import { PatientScheduleView } from "@/features/appointment-scheduling/components/patient-schedule-view";

export const metadata: Metadata = {
  title: "Lịch bệnh nhân | ADSUS",
};

// Màn mới (28/08/2026) — chỉ Doctor xem, độc lập với /schedule (quản lý slot).
export default function PatientSchedulePage() {
  return <PatientScheduleView />;
}
