import { redirect } from "next/navigation";

/**
 * Redirect về trang danh sách - chi tiết bài viết hiển thị qua Modal popup.
 */
export default async function AdminBlogDetailPage() {
  redirect("/admin/blog");
}