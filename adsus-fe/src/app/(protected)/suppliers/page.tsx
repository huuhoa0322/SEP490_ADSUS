import type { Metadata } from "next";

import { SupplierList } from "@/features/medicines/components/supplier-list";

export const metadata: Metadata = {
  title: "Quản lý nhà cung cấp | ADSUS",
};

export default function AdminSuppliersPage() {
  return <SupplierList />;
}
