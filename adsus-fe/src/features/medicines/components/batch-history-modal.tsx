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
                    
                    return (
                      <tr key={txn.transactionId} className="border-b border-border last:border-0 hover:bg-secondary/20">
                        <td className="px-4 py-3 text-foreground whitespace-nowrap">
                          {format(new Date(txn.txnDate), "dd/MM/yyyy HH:mm")}
                        </td>
                        <td className="px-4 py-3">
                          <Badge variant={isImport ? "default" : "secondary"} className={isImport ? "bg-emerald-100 text-emerald-800 hover:bg-emerald-100" : "bg-orange-100 text-orange-800 hover:bg-orange-100"}>
                            {isImport ? (
                              <><ArrowDownToLine className="mr-1 size-3" /> Nhập kho</>
                            ) : (
                              <><ArrowUpFromLine className="mr-1 size-3" /> Xuất kho</>
                            )}
                          </Badge>
                        </td>
                        <td className={`px-4 py-3 text-right font-semibold ${isImport ? "text-emerald-600" : "text-orange-600"}`}>
                          {isImport ? "+" : "-"}{txn.quantityBase}
                        </td>
                        <td className="px-4 py-3 text-right text-muted-foreground">
                          {txn.quantityInUnit} {txn.unitName}
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
