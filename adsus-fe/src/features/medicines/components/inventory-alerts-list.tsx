'use client';

import { useState, useMemo } from 'react';
import { useInventoryAlerts } from '@/features/medicines/api/inventory.api';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { AlertCircle, AlertTriangle, PackageX, Target, Calendar, Info } from 'lucide-react';
import Link from 'next/link';
import { PaginationNumbered } from '@/components/ui/pagination-numbered';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';

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
      <div className="p-8 text-center bg-muted/30 rounded-xl border border-dashed">
        <h3 className="text-lg font-medium text-foreground">Kho hoạt động ổn định</h3>
        <p className="text-muted-foreground">Không có cảnh báo nào về số lượng hay hạn sử dụng.</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* 4 Summary Cards */}
      <div className="grid gap-4 md:grid-cols-4">
        <Card className="border-red-200 bg-red-50/30 dark:bg-red-950/20 dark:border-red-900/50">
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-red-800 dark:text-red-300">Đã hết hạn</CardTitle>
            <div className="rounded-full bg-red-100 p-2 dark:bg-red-900/50">
              <Target className="h-4 w-4 text-red-600 dark:text-red-400" />
            </div>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-red-600 dark:text-red-400">{summary.expiredCount}</div>
          </CardContent>
        </Card>
        
        <Card className="border-orange-200 bg-orange-50/30 dark:bg-orange-950/20 dark:border-orange-900/50">
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-orange-800 dark:text-orange-300">Sắp hết hạn</CardTitle>
            <div className="rounded-full bg-orange-100 p-2 dark:bg-orange-900/50">
              <Calendar className="h-4 w-4 text-orange-600 dark:text-orange-400" />
            </div>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-orange-600 dark:text-orange-400">{summary.expiringSoonCount}</div>
          </CardContent>
        </Card>
        
        <Card className="border-amber-200 bg-amber-50/30 dark:bg-amber-950/20 dark:border-amber-900/50">
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-amber-800 dark:text-amber-300">Sắp hết hàng</CardTitle>
            <div className="rounded-full bg-amber-100 p-2 dark:bg-amber-900/50">
              <PackageX className="h-4 w-4 text-amber-600 dark:text-amber-400" />
            </div>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-amber-600 dark:text-amber-400">{summary.lowStockCount}</div>
          </CardContent>
        </Card>

        <Card className="border-blue-200 bg-blue-50/30 dark:bg-blue-950/20 dark:border-blue-900/50">
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-blue-800 dark:text-blue-300">Tổng cảnh báo</CardTitle>
            <div className="rounded-full bg-blue-100 p-2 dark:bg-blue-900/50">
              <Info className="h-4 w-4 text-blue-600 dark:text-blue-400" />
            </div>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-blue-600 dark:text-blue-400">{totalAlerts}</div>
          </CardContent>
        </Card>
      </div>

      <div className="flex flex-col sm:flex-row justify-between gap-4">
        <div className="flex flex-1 items-center space-x-2">
          <Input
            placeholder="Tìm theo tên thuốc, số lô..."
            value={searchTerm}
            onChange={(e) => {
              setSearchTerm(e.target.value);
              setPage(1);
            }}
            className="max-w-sm border-[#E7E8EB]"
          />
          <Select 
            value={alertTypeFilter} 
            onValueChange={(val) => {
              setAlertTypeFilter(val);
              setPage(1);
            }}
          >
            <SelectTrigger className="w-[180px] border-[#E7E8EB]">
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
      </div>

      <Card className="border border-[#E7E8EB] shadow-[0px_0px_35px_0px_rgba(104,134,177,0.15)] rounded-[5px] overflow-hidden bg-[#FFFFFF]">
        <Table>
          <TableHeader className="bg-[#F5F6F8]">
            <TableRow>
              <TableHead className="px-5 py-4 font-semibold text-[#0A1B39]">Tên thuốc</TableHead>
              <TableHead className="px-5 py-4 font-semibold text-[#0A1B39]">Số lô</TableHead>
              <TableHead className="px-5 py-4 font-semibold text-[#0A1B39]">Số lượng</TableHead>
              <TableHead className="px-5 py-4 font-semibold text-[#0A1B39]">Ngày hết hạn</TableHead>
              <TableHead className="px-5 py-4 font-semibold text-[#0A1B39]">Loại cảnh báo</TableHead>
              <TableHead className="px-5 py-4 font-semibold text-[#0A1B39]">Trạng thái</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {paginatedAlerts.length === 0 ? (
              <TableRow>
                <TableCell colSpan={6} className="h-24 text-center text-[#6C7688]">
                  Không tìm thấy cảnh báo nào.
                </TableCell>
              </TableRow>
            ) : (
              paginatedAlerts.map((alert) => (
                <TableRow key={alert.id} className="hover:bg-accent/50 border-b-[#E7E8EB]">
                  <TableCell className="px-5 py-4">
                    <Link href={`/medicines/${alert.medicineId}/batches`} className="font-medium hover:underline text-[#0A1B39]">
                      {alert.medicineName}
                    </Link>
                  </TableCell>
                  <TableCell className="px-5 py-4 text-[#6C7688]">{alert.lotNumber}</TableCell>
                  <TableCell className="px-5 py-4">
                    <span className={alert.alertType === 'LOW_STOCK' ? 'text-[#E04F16] font-medium' : 'text-[#0A1B39]'}>
                      {alert.quantityBase} {alert.baseUnitName}
                    </span>
                  </TableCell>
                  <TableCell className="px-5 py-4 text-[#0A1B39]">
                    {alert.expiryDate ? new Date(alert.expiryDate).toLocaleDateString('vi-VN') : '-'}
                  </TableCell>
                  <TableCell className="px-5 py-4 text-[#6C7688]">
                    {alert.alertType === 'EXPIRED' && 'Đã hết hạn'}
                    {alert.alertType === 'EXPIRING_SOON' && 'Sắp hết hạn'}
                    {alert.alertType === 'LOW_STOCK' && 'Sắp hết hàng'}
                  </TableCell>
                  <TableCell className="px-5 py-4">
                    {alert.alertType === 'EXPIRED' && (
                      <Badge className="bg-[#EF1E1E]/10 text-[#EF1E1E] hover:bg-[#EF1E1E]/20 border-none rounded-full px-3 font-medium">
                        Expired
                      </Badge>
                    )}
                    {alert.alertType === 'EXPIRING_SOON' && (
                      <Badge className="bg-[#E04F16]/10 text-[#E04F16] hover:bg-[#E04F16]/20 border-none rounded-full px-3 font-medium">
                        {`Expiring in ${alert.daysUntilExpiry} days`}
                      </Badge>
                    )}
                    {alert.alertType === 'LOW_STOCK' && (
                      <Badge className="bg-amber-100 text-amber-700 hover:bg-amber-200 border-none rounded-full px-3 font-medium">
                        {alert.severity === 'CRITICAL' ? 'Critical Low' : 'Low Stock'}
                      </Badge>
                    )}
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
        {totalPages > 1 && (
          <div className="p-4 border-t border-[#E7E8EB] flex justify-end">
            <PaginationNumbered
              currentPage={page}
              totalPages={totalPages}
              setPage={setPage}
            />
          </div>
        )}
      </Card>
    </div>
  );
}
