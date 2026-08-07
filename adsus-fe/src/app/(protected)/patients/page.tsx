import type { Metadata } from "next";

import { PatientListView } from "@/features/medical-record/components/patient-list-view";

export const metadata: Metadata = {
  title: "Danh sách bệnh nhân | ADSUS",
};

/** SCR-09 — UC-09. Điểm vào của toàn bộ Module 04. */
export default function PatientsPage() {
  return <PatientListView />;
}
