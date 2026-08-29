import type { Metadata } from "next";
import { InvoiceListView } from "@/features/prescription-adherence/components/invoice-list-view";

export const metadata: Metadata = {
  title: "Quản lý hóa đơn | ADSUS",
};

export default function InvoicesPage() {
  return <InvoiceListView />;
}
