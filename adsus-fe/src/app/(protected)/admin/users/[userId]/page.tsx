import type { Metadata } from "next";

import { UserForm } from "@/features/user-role-management/components/user-form";

export const metadata: Metadata = {
  title: "Sửa tài khoản | ADSUS",
};

// SCR-07 ở chế độ sửa (UC-04 FT-09 — phân quyền và cập nhật thông tin).
// Next.js 16: params là Promise, phải await.
export default async function EditUserPage({
  params,
}: {
  params: Promise<{ userId: string }>;
}) {
  const { userId } = await params;

  return <UserForm userId={userId} />;
}
