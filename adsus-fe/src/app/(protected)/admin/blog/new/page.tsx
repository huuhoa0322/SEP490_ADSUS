import type { Metadata } from "next";

import { AdminBlogCreateView } from "@/features/blog/components/admin-blog-create-view";

export const metadata: Metadata = {
  title: "Tạo Bài viết mới | ADSUS Admin",
};

/**
 * SCR-27 — Admin Create Blog Post.
 * Requires ADMIN role.
 */
export default function AdminBlogCreatePage() {
  return <AdminBlogCreateView />;
}
