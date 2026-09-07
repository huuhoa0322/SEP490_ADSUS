'use client';

import { useState, useCallback } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { format } from 'date-fns';
import { useDebounce } from 'use-debounce';
import {
  ArrowLeft, Search, Package, Clock, ChevronUp, ChevronDown, ChevronsUpDown,
  AlertTriangle,
} from 'lucide-react';
import { usePagedMedicineBatches } from '@/features/medicines/api/inventory.api';
import { useMedicineById } from '@/features/medicines/hooks/use-medicines';
import { formatCurrency } from '@/lib/utils';
import { PaginationNumbered } from '@/components/ui/pagination-numbered';

type SortKey = 'expiryDate' | 'quantityBase' | 'avgPrice';

function SortIcon({ field, current, dir }: { field: SortKey; current: SortKey; dir: 'asc' | 'desc' }) {
  if (field !== current) return <ChevronsUpDown className="ml-1 inline size-3.5 text-muted-foreground/40" />;
  return dir === 'asc'
    ? <ChevronUp className="ml-1 inline size-3.5 text-accent" />
    : <ChevronDown className="ml-1 inline size-3.5 text-accent" />;
}

export default function MedicineBatchesPage() {
  const { id: medicineId } = useParams<{ id: string }>();
  const router = useRouter();

  const [searchInput, setSearchInput] = useState('');
  const [debouncedSearch] = useDebounce(searchInput, 400);
  const [page, setPage] = useState(1);
  const pageSize = 10;
  const [sortBy, setSortBy] = useState<SortKey>('expiryDate');
  const [sortDir, setSortDir] = useState<'asc' | 'desc'>('asc');

  const { data: medicine } = useMedicineById(medicineId);

  const { data, isLoading, isError } = usePagedMedicineBatches({
    medicineId,
    search: debouncedSearch || undefined,
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
      setSortDir('asc');
    }
    setPage(1);
  }, [sortBy]);

  const handleSearch = (val: string) => {
    setSearchInput(val);
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
        <div>
          <p className="text-sm text-muted-foreground">Quản lý danh mục thuốc</p>
          <h1 className="font-heading text-2xl font-bold tracking-tight text-foreground">
            Danh sách lô — {medicine?.name ?? 'Đang tải...'}
          </h1>
        </div>
      </div>

      {/* Toolbar */}
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <div className="relative flex-1 min-w-[260px] max-w-md">
          <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <input
            type="text"
            placeholder="Tìm theo mã lô..."
            value={searchInput}
            onChange={e => handleSearch(e.target.value)}
            className="w-full h-10 rounded-full border border-border bg-background py-2 pl-10 pr-4 text-sm outline-none transition-colors focus:border-accent"
          />
        </div>
        {data && (
          <p className="ml-auto text-sm text-muted-foreground">
            Tổng <span className="font-semibold text-foreground">{data.totalItems}</span> lô
          </p>
        )}
      </div>

      {/* Table */}
      <div className="overflow-hidden rounded-2xl border border-border bg-white shadow-sm">
        <table className="w-full text-sm">
          <thead className="bg-secondary/40">
            <tr className="border-b border-border">
              <th className="px-5 py-3.5 text-left font-semibold text-muted-foreground">Mã lô</th>
              <th
                className="px-5 py-3.5 text-left font-semibold text-muted-foreground cursor-pointer select-none hover:text-foreground"
                onClick={() => handleSort('expiryDate')}
              >
                Hạn sử dụng <SortIcon field="expiryDate" current={sortBy} dir={sortDir} />
              </th>
              <th
                className="px-5 py-3.5 text-right font-semibold text-muted-foreground cursor-pointer select-none hover:text-foreground"
                onClick={() => handleSort('quantityBase')}
              >
                Tồn kho <SortIcon field="quantityBase" current={sortBy} dir={sortDir} />
              </th>
              <th
                className="px-5 py-3.5 text-right font-semibold text-muted-foreground cursor-pointer select-none hover:text-foreground"
                onClick={() => handleSort('avgPrice')}
              >
                Giá nhập TB <SortIcon field="avgPrice" current={sortBy} dir={sortDir} />
              </th>
              <th className="px-5 py-3.5 text-center font-semibold text-muted-foreground">Lịch sử</th>
            </tr>
          </thead>
          <tbody>
            {isLoading ? (
              <tr>
                <td colSpan={5} className="px-5 py-14 text-center text-muted-foreground">
                  <div className="mx-auto size-5 animate-spin rounded-full border-2 border-accent border-t-transparent" />
                </td>
              </tr>
            ) : isError ? (
              <tr>
                <td colSpan={5} className="px-5 py-14 text-center text-destructive">
                  Không thể tải dữ liệu. Vui lòng thử lại.
                </td>
              </tr>
            ) : !data?.items?.length ? (
              <tr>
                <td colSpan={5} className="px-5 py-14 text-center">
                  <Package className="mx-auto mb-3 size-10 text-muted-foreground/30" />
                  <p className="text-muted-foreground">
                    {debouncedSearch ? `Không tìm thấy lô nào khớp với "${debouncedSearch}".` : 'Thuốc này chưa có lô tồn kho.'}
                  </p>
                </td>
              </tr>
            ) : (
              (() => {
                const now = new Date();
                const thirtyDaysFromNow = new Date(now.getTime() + 30 * 24 * 60 * 60 * 1000);
                
                return data.items.map(batch => {
                  const expiry = new Date(batch.expiryDate);
                  const isExpired = expiry < now;
                  const soonExpiry = !isExpired && expiry < thirtyDaysFromNow;

                return (
                  <tr key={batch.batchId} className="border-b border-border last:border-0 transition-colors hover:bg-secondary/20">
                    <td className="px-5 py-4 font-semibold text-foreground">{batch.lotNumber}</td>
                    <td className="px-5 py-4">
                      <span className={isExpired ? 'text-destructive font-semibold' : soonExpiry ? 'text-amber-600 font-medium' : 'text-foreground'}>
                        {format(new Date(batch.expiryDate), 'dd/MM/yyyy')}
                      </span>
                      {isExpired && (
                        <span className="ml-2 inline-flex items-center gap-1 rounded-full bg-destructive/10 px-2 py-0.5 text-xs font-medium text-destructive">
                          <AlertTriangle className="size-3" /> Đã HSD
                        </span>
                      )}
                      {soonExpiry && (
                        <span className="ml-2 inline-flex items-center gap-1 rounded-full bg-amber-50 px-2 py-0.5 text-xs font-medium text-amber-600">
                          <AlertTriangle className="size-3" /> Sắp HSD
                        </span>
                      )}
                    </td>
                    <td className="px-5 py-4 text-right font-semibold text-emerald-600">
                      {batch.quantityBase.toLocaleString()}
                      <span className="ml-1 text-sm font-normal text-muted-foreground">{batch.usageUnit || 'đv'}</span>
                    </td>
                    <td className="px-5 py-4 text-right text-muted-foreground">
                      {formatCurrency(batch.baseUnitAvgImportPrice)}
                      <span className="ml-1 text-xs">/ {batch.usageUnit || 'đv'}</span>
                    </td>
                    <td className="px-5 py-4 text-center">
                      <button
                        onClick={() => router.push(`/medicines/${medicineId}/batches/${batch.batchId}`)}
                        title="Xem lịch sử giao dịch lô này"
                        className="inline-flex size-8 items-center justify-center rounded-full text-blue-600 transition-colors hover:bg-blue-50"
                      >
                        <Clock className="size-4" />
                      </button>
                    </td>
                  </tr>
                );
                });
              })()
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      {data && data.totalPages > 1 && (
        <div className="mt-5 flex items-center justify-between text-sm text-muted-foreground">
          <span>
            Trang <span className="font-semibold text-foreground">{page}</span> / {data.totalPages} &nbsp;·&nbsp;
            {((page - 1) * pageSize) + 1}–{Math.min(page * pageSize, data.totalItems)} trên {data.totalItems} lô
          </span>
          <PaginationNumbered
            currentPage={page}
            totalPages={data.totalPages}
            setPage={setPage}
          />
        </div>
      )}
    </div>
  );
}
