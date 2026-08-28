"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { invoiceService, InvoiceResponse, PagedResult } from "@/api/invoiceService";
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
import { Search } from "lucide-react";

export function InvoiceListView() {
  const router = useRouter();
  const [data, setData] = useState<PagedResult<InvoiceResponse> | null>(null);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);

  const fetchInvoices = async (currentPage: number, currentSearch: string) => {
    try {
      setLoading(true);
      const res = await invoiceService.getInvoices({
        page: currentPage,
        pageSize: 10,
        search: currentSearch,
      });
      setData(res);
    } catch (error) {
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchInvoices(page, search);
  }, [page]);

  const handleSearch = () => {
    setPage(1);
    fetchInvoices(1, search);
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "PENDING":
        return <Badge variant="secondary">Chờ thanh toán</Badge>;
      case "PAID":
        return <Badge variant="default" className="bg-green-600 hover:bg-green-700">Đã thanh toán</Badge>;
      case "CANCELLED":
        return <Badge variant="destructive">Đã hủy</Badge>;
      default:
        return <Badge>{status}</Badge>;
    }
  };

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat("vi-VN", {
      style: "currency",
      currency: "VND",
    }).format(amount);
  };

  return (
    <div className="p-6 space-y-6">
      <div className="flex justify-between items-center border-b pb-4">
        <div>
          <h1 className="text-3xl font-bold tracking-tight text-primary">Quản lý hóa đơn</h1>
          <p className="text-muted-foreground mt-1">Danh sách hóa đơn của các ca khám bệnh.</p>
        </div>
      </div>

      <div className="flex items-center space-x-2">
        <div className="relative flex-1 max-w-sm">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Tìm theo ID hoặc Tên bệnh nhân..."
            className="pl-8"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && handleSearch()}
          />
        </div>
        <Button onClick={handleSearch}>Tìm kiếm</Button>
      </div>

      <div className="border rounded-md">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Mã Hóa Đơn</TableHead>
              <TableHead>Bệnh Nhân</TableHead>
              <TableHead>Tổng Tiền</TableHead>
              <TableHead>Trạng Thái</TableHead>
              <TableHead>Ngày Tạo</TableHead>
              <TableHead className="text-right">Thao Tác</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {loading ? (
              <TableRow>
                <TableCell colSpan={6} className="text-center py-10">Đang tải...</TableCell>
              </TableRow>
            ) : data?.items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={6} className="text-center py-10">Không có hóa đơn nào.</TableCell>
              </TableRow>
            ) : (
              data?.items.map((invoice) => (
                <TableRow key={invoice.id}>
                  <TableCell className="font-medium text-xs">{invoice.id}</TableCell>
                  <TableCell>{invoice.caseName}</TableCell>
                  <TableCell className="font-semibold">{formatCurrency(invoice.totalAmount)}</TableCell>
                  <TableCell>{getStatusBadge(invoice.status)}</TableCell>
                  <TableCell>{new Date(invoice.createdAt).toLocaleString("vi-VN")}</TableCell>
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
        <div className="flex justify-end items-center space-x-2">
          <Button
            variant="outline"
            disabled={page === 1}
            onClick={() => setPage(p => p - 1)}
          >
            Trước
          </Button>
          <span className="text-sm">Trang {page} / {data.totalPages}</span>
          <Button
            variant="outline"
            disabled={page === data.totalPages}
            onClick={() => setPage(p => p + 1)}
          >
            Sau
          </Button>
        </div>
      )}
    </div>
  );
}
