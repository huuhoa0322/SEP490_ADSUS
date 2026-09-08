'use client';

import { useState, useMemo } from 'react';
import { useInventoryAlerts } from '@/features/medicines/api/inventory.api';
import { PackageX, Target, Calendar, Info, Search } from 'lucide-react';
import Link from 'next/link';
import { PaginationNumbered } from '@/components/ui/pagination-numbered';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';

export function InventoryAlertsList() {
  const { data: summary, isLoading, isError } = useInventoryAlerts();
  
  const [page, setPage] = useState(1);
  const pageSize = 10;
  const [searchTerm, setSearchTerm] = useState('');
  const [alertTypeFilter, setAlertTypeFilter] = useState('ALL');

  const combinedAlerts = useMemo(() => {
    if (!summary) return [];
    
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const alerts: any[] = [];
    
    // Process Expiry Alerts
    summary.expiryAlerts.forEach(alert => {
      alerts.push({
        id: `expiry-${alert.batchId}`,
        medicineId: alert.medicineId,
        medicineName: alert.medicineName,
        lotNumber: alert.lotNumber,
        quantityBase: alert.quantityBase,
        baseUnitName: alert.baseUnitName,
        expiryDate: alert.expiryDate,
        daysUntilExpiry: alert.daysUntilExpiry,
        alertType: alert.severity === 'EXPIRED' ? 'EXPIRED' : 'EXPIRING_SOON',
        severity: alert.severity,
      });
    });
    
    // Process Low Stock Alerts
    summary.lowStockAlerts.forEach(alert => {
      alerts.push({
        id: `lowstock-${alert.medicineId}`,
        medicineId: alert.medicineId,
        medicineName: alert.medicineName,
        lotNumber: '-',
        quantityBase: alert.currentStock,
        baseUnitName: alert.baseUnitName,
        expiryDate: null,
        daysUntilExpiry: null,
        alertType: 'LOW_STOCK',
        severity: alert.severity,
      });
    });
    
    return alerts;
  }, [summary]);

  const filteredAlerts = useMemo(() => {
    return combinedAlerts.filter(alert => {
      const matchSearch = alert.medicineName.toLowerCase().includes(searchTerm.toLowerCase()) || 
                          (alert.lotNumber && alert.lotNumber.toLowerCase().includes(searchTerm.toLowerCase()));
      
      const matchType = alertTypeFilter === 'ALL' || alert.alertType === alertTypeFilter;
      
      return matchSearch && matchType;
    });
  }, [combinedAlerts, searchTerm, alertTypeFilter]);

  const paginatedAlerts = useMemo(() => {
    const startIndex = (page - 1) * pageSize;
    return filteredAlerts.slice(startIndex, startIndex + pageSize);
  }, [filteredAlerts, page, pageSize]);

  const totalPages = Math.ceil(filteredAlerts.length / pageSize);

  if (isLoading) {
    return <div className="p-8 text-center text-muted-foreground">Đang tải dữ liệu cảnh báo...</div>;
  }

  if (isError || !summary) {
    return <div className="p-8 text-center text-destructive">Lỗi khi tải dữ liệu cảnh báo.</div>;
  }

  const totalAlerts = summary.expiredCount + summary.expiringSoonCount + summary.lowStockCount;

  if (totalAlerts === 0) {
    return (
      <div className="preclinic-card p-8 text-center">
        <span className="mx-auto mb-3 flex size-12 items-center justify-center rounded-full bg-[var(--status-good)]/12 text-[var(--status-good)]">
          <Target className="size-6" />
        </span>
        <h3 className="font-heading text-lg font-bold text-foreground">Kho hoạt động ổn định</h3>
        <p className="text-muted-foreground">Không có cảnh báo nào về số lượng hay hạn sử dụng.</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* 4 Summary Cards — số thật từ InventoryAlertSummary */}
      <div className="grid gap-4 md:grid-cols-4">
        <div className="preclinic-card flex items-center gap-3.5 p-4">
          <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-destructive/12 text-destructive">
            <Target className="size-5" />
          </span>
          <div>
            <p className="text-xs font-600 uppercase tracking-wide text-muted-foreground">Đã hết hạn</p>
            <p className="font-heading text-2xl font-bold text-destructive">{summary.expiredCount}</p>
          </div>
        </div>

        <div className="preclinic-card flex items-center gap-3.5 p-4">
          <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-[var(--status-warning)]/12 text-[var(--status-warning)]">
            <Calendar className="size-5" />
          </span>
          <div>
            <p className="text-xs font-600 uppercase tracking-wide text-muted-foreground">Sắp hết hạn</p>
            <p className="font-heading text-2xl font-bold text-[var(--status-warning)]">{summary.expiringSoonCount}</p>
          </div>
        </div>

        <div className="preclinic-card flex items-center gap-3.5 p-4">
          <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-[var(--status-warning)]/12 text-[var(--status-warning)]">
            <PackageX className="size-5" />
          </span>
          <div>
            <p className="text-xs font-600 uppercase tracking-wide text-muted-foreground">Sắp hết hàng</p>
            <p className="font-heading text-2xl font-bold text-[var(--status-warning)]">{summary.lowStockCount}</p>
          </div>
        </div>

        <div className="preclinic-card flex items-center gap-3.5 p-4">
          <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary">
            <Info className="size-5" />
          </span>
          <div>
            <p className="text-xs font-600 uppercase tracking-wide text-muted-foreground">Tổng cảnh báo</p>
            <p className="font-heading text-2xl font-bold text-primary">{totalAlerts}</p>
          </div>
        </div>
      </div>

      <div className="flex flex-wrap gap-3">
        <div className="relative min-w-64 flex-1">
          <Search className="pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <input
            placeholder="Tìm theo tên thuốc, số lô..."
            value={searchTerm}
            onChange={(e) => {
              setSearchTerm(e.target.value);
              setPage(1);
            }}
            className="h-12 w-full rounded-full border border-border bg-background pl-11 pr-4 text-[15px] outline-none transition-colors focus:border-accent"
          />
        </div>
        <Select
          value={alertTypeFilter}
          onValueChange={(val) => {
            setAlertTypeFilter(val);
            setPage(1);
          }}
        >
          <SelectTrigger className="h-12 w-[190px] rounded-full border-border bg-background px-5">
            <SelectValue placeholder="Loại cảnh báo" />
          </SelectTrigger>
          <SelectContent position="popper" sideOffset={4}>
            <SelectItem value="ALL">Tất cả cảnh báo</SelectItem>
            <SelectItem value="EXPIRED">Đã hết hạn</SelectItem>
            <SelectItem value="EXPIRING_SOON">Sắp hết hạn</SelectItem>
            <SelectItem value="LOW_STOCK">Sắp hết hàng</SelectItem>
          </SelectContent>
        </Select>
      </div>

      <div className="overflow-hidden overflow-x-auto rounded-3xl border border-border bg-background">
        <table className="w-full min-w-3xl text-left text-sm">
          <thead>
            <tr className="border-b border-border bg-secondary/40">
              <th className="px-5 py-3.5 font-semibold text-muted-foreground">Tên thuốc</th>
              <th className="px-5 py-3.5 font-semibold text-muted-foreground">Số lô</th>
              <th className="px-5 py-3.5 font-semibold text-muted-foreground">Số lượng</th>
              <th className="px-5 py-3.5 font-semibold text-muted-foreground">Ngày hết hạn</th>
              <th className="px-5 py-3.5 font-semibold text-muted-foreground">Mức độ</th>
            </tr>
          </thead>
          <tbody>
            {paginatedAlerts.length === 0 ? (
              <tr>
                <td colSpan={5} className="px-5 py-14 text-center text-muted-foreground">
                  Không tìm thấy cảnh báo nào.
                </td>
              </tr>
            ) : (
              paginatedAlerts.map((alert) => {
                const isCritical = alert.alertType === 'EXPIRED' || alert.severity === 'CRITICAL';
                return (
                <tr
                  key={alert.id}
                  className={`border-b border-border last:border-0 transition-colors hover:bg-secondary/20 ${isCritical ? 'border-l-2 border-l-destructive' : 'border-l-2 border-l-[var(--status-warning)]'}`}
                >
                  <td className="px-5 py-4">
                    <Link href={`/medicines/${alert.medicineId}/batches`} className="font-semibold text-foreground hover:text-primary hover:underline">
                      {alert.medicineName}
                    </Link>
                  </td>
                  <td className="px-5 py-4 text-muted-foreground">{alert.lotNumber}</td>
                  <td className="px-5 py-4 font-mono">
                    <span className={alert.alertType === 'LOW_STOCK' ? 'font-semibold text-[var(--status-warning)]' : 'text-foreground'}>
                      {alert.quantityBase} {alert.baseUnitName}
                    </span>
                  </td>
                  <td className="px-5 py-4 text-foreground">
                    {alert.expiryDate ? new Date(alert.expiryDate).toLocaleDateString('vi-VN') : '—'}
                  </td>
                  <td className="px-5 py-4">
                    {alert.alertType === 'EXPIRED' && (
                      <span className="inline-flex items-center gap-1.5 rounded-full bg-destructive/12 px-3 py-1 text-xs font-600 text-destructive">
                        Hết hạn
                      </span>
                    )}
                    {alert.alertType === 'EXPIRING_SOON' && (
                      <span className="inline-flex items-center gap-1.5 rounded-full bg-[var(--status-warning)]/12 px-3 py-1 text-xs font-600 text-[var(--status-warning)]">
                        <span className="size-1.5 animate-pulse rounded-full bg-[var(--status-warning)]" />
                        Còn {alert.daysUntilExpiry} ngày
                      </span>
                    )}
                    {alert.alertType === 'LOW_STOCK' && (
                      <span className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-600 ${isCritical ? 'bg-destructive/12 text-destructive' : 'bg-[var(--status-warning)]/12 text-[var(--status-warning)]'}`}>
                        {isCritical ? 'Hết hàng nghiêm trọng' : 'Sắp hết hàng'}
                      </span>
                    )}
                  </td>
                </tr>
              );})
            )}
          </tbody>
        </table>
        {totalPages > 1 && (
          <div className="flex justify-end border-t border-border p-4">
            <PaginationNumbered
              currentPage={page}
              totalPages={totalPages}
              setPage={setPage}
            />
          </div>
        )}
      </div>
    </div>
  );
}
