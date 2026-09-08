'use client';

import { useState } from 'react';
import { format } from 'date-fns';
import { useDebounce } from 'use-debounce';
import { Search } from 'lucide-react';

import { PaginationNumbered } from "@/components/ui/pagination-numbered";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useInventoryHistory } from '@/features/medicines/api/inventory.api';

export default function InventoryHistoryPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [debouncedSearch] = useDebounce(searchTerm, 500);
  const [page, setPage] = useState(1);
  const pageSize = 15;

  type TxnTypeFilter = '' | 'Import' | 'Dispense' | 'Adjustment';
  const [txnType, setTxnType] = useState<TxnTypeFilter>('');

  const { data, isLoading, isError } = useInventoryHistory({
    search: debouncedSearch || undefined,
    type: txnType || undefined,
    page,
    pageSize,
  });

  const handleTypeChange = (val: TxnTypeFilter) => {
    setTxnType(val);
    setPage(1);
  };

  const getTxnTypeLabel = (type: string) => {
    switch (type.toLowerCase()) {
      case 'import': return <span className="rounded-full bg-[var(--status-good)]/12 px-2.5 py-1 text-xs font-600 text-[var(--status-good)]">Nhập kho</span>;
      case 'dispense': return <span className="rounded-full bg-[var(--status-warning)]/12 px-2.5 py-1 text-xs font-600 text-[var(--status-warning)]">Xuất kho</span>;
      case 'adjustment': return <span className="rounded-full bg-[var(--ring)]/12 px-2.5 py-1 text-xs font-600 text-[var(--ring)]">Điều chỉnh</span>;
      default: return <span className="rounded-full bg-secondary px-2.5 py-1 text-xs font-600 text-muted-foreground">{type}</span>;
    }
  };

  return (
    <div className="mx-auto w-full max-w-screen-2xl px-6 py-8">
      <div>
        <h1 className="font-heading text-[32px] font-bold tracking-[-0.02em] text-foreground">Lịch sử nhập / xuất kho</h1>
        <p className="mt-1.5 text-[15px] text-muted-foreground">
          Tra cứu biến động tồn kho: nhập kho, xuất bán và điều chỉnh kiểm kê.
        </p>
      </div>

      <div className="mt-8 flex flex-wrap items-center gap-3">
        <div className="relative min-w-64 flex-1">
          <Search className="pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <input
            placeholder="Tìm kiếm theo Tên thuốc, Số lô, Nhà cung cấp..."
            value={searchTerm}
            onChange={(e) => {
              setSearchTerm(e.target.value);
              setPage(1); // Reset page on search
            }}
            className="h-12 w-full rounded-full border border-border bg-background pl-11 pr-4 text-[15px] outline-none transition-colors focus:border-accent"
          />
        </div>

        <div className="ml-0 flex gap-1.5 md:ml-auto">
          {(['', 'Import', 'Dispense', 'Adjustment'] as TxnTypeFilter[]).map(type => (
            <button
              key={type}
              onClick={() => handleTypeChange(type)}
              className={`h-9 rounded-full px-4 text-sm font-medium transition-colors border ${
                txnType === type
                  ? 'border-accent bg-accent text-accent-foreground shadow-sm'
                  : 'border-border hover:bg-secondary text-foreground'
              }`}
            >
              {type === '' ? 'Tất cả' : type === 'Import' ? 'Nhập kho' : type === 'Dispense' ? 'Xuất kho' : 'Điều chỉnh'}
            </button>
          ))}
        </div>
      </div>

      <div className="mt-6 overflow-hidden overflow-x-auto rounded-3xl border border-border bg-background">
            <Table>
              <TableHeader className="bg-secondary/40">
                <TableRow>
                  <TableHead className="w-[180px] px-5 py-4">Thời gian</TableHead>
                  <TableHead className="px-5 py-4">Loại giao dịch</TableHead>
                  <TableHead className="px-5 py-4">Thuốc (Số lô)</TableHead>
                  <TableHead className="px-5 py-4">Đối tác / Ghi chú</TableHead>
                  <TableHead className="text-right px-5 py-4">Đơn giá nhập</TableHead>
                  <TableHead className="text-right px-5 py-4">Số lượng (Đơn vị cơ bản)</TableHead>
                  <TableHead className="text-right px-5 py-4">Số lượng (Đơn vị đóng gói)</TableHead>
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
                  data?.items?.map((item) => {
                    const isImport = item.txnType.toLowerCase() === 'import';
                    const isDispense = item.txnType.toLowerCase() === 'dispense';
                    const isIncrease = item.quantityBase > 0;
                    const isPositive = isImport || (!isDispense && isIncrease);

                    return (
                    <TableRow key={item.transactionId}>
                      <TableCell className="font-medium px-5 py-4">
                        {format(new Date(item.txnDate), 'dd/MM/yyyy HH:mm')}
                      </TableCell>
                      <TableCell className="px-5 py-4">
                        {getTxnTypeLabel(item.txnType)}
                      </TableCell>
                      <TableCell className="px-5 py-4">
                        <div className="font-medium text-primary">{item.medicineName}</div>
                        <div className="text-xs text-muted-foreground">Lô: {item.lotNumber}</div>
                      </TableCell>
                      <TableCell className="px-5 py-4">
                        {item.supplierName ? (
                          item.supplierName
                        ) : item.txnType.toLowerCase() === 'adjustment' && item.reason ? (
                          item.reason
                        ) : (
                          '—'
                        )}
                      </TableCell>
                      <TableCell className="text-right font-mono px-5 py-4">
                        {item.unitImportPrice ? item.unitImportPrice.toLocaleString() + ' đ' : '—'}
                      </TableCell>
                      <TableCell className="text-right px-5 py-4 font-mono">
                        <span className={isPositive ? 'text-[var(--status-good)] font-semibold' : 'text-[var(--status-warning)] font-semibold'}>
                          {isPositive ? '+' : '-'}{Math.abs(item.quantityBase).toLocaleString()}
                        </span>
                        {item.baseUnitName && (
                          <span className="ml-1 font-sans text-xs text-muted-foreground">{item.baseUnitName}</span>
                        )}
                      </TableCell>
                      <TableCell className="text-right text-sm px-5 py-4 font-mono">
                        <span className={isPositive ? 'text-[var(--status-good)] font-semibold' : 'text-[var(--status-warning)] font-semibold'}>
                          {isPositive ? '+' : '-'}{Math.abs(item.quantityInUnit).toLocaleString()}
                        </span>
                        {item.unitName && (
                          <span className="ml-1 font-sans text-xs text-muted-foreground">{item.unitName}</span>
                        )}
                      </TableCell>
                    </TableRow>
                  );
                })
                )}
              </TableBody>
            </Table>
      </div>

      {data && data.totalItems > pageSize && (
        <div className="mt-5 flex items-center justify-between text-sm text-muted-foreground">
          <span>
            Đang hiển thị {((page - 1) * pageSize) + 1} - {Math.min(page * pageSize, data.totalItems)} trên tổng số {data.totalItems} giao dịch.
          </span>
          <PaginationNumbered
            currentPage={page}
            totalPages={Math.ceil(data.totalItems / pageSize)}
            setPage={setPage}
          />
        </div>
      )}
    </div>
  );
}
