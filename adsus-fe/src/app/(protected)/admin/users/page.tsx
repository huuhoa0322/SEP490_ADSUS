import type { Metadata } from "next";

import { UserList } from "@/features/users/components/user-list";

export const metadata: Metadata = {
  title: "Quản lý tài khoản | ADSUS",
};

// SCR-06 — danh sách tài khoản (UC-04, Module 2).
// Nằm trong (protected) nên đã được AuthGuard bảo vệ; luật chỉ-Admin khai trong ROUTE_ROLES.
export default async function AdminUsersPage({
  searchParams,
}: {
  searchParams: Promise<{ created?: string | string[] }>;
}) {
  const created = (await searchParams).created;
  const createNotice =
    typeof created === "string" && created.length <= 500 ? created : undefined;

  return <UserList initialCreateNotice={createNotice} />;
}
