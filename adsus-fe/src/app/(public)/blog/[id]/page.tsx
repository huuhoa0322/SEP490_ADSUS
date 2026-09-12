import { BlogDetailView } from "@/features/blog/components/blog-detail-view";

interface BlogDetailPageProps {
  params: Promise<{ id: string }>;
}

/**
 * SCR-26 — Chi tiết Blog Sức khỏe (UC-23).
 * 2026-09-12: public cho Guest + Patient + mọi role (BE [AllowAnonymous]).
 * GB-05: trả 404 nếu bài viết không tồn tại hoặc chưa publish.
 *
 * Route nằm trong (public) group để áp dụng LandingNavbar + LandingFooter
 * từ app/(public)/layout.tsx — giống trang chủ /.
 */
export default async function BlogDetailPage({ params }: BlogDetailPageProps) {
  const { id } = await params;

  return <BlogDetailView id={id} />;
}
