'use client';

import { useState } from 'react';
import { useInventoryAlerts } from '@/features/medicines/api/inventory.api';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { AlertCircle, AlertTriangle, PackageX } from 'lucide-react';
import Link from 'next/link';
import { PaginationNumbered } from '@/components/ui/pagination-numbered';

export function InventoryAlertsList() {
  const { data: summary, isLoading, isError } = useInventoryAlerts();

  const [expiryPage, setExpiryPage] = useState(1);
  const [lowStockPage, setLowStockPage] = useState(1);
  const pageSize = 15;

  if (isLoading) {
    return <div className="p-8 text-center text-muted-foreground">Đang tải dữ liệu cảnh báo...</div>;
  }

  if (isError || !summary) {
    return <div className="p-8 text-center text-destructive">Lỗi khi tải dữ liệu cảnh báo.</div>;
  }

  if (summary.lowStockCount === 0 && summary.expiringSoonCount === 0 && summary.expiredCount === 0) {
    return (
      <div className="p-8 text-center bg-muted/30 rounded-xl border border-dashed">
        <h3 className="text-lg font-medium text-foreground">Kho hoạt động ổn định</h3>
        <p className="text-muted-foreground">Không có cảnh báo nào về số lượng hay hạn sử dụng.</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="grid gap-4 md:grid-cols-3">
        <Card className="border-red-200 bg-red-50/30 dark:bg-red-950/20 dark:border-red-900/50">
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-red-800 dark:text-red-300">Đã hết hạn</CardTitle>
            <PackageX className="h-4 w-4 text-red-600 dark:text-red-400" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-red-600 dark:text-red-400">{summary.expiredCount}</div>
          </CardContent>
        </Card>
        <Card className="border-amber-200 bg-amber-50/30 dark:bg-amber-950/20 dark:border-amber-900/50">
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-amber-800 dark:text-amber-300">Sắp hết hạn</CardTitle>
            <AlertTriangle className="h-4 w-4 text-amber-600 dark:text-amber-400" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-amber-600 dark:text-amber-400">{summary.expiringSoonCount}</div>
          </CardContent>
        </Card>
        <Card className="border-orange-200 bg-orange-50/30 dark:bg-orange-950/20 dark:border-orange-900/50">
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-orange-800 dark:text-orange-300">Sắp hết hàng</CardTitle>
            <AlertCircle className="h-4 w-4 text-orange-600 dark:text-orange-400" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-orange-600 dark:text-orange-400">{summary.lowStockCount}</div>
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-6 md:grid-cols-2">
        {/* Expiry Alerts */}
        <Card>
          <CardHeader>
            <CardTitle>Cảnh báo hạn sử dụng</CardTitle>
          </CardHeader>
          <CardContent>
            {summary.expiryAlerts.length === 0 ? (
              <p className="text-sm text-muted-foreground">Không có lô thuốc nào sắp hết hạn.</p>
            ) : (
              <div className="space-y-4">
                {summary.expiryAlerts.slice((expiryPage - 1) * pageSize, expiryPage * pageSize).map((alert) => (
                  <div key={alert.batchId} className="flex flex-col space-y-1 p-3 rounded-lg border bg-card hover:bg-accent/50 transition-colors">
                    <div className="flex items-center justify-between">
                      <Link href={`/admin/medicines/${alert.medicineId}/batches`} className="font-medium hover:underline">
                        {alert.medicineName}
                      </Link>
                      <Badge variant={alert.severity === 'EXPIRED' ? 'destructive' : alert.severity === 'CRITICAL' ? 'destructive' : 'secondary'} className={alert.severity === 'WARNING' ? 'bg-amber-500 text-white' : ''}>
                        {alert.severity === 'EXPIRED' ? 'Đã hết hạn' : `${alert.daysUntilExpiry} ngày`}
                      </Badge>
                    </div>
                    <div className="flex justify-between text-sm text-muted-foreground">
                      <span>Lô: {alert.lotNumber}</span>
                      <span>Tồn: {alert.quantityBase} {alert.baseUnitName}</span>
                    </div>
                  </div>
                ))}
                
                <PaginationNumbered
                  currentPage={expiryPage}
                  totalPages={Math.ceil(summary.expiryAlerts.length / pageSize)}
                  setPage={setExpiryPage}
                  className="pt-4 border-t mt-4 justify-end"
                />
              </div>
            )}
          </CardContent>
        </Card>

        {/* Low Stock Alerts */}
        <Card>
          <CardHeader>
            <CardTitle>Cảnh báo sắp hết hàng</CardTitle>
          </CardHeader>
          <CardContent>
            {summary.lowStockAlerts.length === 0 ? (
              <p className="text-sm text-muted-foreground">Không có thuốc nào sắp hết hàng.</p>
            ) : (
              <div className="space-y-4">
                {summary.lowStockAlerts.slice((lowStockPage - 1) * pageSize, lowStockPage * pageSize).map((alert) => (
                  <div key={alert.medicineId} className="flex flex-col space-y-1 p-3 rounded-lg border bg-card hover:bg-accent/50 transition-colors">
                    <div className="flex items-center justify-between">
                      <Link href={`/admin/medicines/${alert.medicineId}`} className="font-medium hover:underline">
                        {alert.medicineName}
                      </Link>
                      <Badge variant={alert.severity === 'CRITICAL' ? 'destructive' : 'secondary'} className={alert.severity === 'WARNING' ? 'bg-amber-500 text-white' : ''}>
                        {alert.severity}
                      </Badge>
                    </div>
                    <div className="flex justify-between text-sm">
                      <span className="text-destructive font-medium">Tồn: {alert.currentStock} {alert.baseUnitName}</span>
                      <span className="text-muted-foreground">Ngưỡng: {alert.threshold}</span>
                    </div>
                  </div>
                ))}
                
                <PaginationNumbered
                  currentPage={lowStockPage}
                  totalPages={Math.ceil(summary.lowStockAlerts.length / pageSize)}
                  setPage={setLowStockPage}
                  className="pt-4 border-t mt-4 justify-end"
                />
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
