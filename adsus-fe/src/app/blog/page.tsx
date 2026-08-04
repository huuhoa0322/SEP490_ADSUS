import type { Metadata } from "next";

import { BlogListView } from "@/features/blog/components/blog-list-view";

export const metadata: Metadata = {
  title: "Blog Sức khỏe | ADSUS",
  description: "Bài viết y tế được kiểm duyệt bởi bác sĩ chuyên khoa",
};

/**
 * SCR-26 — Blog Sức khỏe PUBLIC (UC-23).
 * GB-05: không có [Authorize] — Guest đọc được, Google index được.
 * GB-09: PUBLIC trên Web, không phải Patient-only.
 */
export default function BlogPage() {
  return <BlogListView />;
}
