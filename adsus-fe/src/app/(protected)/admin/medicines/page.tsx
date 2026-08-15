import type { Metadata } from "next";

import { MedicineList } from "@/features/medicines/components/medicine-list";

export const metadata: Metadata = {
  title: "Quản lý danh mục thuốc | ADSUS",
};

export default function AdminMedicinesPage() {
  return <MedicineList />;
}
