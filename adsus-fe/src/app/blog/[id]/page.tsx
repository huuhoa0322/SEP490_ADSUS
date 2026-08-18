import type { Metadata } from "next";

import { BlogDetailView } from "@/features/blog/components/blog-detail-view";

interface BlogDetailPageProps {
  params: Promise<{ id: string }>;
}

/**
 * SCR-26 — Chi tiết Blog Sức khỏe PUBLIC (UC-23).
 * GB-05: trả 404 nếu bài viết không tồn tại hoặc chưa publish.
 */
export async function generateMetadata(): Promise<Metadata> {
  return {
    title: `Bài viết | ADSUS`,
    description: "Chi tiết bài viết y tế",
  };
}

export default async function BlogDetailPage({ params }: BlogDetailPageProps) {
  const { id } = await params;

  return <BlogDetailView id={id} />;
}
