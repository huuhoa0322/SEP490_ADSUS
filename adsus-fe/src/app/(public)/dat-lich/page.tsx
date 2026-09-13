import type { Metadata } from "next";

import { BookingPageWrapper } from "@/features/appointment-scheduling/components/booking-page-wrapper";

export const metadata: Metadata = {
  title: "Đặt lịch khám | ADSUS",
  description: "Đặt lịch khám trực tuyến tại Phòng khám Siêu âm ADSUS.",
};

/**
 * UC-13 — Patient đặt lịch khám (Module 8).
 * Route: /dat-lich
 * Guest thấy slot, bấm "Đặt lịch" → redirect /login.
 * Patient đã login → đặt lịch bình thường.
 */
export default function SchedulePage() {
  return <BookingPageWrapper />;
}
