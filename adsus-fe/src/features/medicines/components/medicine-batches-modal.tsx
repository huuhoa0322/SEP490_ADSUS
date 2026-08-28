"use client";

import { Loader2, Calendar, FileText, Package, Clock } from "lucide-react";
import { format } from "date-fns";
import { useState } from "react";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { useMedicineBatches } from "../api/inventory.api";
import { formatCurrency } from "@/lib/utils";
import type { MedicineResponse } from "../api/medicines-api";
import { BatchHistoryModal } from "./batch-history-modal";

interface MedicineBatchesModalProps {
  medicine: MedicineResponse | null;
  isOpen: boolean;
  onClose: () => void;
}

export function MedicineBatchesModal({
  medicine,
  isOpen,
  onClose,
}: MedicineBatchesModalProps) {
  const [selectedBatchId, setSelectedBatchId] = useState<string | null>(null);
  
  const { data: batches, isLoading } = useMedicineBatches(medicine?.medicineId ?? "");

  if (!medicine) return null;

  return (
    <>
      <Dialog open={isOpen} onOpenChange={(open) => !open && onClose()}>
        <DialogContent className="sm:max-w-4xl">
          <DialogHeader>
            <DialogTitle className="text-xl font-heading text-foreground">
              Chi tiết các lô - {medicine.name}
            </DialogTitle>
          </DialogHeader>
          
          <div className="mt-4">
            {isLoading ? (
              <div className="flex h-40 items-center justify-center">
                <Loader2 className="size-6 animate-spin text-muted-foreground" />
              </div>
            ) : !batches || batches.length === 0 ? (
              <div className="flex flex-col items-center justify-center rounded-xl border border-dashed border-border py-12">
                <Package className="mb-4 size-10 text-muted-foreground/50" />
                <p className="text-sm font-medium text-muted-foreground">
                  Thuốc này hiện không có tồn kho.
                </p>
              </div>
            ) : (
              <div className="overflow-hidden rounded-xl border border-border">
                <table className="w-full text-sm">
                  <thead className="bg-secondary/50">
                    <tr className="border-b border-border">
                      <th className="px-4 py-3 text-left font-semibold text-muted-foreground">Mã lô</th>
                      <th className="px-4 py-3 text-left font-semibold text-muted-foreground">Hạn sử dụng</th>
                      <th className="px-4 py-3 text-right font-semibold text-muted-foreground">Số lượng tồn</th>
                      <th className="px-4 py-3 text-right font-semibold text-muted-foreground">Giá nhập TB</th>
                      <th className="px-4 py-3 text-center font-semibold text-muted-foreground">Lịch sử</th>
                    </tr>
                  </thead>
                  <tbody>
                    {batches.map((batch) => {
                      const isExpired = new Date(batch.expiryDate) < new Date();
                      
                      return (
                        <tr key={batch.batchId} className="border-b border-border last:border-0 hover:bg-secondary/20">
                          <td className="px-4 py-3 font-medium text-foreground">
                            {batch.lotNumber}
                          </td>
                          <td className="px-4 py-3">
                            <span className={isExpired ? "text-destructive font-medium" : "text-foreground"}>
                              {format(new Date(batch.expiryDate), "dd/MM/yyyy")}
                            </span>
                            {isExpired && <span className="ml-2 text-xs text-destructive">(Đã HSD)</span>}
                          </td>
                          <td className="px-4 py-3 text-right font-semibold text-emerald-600">
                            {batch.quantityBase} {medicine.usageUnit || "đv"}
                          </td>
                          <td className="px-4 py-3 text-right text-muted-foreground">
                            {formatCurrency(batch.baseUnitAvgImportPrice)} / {medicine.usageUnit || "đv"}
                          </td>
                          <td className="px-4 py-3 text-center">
                            <button
                              onClick={() => setSelectedBatchId(batch.batchId)}
                              title="Xem lịch sử xuất nhập"
                              className="inline-flex size-8 items-center justify-center rounded-full text-blue-600 transition-colors hover:bg-blue-50"
                            >
                              <Clock className="size-4" />
                            </button>
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
      
      <BatchHistoryModal
        batchId={selectedBatchId}
        isOpen={!!selectedBatchId}
        onClose={() => setSelectedBatchId(null)}
        medicineName={medicine.name}
      />
    </>
  );
}
