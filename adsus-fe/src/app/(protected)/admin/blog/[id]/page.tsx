import { redirect } from "next/navigation";

interface AdminBlogDetailPageProps {
  params: Promise<{ id: string }>;
}

/**
 * Redirect về trang danh sách - chi tiết bài viết hiển thị qua Modal popup.
 */
export default async function AdminBlogDetailPage(_props: AdminBlogDetailPageProps) {
  redirect("/admin/blog");
}