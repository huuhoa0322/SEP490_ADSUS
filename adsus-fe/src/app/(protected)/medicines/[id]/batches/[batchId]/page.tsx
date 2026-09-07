'use client';

import { useState, useCallback } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { format } from 'date-fns';
import { useDebounce } from 'use-debounce';
import { PaginationNumbered } from '@/components/ui/pagination-numbered';
import {
  ArrowLeft, Search, Activity, ArrowDownToLine, ArrowUpFromLine,
  ChevronUp, ChevronDown, ChevronsUpDown,
} from 'lucide-react';
import { useInventoryHistory, useMedicineBatches } from '@/features/medicines/api/inventory.api';
import { useMedicineById } from '@/features/medicines/hooks/use-medicines';
import { formatCurrency } from '@/lib/utils';
import { AdjustInventoryModal } from '@/features/medicines/components/adjust-inventory-modal';
import { Edit } from 'lucide-react';

type SortKey = 'txnDate' | 'quantityBase';
type TxnTypeFilter = '' | 'Import' | 'Dispense' | 'Adjustment';

function SortIcon({ field, current, dir }: { field: SortKey; current: SortKey; dir: 'asc' | 'desc' }) {
  if (field !== current) return <ChevronsUpDown className="ml-1 inline size-3.5 text-muted-foreground/40" />;
  return dir === 'asc'
    ? <ChevronUp className="ml-1 inline size-3.5 text-accent" />
    : <ChevronDown className="ml-1 inline size-3.5 text-accent" />;
}


export default function BatchHistoryPage() {
  const { id: medicineId, batchId } = useParams<{ id: string; batchId: string }>();
  const router = useRouter();

  const [searchInput, setSearchInput] = useState('');
  const [debouncedSearch] = useDebounce(searchInput, 400);
  const [txnType, setTxnType] = useState<TxnTypeFilter>('');
  const [page, setPage] = useState(1);
  const pageSize = 25;
  const [sortBy, setSortBy] = useState<SortKey>('txnDate');
  const [sortDir, setSortDir] = useState<'asc' | 'desc'>('desc');
  const [isAdjustOpen, setIsAdjustOpen] = useState(false);

  const { data: medicine } = useMedicineById(medicineId);
  const { data: batches } = useMedicineBatches(medicineId);
  // @ts-expect-error - BE trả về PagedResult mặc dù type API đang định nghĩa là Array
  const currentBatch = (batches?.items || batches)?.find((b: { batchId: string }) => b.batchId === batchId);

  const { data, isLoading, isError } = useInventoryHistory({
    batchId,
    search: debouncedSearch || undefined,
    type: txnType || undefined,
    sortBy,
    sortDir,
    page,
    pageSize,
  });

  const handleSort = useCallback((field: SortKey) => {
    if (field === sortBy) {
      setSortDir(d => d === 'asc' ? 'desc' : 'asc');
    } else {
      setSortBy(field);
      setSortDir(field === 'txnDate' ? 'desc' : 'asc');
    }
    setPage(1);
  }, [sortBy]);

  const handleSearch = (val: string) => {
    setSearchInput(val);
    setPage(1);
  };

  const handleTypeChange = (val: TxnTypeFilter) => {
    setTxnType(val);
    setPage(1);
  };

  return (
    <div className="mx-auto w-full max-w-screen-2xl px-6 py-8">
      {/* Header */}
      <div className="mb-6 flex items-center gap-4">
        <button
          onClick={() => router.back()}
          className="flex size-10 items-center justify-center rounded-full border border-border text-muted-foreground transition-colors hover:bg-secondary"
        >
          <ArrowLeft className="size-4" />
        </button>
        <div className="flex-1 flex justify-between items-start">
          <div>
            <p className="text-sm text-muted-foreground">
              <button onClick={() => router.push(`/medicines/${medicineId}/batches`)} className="hover:text-accent transition-colors">
                {medicine?.name ?? 'Thuốc'} / Danh sách lô
              </button>
            </p>
            <h1 className="font-heading text-2xl font-bold tracking-tight text-foreground flex items-center gap-2">
              Lịch sử giao dịch — Lô #{currentBatch?.lotNumber || batchId.split('-')[0].toUpperCase()}
            </h1>
          </div>
          {currentBatch && (
            <button
              onClick={() => setIsAdjustOpen(true)}
              className="flex items-center gap-2 rounded-full bg-amber-500 px-4 py-2 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-amber-600"
            >
              <Edit className="size-4" />
              Kiểm kê / Điều chỉnh
            </button>
          )}
        </div>
      </div>

      {/* Toolbar */}
      <div className="mb-5 flex flex-wrap items-center gap-3">
        {/* Search */}
        <div className="relative min-w-[220px] max-w-sm flex-1">
          <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <input
            type="text"
            placeholder="Tìm theo nhà cung cấp..."
            value={searchInput}
            onChange={e => handleSearch(e.target.value)}
            className="w-full h-10 rounded-full border border-border bg-background py-2 pl-10 pr-4 text-sm outline-none transition-colors focus:border-accent"
          />
        </div>

        {/* Type filter */}
        <div className="flex gap-1.5">
          {(['', 'Import', 'Dispense', 'Adjustment'] as TxnTypeFilter[]).map(type => (
            <button
              key={type}
              onClick={() => handleTypeChange(type)}
              className={`h-9 rounded-full px-4 text-sm font-medium transition-colors border ${
                txnType === type
                  ? 'border-accent bg-accent text-white shadow-sm'
                  : 'border-border hover:bg-secondary text-foreground'
              }`}
            >
              {type === '' ? 'Tất cả' : type === 'Import' ? 'Nhập kho' : type === 'Dispense' ? 'Xuất kho' : 'Điều chỉnh'}
            </button>
          ))}
        </div>

        {data && (
          <p className="ml-auto text-sm text-muted-foreground">
            Tổng <span className="font-semibold text-foreground">{data.totalItems}</span> giao dịch
          </p>
        )}
      </div>

      {/* Table */}
      <div className="overflow-hidden rounded-2xl border border-border bg-white shadow-sm">
        <table className="w-full text-sm">
          <thead className="bg-secondary/40">
            <tr className="border-b border-border">
              <th
                className="px-5 py-3.5 text-left font-semibold text-muted-foreground cursor-pointer select-none hover:text-foreground"
                onClick={() => handleSort('txnDate')}
              >
                Thời gian <SortIcon field="txnDate" current={sortBy} dir={sortDir} />
              </th>
              <th className="px-5 py-3.5 text-left font-semibold text-muted-foreground">Loại</th>
              <th
                className="px-5 py-3.5 text-right font-semibold text-muted-foreground cursor-pointer select-none hover:text-foreground"
                onClick={() => handleSort('quantityBase')}
              >
                Số lượng ({data?.items?.[0]?.baseUnitName || 'ĐV cơ sở'}) <SortIcon field="quantityBase" current={sortBy} dir={sortDir} />
              </th>
              <th className="px-5 py-3.5 text-right font-semibold text-muted-foreground">Số lượng (Quy đổi)</th>
              <th className="px-5 py-3.5 text-right font-semibold text-muted-foreground">Đơn giá nhập</th>
              <th className="px-5 py-3.5 text-left font-semibold text-muted-foreground">Đối tác / Ghi chú</th>
            </tr>
          </thead>
          <tbody>
            {isLoading ? (
              <tr>
                <td colSpan={6} className="px-5 py-14 text-center text-muted-foreground">
                  <div className="mx-auto size-5 animate-spin rounded-full border-2 border-accent border-t-transparent" />
                </td>
              </tr>
            ) : isError ? (
              <tr>
                <td colSpan={6} className="px-5 py-14 text-center text-destructive">
                  Không thể tải dữ liệu. Vui lòng thử lại.
                </td>
              </tr>
            ) : !data?.items?.length ? (
              <tr>
                <td colSpan={6} className="px-5 py-14 text-center">
                  <Activity className="mx-auto mb-3 size-10 text-muted-foreground/30" />
                  <p className="text-muted-foreground">Chưa có giao dịch nào cho lô này.</p>
                </td>
              </tr>
            ) : (
              data.items.map(txn => {
                const isImport = txn.txnType === 'Import';
                const isDispense = txn.txnType === 'Dispense';
                const isAdjustment = txn.txnType === 'Adjustment';
                const isIncrease = txn.quantityBase > 0;
                
                return (
                  <tr key={txn.transactionId} className="border-b border-border last:border-0 transition-colors hover:bg-secondary/20">
                    <td className="px-5 py-4 text-foreground whitespace-nowrap">
                      {format(new Date(txn.txnDate), 'dd/MM/yyyy HH:mm')}
                    </td>
                    <td className="px-5 py-4">
                      <span className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold ${
                        isImport
                          ? 'bg-emerald-100 text-emerald-800'
                          : isDispense
                          ? 'bg-orange-100 text-orange-800'
                          : 'bg-blue-100 text-blue-800'
                      }`}>
                        {isImport
                          ? <><ArrowDownToLine className="size-3" /> Nhập kho</>
                          : isDispense
                          ? <><ArrowUpFromLine className="size-3" /> Xuất kho</>
                          : <><Edit className="size-3" /> Điều chỉnh</>}
                      </span>
                    </td>
                    <td className={`px-5 py-4 text-right font-semibold ${isImport || (isAdjustment && isIncrease) ? 'text-emerald-600' : 'text-orange-600'}`}>
                      {isImport || (isAdjustment && isIncrease) ? '+' : '-'}{Math.abs(txn.quantityBase).toLocaleString()}
                      {txn.baseUnitName && (
                        <span className="ml-1 text-xs font-normal text-muted-foreground">{txn.baseUnitName}</span>
                      )}
                    </td>
                    <td className="px-5 py-4 text-right text-muted-foreground">
                      {isImport || (isAdjustment && isIncrease) ? '+' : '-'}{Math.abs(txn.quantityInUnit)} {txn.unitName}
                    </td>
                    <td className="px-5 py-4 text-right text-muted-foreground">
                      {isImport && txn.unitImportPrice ? formatCurrency(txn.unitImportPrice) : '—'}
                    </td>
                    <td className="px-5 py-4 text-muted-foreground max-w-[200px] truncate" title={isAdjustment ? (txn.reason || 'Điều chỉnh kho') : (txn.supplierName ?? (txn.prescriptionItemId ? 'Bán thuốc theo đơn' : '—'))}>
                      {isAdjustment 
                        ? (txn.reason || 'Điều chỉnh kho')
                        : (txn.supplierName ?? (txn.prescriptionItemId ? 'Bán thuốc theo đơn' : '—'))}
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      {data && data.totalPages > 1 && (
        <div className="mt-5 flex items-center justify-between text-sm text-muted-foreground">
          <span>
            Trang <span className="font-semibold text-foreground">{page}</span> / {data.totalPages} &nbsp;·&nbsp;
            {((page - 1) * pageSize) + 1}–{Math.min(page * pageSize, data.totalItems)} trên {data.totalItems} giao dịch
          </span>
          <PaginationNumbered
            currentPage={page}
            totalPages={data.totalPages}
            setPage={setPage}
          />
        </div>
      )}
      
      {currentBatch && (
        <AdjustInventoryModal
          isOpen={isAdjustOpen}
          onClose={() => setIsAdjustOpen(false)}
          batchId={currentBatch.batchId}
          medicineName={medicine?.name || ''}
          lotNumber={currentBatch.lotNumber}
          currentQuantity={currentBatch.quantityBase}
          baseUnitName={medicine?.baseUnitName || 'đv'}
        />
      )}
    </div>
  );
}
