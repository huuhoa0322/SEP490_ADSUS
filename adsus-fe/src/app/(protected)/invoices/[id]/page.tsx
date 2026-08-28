import type { Metadata } from "next";
import { InvoiceDetailView } from "@/features/prescription-adherence/components/invoice-detail-view";

export const metadata: Metadata = {
  title: "Chi tiết hóa đơn | ADSUS",
};

export default function InvoiceDetailPage({ params }: { params: { id: string } }) {
  return <InvoiceDetailView invoiceId={params.id} />;
}
