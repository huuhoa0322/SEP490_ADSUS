import type { Metadata } from "next";

import { BlogListView } from "@/features/blog/components/blog-list-view";

export const metadata: Metadata = {
  title: "Blog Sức khỏe | ADSUS",
  description: "Bài viết y tế được kiểm duyệt bởi bác sĩ chuyên khoa",
};

/**
 * SCR-26 — Blog Sức khỏe (UC-23).
 * 2026-09-12: mở public cho Guest (chưa đăng nhập) + Patient + mọi role.
 * BE `GET /api/v1/blog-posts` đã [AllowAnonymous] — override tạm GB-09 cũ,
 * xem project-state/decisions.md.
 * GB-05: chỉ hiển thị bài viết Status == Published.
 */
export default function BlogPage() {
  return <BlogListView />;
}
