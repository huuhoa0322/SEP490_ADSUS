import type { Metadata } from "next";

import { DashboardView } from "@/features/dashboard/components/dashboard-view";

export const metadata: Metadata = {
  title: "Thống kê | ADSUS",
};

// SCR-08 — thống kê vận hành hệ thống (UC-05, FT-10, Module 3).
// Chỉ Admin: luật khai trong ROUTE_ROLES, chặn thật ở [Authorize(Roles = "ADMIN")] phía backend.
export default function DashboardPage() {
  return <DashboardView />;
}
