import type { Metadata } from "next";

import { AdminFeedbackView } from "@/features/feedback/components/admin-feedback-view";

export const metadata: Metadata = {
  title: "Quản lý phản hồi dịch vụ | ADSUS",
  description: "Trang quản lý và theo dõi phản hồi, đánh giá chất lượng dịch vụ phòng khám dành cho Quản trị viên.",
};

export default function AdminFeedbackPage() {
  return <AdminFeedbackView />;
}
