import type { Metadata } from "next";
import { InvoiceDetailView } from "@/features/prescription-adherence/components/invoice-detail-view";

export const metadata: Metadata = {
  title: "Chi tiết hóa đơn | ADSUS",
};

export default async function InvoiceDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <InvoiceDetailView invoiceId={id} />;
}
