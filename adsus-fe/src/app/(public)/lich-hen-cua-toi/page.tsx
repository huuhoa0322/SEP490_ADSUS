import type { Metadata } from "next";
import { AppointmentHistoryPageWrapper } from "@/features/appointment-scheduling/components/appointment-history-page-wrapper";

export const metadata: Metadata = {
  title: "Lịch hẹn của tôi | ADSUS",
  description: "Xem và quản lý các lịch hẹn khám đã đặt tại ADSUS.",
};

export default function Page() {
  return <AppointmentHistoryPageWrapper />;
}
