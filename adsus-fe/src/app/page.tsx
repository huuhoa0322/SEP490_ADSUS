import { redirect } from "next/navigation";

/**
 * Trang gốc "/" không có nội dung riêng.
 *
 * Mọi màn hình của hệ thống đều nằm sau bước đăng nhập, nên vào "/" là chuyển thẳng tới
 * trang đăng nhập. Người đã đăng nhập rồi thì AuthGuard sẽ tự đưa họ về khu vực theo vai
 * trò của mình.
 *
 * Dùng redirect() ngay phía máy chủ thay vì chuyển hướng bằng JavaScript, để người dùng
 * không phải thấy một trang trắng nhấp nháy rồi mới nhảy sang.
 */
export default function RootPage() {
  redirect("/login");
}
