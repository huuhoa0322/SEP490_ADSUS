import type { Metadata } from "next";
import { ClinicServiceManagement } from "@/features/clinic-service/components/clinic-service-management";

export const metadata: Metadata = {
  title: "Quản lý dịch vụ phòng khám | ADSUS",
};

export default function AdminClinicServicesPage() {
  return <ClinicServiceManagement />;
}
