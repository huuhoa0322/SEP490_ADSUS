import { BlogListView } from "@/features/blog/components/blog-list-view";

/**
 * SCR-26 — Blog Sức khỏe (UC-23).
 * 2026-09-12: mở public cho Guest (chưa đăng nhập) + Patient + mọi role.
 * BE `GET /api/v1/blog-posts` đã [AllowAnonymous] — override tạm GB-09 cũ,
 * xem project-state/decisions.md.
 * GB-05: chỉ hiển thị bài viết Status == Published.
 *
 * Route nằm trong (public) group để áp dụng LandingNavbar + LandingFooter
 * từ app/(public)/layout.tsx — giống trang chủ /.
 */
export default function BlogPage() {
  return <BlogListView />;
}
