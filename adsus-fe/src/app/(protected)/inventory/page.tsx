'use client';

import { useState } from 'react';
import { format } from 'date-fns';
import { useDebounce } from 'use-debounce';
import { Search } from 'lucide-react';

import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useInventoryHistory } from '@/features/medicines/api/inventory.api';

export default function InventoryHistoryPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [debouncedSearch] = useDebounce(searchTerm, 500);
  const [page, setPage] = useState(1);
  const pageSize = 15;

  const { data, isLoading, isError } = useInventoryHistory({
    search: debouncedSearch,
    page,
    pageSize,
  });

  const getTxnTypeLabel = (type: string) => {
    switch (type.toLowerCase()) {
      case 'import': return <span className="px-2 py-1 bg-green-100 text-green-700 rounded-md text-xs font-medium">Nhập kho</span>;
      case 'dispense': return <span className="px-2 py-1 bg-blue-100 text-blue-700 rounded-md text-xs font-medium">Xuất kho</span>;
      case 'adjustment': return <span className="px-2 py-1 bg-orange-100 text-orange-700 rounded-md text-xs font-medium">Điều chỉnh</span>;
      default: return <span className="px-2 py-1 bg-gray-100 text-gray-700 rounded-md text-xs font-medium">{type}</span>;
    }
  };

  return (
    <div className="flex-1 space-y-4 p-4 md:p-8 pt-6">
      <div className="flex items-center justify-between space-y-2">
        <h2 className="text-3xl font-bold tracking-tight">Lịch sử Nhập / Xuất kho</h2>
      </div>
      
      <Card>
        <CardHeader>
          <CardTitle>Tra cứu biến động tồn kho</CardTitle>
          <CardDescription>
            Xem lại lịch sử nhập kho, xuất bán và các giao dịch khác.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex items-center gap-2 mb-6">
            <div className="relative w-full md:w-96">
              <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Tìm kiếm theo Tên thuốc, Số lô, Nhà cung cấp..."
                className="pl-8"
                value={searchTerm}
                onChange={(e) => {
                  setSearchTerm(e.target.value);
                  setPage(1); // Reset page on search
                }}
              />
            </div>
          </div>

          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-[180px]">Thời gian</TableHead>
                  <TableHead>Loại giao dịch</TableHead>
                  <TableHead>Thuốc (Số lô)</TableHead>
                  <TableHead>Nhà cung cấp</TableHead>
                  <TableHead className="text-right">Đơn giá nhập</TableHead>
                  <TableHead className="text-right">Số lượng (Đơn vị cơ bản)</TableHead>
                  <TableHead className="text-right">Số lượng (Đơn vị đóng gói)</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  <TableRow>
                    <TableCell colSpan={7} className="h-24 text-center">
                      Đang tải dữ liệu...
                    </TableCell>
                  </TableRow>
                ) : isError ? (
                  <TableRow>
                    <TableCell colSpan={7} className="h-24 text-center text-destructive">
                      Có lỗi xảy ra khi tải dữ liệu lịch sử.
                    </TableCell>
                  </TableRow>
                ) : data?.items?.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7} className="h-24 text-center text-muted-foreground">
                      Không tìm thấy lịch sử giao dịch nào.
                    </TableCell>
                  </TableRow>
                ) : (
                  data?.items?.map((item) => (
                    <TableRow key={item.transactionId}>
                      <TableCell className="font-medium">
                        {format(new Date(item.txnDate), 'dd/MM/yyyy HH:mm')}
                      </TableCell>
                      <TableCell>
                        {getTxnTypeLabel(item.txnType)}
                      </TableCell>
                      <TableCell>
                        <div className="font-medium text-primary">{item.medicineName}</div>
                        <div className="text-xs text-muted-foreground">Lô: {item.lotNumber}</div>
                      </TableCell>
                      <TableCell>{item.supplierName || '—'}</TableCell>
                      <TableCell className="text-right font-mono">
                        {item.unitImportPrice ? item.unitImportPrice.toLocaleString() + ' đ' : '—'}
                      </TableCell>
                      <TableCell className="text-right">
                        {item.txnType.toLowerCase() === 'import' ? '+' : '-'}{item.quantityBase}
                      </TableCell>
                      <TableCell className="text-right text-muted-foreground text-sm">
                        {item.txnType.toLowerCase() === 'import' ? '+' : '-'}{item.quantityInUnit} {item.unitName}
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
          
          {data && data.totalCount > pageSize && (
            <div className="flex items-center justify-end space-x-2 py-4">
              <div className="flex-1 text-sm text-muted-foreground">
                Đang hiển thị {((page - 1) * pageSize) + 1} - {Math.min(page * pageSize, data.totalCount)} trên tổng số {data.totalCount} giao dịch.
              </div>
              <div className="space-x-2">
                <button
                  className="px-3 py-1 text-sm border rounded-md disabled:opacity-50"
                  onClick={() => setPage(p => Math.max(1, p - 1))}
                  disabled={page === 1}
                >
                  Trang trước
                </button>
                <button
                  className="px-3 py-1 text-sm border rounded-md disabled:opacity-50"
                  onClick={() => setPage(p => p + 1)}
                  disabled={page * pageSize >= data.totalCount}
                >
                  Trang sau
                </button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
