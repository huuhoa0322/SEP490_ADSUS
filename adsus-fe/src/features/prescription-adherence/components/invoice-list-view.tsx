"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useInvoicesList } from "../hooks/use-invoices";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { PaginationNumbered } from "@/components/ui/pagination-numbered";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Search, RefreshCw, Info } from "lucide-react";
import type { InvoiceFilter } from "@/api/invoiceService";

type InvoiceStatus = "PENDING" | "PAID" | "CANCELLED";

const STATUS_LABELS: Record<InvoiceStatus | "ALL", string> = {
  ALL: "Tất cả",
  PENDING: "Chờ thanh toán",
  PAID: "Đã thanh toán",
  CANCELLED: "Đã hủy",
};

function StatusBadge({ status }: { status: string }) {
  switch (status) {
    case "PENDING":
      return <Badge variant="secondary" style={{ backgroundColor: "var(--amber-warn, #E8963C)", color: "#fff" }}>Chờ thanh toán</Badge>;
    case "PAID":
      return <Badge style={{ backgroundColor: "var(--success, #1E9E6B)", color: "#fff" }}>Đã thanh toán</Badge>;
    case "CANCELLED":
      return <Badge variant="destructive">Đã hủy</Badge>;
    default:
      return <Badge>{status}</Badge>;
  }
}

function PaymentMethodBadge({ method }: { method?: string }) {
  if (!method) return null;
  if (method === "BANK_TRANSFER") {
    return (
      <span style={{ backgroundColor: "#E4F5F3", color: "#1F2A44" }} className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium">
        Chuyển khoản
      </span>
    );
  }
  if (method === "CASH") {
    return (
      <span style={{ backgroundColor: "var(--teal-primary, #128C82)", color: "#fff" }} className="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium">
        Tiền mặt
      </span>
    );
  }
  return null;
}

function CancelReasonTooltip({ reason }: { reason?: string }) {
  if (!reason) return null;
  return (
    <span title={reason} className="inline-flex items-center gap-1 text-destructive text-xs ml-1 cursor-help">
      <Info className="h-3 w-3" />
      (đã hủy)
    </span>
  );
}

function formatCurrency(amount: number) {
  return new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND" }).format(amount);
}

export function InvoiceListView() {
  const router = useRouter();
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState<InvoiceStatus | "ALL">("ALL");
  const [page, setPage] = useState(1);

  const filter: InvoiceFilter = {
    page,
    pageSize: 10,
    search: search || undefined,
    status: status === "ALL" ? undefined : status,
  };

  const { data, isLoading, isError, error, refetch, isFetching } = useInvoicesList(filter);

  const handleSearch = () => {
    setPage(1);
    refetch();
  };

  const handleStatusChange = (value: string) => {
    setStatus(value as InvoiceStatus | "ALL");
    setPage(1);
  };

  return (
    <div className="mx-auto w-[90%] max-w-[90%] py-8 space-y-6">
      <div className="flex justify-between items-center border-b-2 pb-4">
        <div>
          <h1 className="text-3xl font-extrabold tracking-tight text-primary">Quản lý hóa đơn</h1>
          <p className="text-muted-foreground mt-1 font-medium">Danh sách hóa đơn của các ca khám bệnh.</p>
        </div>
      </div>

      <div className="flex flex-col sm:flex-row gap-2">
        <div className="relative flex-1 max-w-sm">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Tìm theo ID hoặc Tên bệnh nhân..."
            className="pl-8"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && handleSearch()}
            aria-label="Tìm kiếm hóa đơn"
          />
        </div>
        <Select value={status} onValueChange={handleStatusChange} aria-label="Lọc theo trạng thái">
          <SelectTrigger className="w-48">
            <SelectValue placeholder="Tất cả" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="ALL">{STATUS_LABELS.ALL}</SelectItem>
            <SelectItem value="PENDING">{STATUS_LABELS.PENDING}</SelectItem>
            <SelectItem value="PAID">{STATUS_LABELS.PAID}</SelectItem>
            <SelectItem value="CANCELLED">{STATUS_LABELS.CANCELLED}</SelectItem>
          </SelectContent>
        </Select>
        <Button onClick={handleSearch}>Tìm kiếm</Button>
      </div>

      <div className="border-2 rounded-md">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="hidden sm:table-cell font-bold">Mã Hóa Đơn</TableHead>
              <TableHead className="font-bold">Bệnh Nhân</TableHead>
              <TableHead className="hidden sm:table-cell font-bold">Ca Khám</TableHead>
              <TableHead className="font-bold">Tổng Tiền</TableHead>
              <TableHead className="font-bold">Trạng Thái</TableHead>
              <TableHead className="hidden md:table-cell font-bold">Ngày Tạo</TableHead>
              <TableHead className="text-right font-bold">Thao Tác</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading ? (
              <TableRow>
                <TableCell colSpan={7} className="text-center py-10">
                  <div className="flex justify-center items-center gap-2">
                    <span className="animate-spin"><RefreshCw className="h-5 w-5 text-muted-foreground" /></span>
                    <span>Đang tải...</span>
                  </div>
                </TableCell>
              </TableRow>
            ) : isError ? (
              <TableRow>
                <TableCell colSpan={7} className="text-center py-10 text-destructive">
                  <p className="font-medium">Lỗi khi tải hóa đơn</p>
                  <p className="text-sm text-muted-foreground">{error?.message}</p>
                  <Button variant="outline" size="sm" className="mt-2" onClick={() => refetch()}>
                    Thử lại
                  </Button>
                </TableCell>
              </TableRow>
            ) : data?.items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} className="text-center py-10">
                  <div role="status">
                    <p className="text-muted-foreground">Chưa có hóa đơn nào — chờ ca khám có kê đơn.</p>
                    <Button variant="outline" size="sm" className="mt-3" onClick={() => refetch()}>
                      <RefreshCw className="h-4 w-4 mr-1" /> Làm mới
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            ) : (
              data?.items.map((invoice) => (
                <TableRow key={invoice.id}>
                  <TableCell className="hidden sm:table-cell font-mono text-xs max-w-[120px] truncate" title={invoice.id}>
                    {invoice.id}
                  </TableCell>
                  <TableCell className="font-medium">{invoice.caseName}</TableCell>
                  <TableCell className="hidden sm:table-cell">
                    <span className="font-mono text-xs text-muted-foreground">{invoice.caseId}</span>
                  </TableCell>
                  <TableCell className="font-bold">{formatCurrency(invoice.totalAmount)}</TableCell>
                  <TableCell>
                    <div className="flex flex-wrap items-center gap-1">
                      <StatusBadge status={invoice.status} />
                      {invoice.status === "PAID" && invoice.paymentMethod && (
                        <PaymentMethodBadge method={invoice.paymentMethod} />
                      )}
                      {invoice.status === "CANCELLED" && invoice.cancelledReason && (
                        <CancelReasonTooltip reason={invoice.cancelledReason} />
                      )}
                    </div>
                  </TableCell>
                  <TableCell className="hidden md:table-cell text-sm text-muted-foreground">
                    {new Date(invoice.createdAt).toLocaleString("vi-VN")}
                  </TableCell>
                  <TableCell className="text-right">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => router.push(`/invoices/${invoice.id}`)}
                    >
                      Chi Tiết
                    </Button>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {data && data.totalPages > 1 && (
        <PaginationNumbered
          currentPage={page}
          totalPages={data.totalPages}
          setPage={setPage}
          className="justify-end mt-4"
        />
      )}
    </div>
  );
}
