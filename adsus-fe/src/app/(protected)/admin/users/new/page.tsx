import type { Metadata } from "next";

import { UserForm } from "@/features/user-role-management/components/user-form";

export const metadata: Metadata = {
  title: "Tạo tài khoản | ADSUS",
};

// SCR-07 ở chế độ tạo mới (UC-04 FT-07).
export default function CreateUserPage() {
  return <UserForm />;
}
