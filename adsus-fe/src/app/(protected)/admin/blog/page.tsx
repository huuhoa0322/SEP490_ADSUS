import type { Metadata } from "next";

import { AdminBlogListView } from "@/features/blog/components/admin-blog-list-view";

export const metadata: Metadata = {
  title: "Quản lý Blog | ADSUS Admin",
};

/**
 * SCR-27 — Admin Blog Management (UC-24).
 * Yêu cầu quyền ADMIN.
 */
export default function AdminBlogPage() {
  return <AdminBlogListView />;
}
