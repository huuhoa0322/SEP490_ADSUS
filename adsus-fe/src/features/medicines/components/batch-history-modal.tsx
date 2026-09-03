"use client";

import { Loader2, ArrowDownToLine, ArrowUpFromLine, Activity } from "lucide-react";
import { format } from "date-fns";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { useInventoryHistory } from "../api/inventory.api";
import { formatCurrency } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";

interface BatchHistoryModalProps {
  batchId: string | null;
  isOpen: boolean;
  onClose: () => void;
  medicineName: string;
}

export function BatchHistoryModal({
  batchId,
  isOpen,
  onClose,
  medicineName,
}: BatchHistoryModalProps) {
  
  // Always query if batchId exists, we can use page=1, pageSize=100 for a simple view inside modal
  const { data: historyData, isLoading } = useInventoryHistory({
    batchId: batchId || undefined,
    page: 1,
    pageSize: 100
  });

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-4xl max-h-[80vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="text-xl font-heading text-foreground">
            Lịch sử giao dịch Lô - {medicineName}
          </DialogTitle>
        </DialogHeader>
        
        <div className="mt-4">
          {isLoading ? (
            <div className="flex h-40 items-center justify-center">
              <Loader2 className="size-6 animate-spin text-muted-foreground" />
            </div>
          ) : !historyData || historyData.items.length === 0 ? (
            <div className="flex flex-col items-center justify-center rounded-xl border border-dashed border-border py-12">
              <Activity className="mb-4 size-10 text-muted-foreground/50" />
              <p className="text-sm font-medium text-muted-foreground">
                Chưa có lịch sử giao dịch nào cho lô này.
              </p>
            </div>
          ) : (
            <div className="overflow-hidden rounded-xl border border-border">
              <table className="w-full text-sm">
                <thead className="bg-secondary/50">
                  <tr className="border-b border-border">
                    <th className="px-4 py-3 text-left font-semibold text-muted-foreground">Thời gian</th>
                    <th className="px-4 py-3 text-left font-semibold text-muted-foreground">Loại</th>
                    <th className="px-4 py-3 text-right font-semibold text-muted-foreground">Số lượng (ĐV Cơ sở)</th>
                    <th className="px-4 py-3 text-right font-semibold text-muted-foreground">Số lượng (ĐV Quy đổi)</th>
                    <th className="px-4 py-3 text-right font-semibold text-muted-foreground">Đơn giá nhập</th>
                    <th className="px-4 py-3 text-left font-semibold text-muted-foreground">Đối tác / Ghi chú</th>
                  </tr>
                </thead>
                <tbody>
                  {historyData.items.map((txn) => {
                    const isImport = txn.txnType === "Import";
                    const isDispense = txn.txnType === "Dispense";
                    const isAdjustment = txn.txnType === "Adjustment";
                    const isIncrease = txn.quantityBase > 0;
                    const isPositive = isImport || (!isDispense && isIncrease);
                    
                    return (
                      <tr key={txn.transactionId} className="border-b border-border last:border-0 hover:bg-secondary/20">
                        <td className="px-4 py-3 text-foreground whitespace-nowrap">
                          {format(new Date(txn.txnDate), "dd/MM/yyyy HH:mm")}
                        </td>
                        <td className="px-4 py-3">
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
                              : <><Activity className="size-3" /> Điều chỉnh</>}
                          </span>
                        </td>
                        <td className={`px-4 py-3 text-right font-semibold ${isPositive ? "text-emerald-600" : "text-orange-600"}`}>
                          {isPositive ? "+" : "-"}{Math.abs(txn.quantityBase)}
                        </td>
                        <td className="px-4 py-3 text-right text-muted-foreground">
                          {isPositive ? "+" : "-"}{Math.abs(txn.quantityInUnit)} {txn.unitName}
                        </td>
                        <td className="px-4 py-3 text-right text-muted-foreground">
                          {isImport && txn.unitImportPrice ? formatCurrency(txn.unitImportPrice) : "-"}
                        </td>
                        <td className="px-4 py-3 text-muted-foreground">
                          {txn.supplierName || (txn.prescriptionItemId ? "Bán thuốc theo đơn" : "-")}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
