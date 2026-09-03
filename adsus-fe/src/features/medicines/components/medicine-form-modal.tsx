"use client";

import React, { useState } from "react";
import { Loader2 } from "lucide-react";
import toast from "react-hot-toast";

import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { getApiErrorMessage } from "@/lib/api-client";
import { useMedicineUnits, useCreateMedicine, useUpdateMedicine } from "../hooks/use-medicines";
import type { MedicineResponse } from "../api/medicines-api";

interface MedicineFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  medicineToEdit: MedicineResponse | null;
  onSuccessCreate?: (medicine: MedicineResponse) => void;
}

export function MedicineFormModal({ isOpen, onClose, medicineToEdit, onSuccessCreate }: MedicineFormModalProps) {
  const { data: units = [] } = useMedicineUnits();
  const createMutation = useCreateMedicine();
  const updateMutation = useUpdateMedicine();

  const [name, setName] = useState("");
  const [medicineUnitId, setMedicineUnitId] = useState("");
  const [salePrice, setSalePrice] = useState("");
  const [isLiquid, setIsLiquid] = useState(false);
  const [usageUnit, setUsageUnit] = useState("");
  const [volume, setVolume] = useState("1");
  const [lowStockThreshold, setLowStockThreshold] = useState("0");

  // Reset form when modal opens or editing changes
  const [prevIsOpen, setPrevIsOpen] = useState(isOpen);
  const [prevMedicineId, setPrevMedicineId] = useState(medicineToEdit?.medicineId);
  if (isOpen !== prevIsOpen || medicineToEdit?.medicineId !== prevMedicineId) {
    setPrevIsOpen(isOpen);
    setPrevMedicineId(medicineToEdit?.medicineId);
    if (isOpen) {
      if (medicineToEdit) {
        setName(medicineToEdit.name);
        setUsageUnit(medicineToEdit.usageUnit || "");
        setVolume(medicineToEdit.volumePerBaseUnit ? medicineToEdit.volumePerBaseUnit.toString() : "1");
        setIsLiquid(medicineToEdit.volumePerBaseUnit !== 1 && medicineToEdit.volumePerBaseUnit != null);
        setLowStockThreshold(medicineToEdit.lowStockThreshold?.toString() || "0");
      } else {
        setName("");
        setMedicineUnitId("");
        setSalePrice("0");
        setIsLiquid(false);
        setUsageUnit("");
        setVolume("1");
        setLowStockThreshold("0");
      }
    }
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      toast.error("Vui lòng nhập tên thuốc");
      return;
    }

    if (!medicineToEdit) {
      if (!medicineUnitId) {
        toast.error("Vui lòng chọn Đơn vị cơ sở");
        return;
      }
      
      const priceVal = parseFloat(salePrice);
      if (isNaN(priceVal) || priceVal < 0) {
        toast.error("Giá bán không hợp lệ");
        return;
      }
    }

    const thresholdVal = parseInt(lowStockThreshold, 10);
    if (isNaN(thresholdVal) || thresholdVal < 0) {
      toast.error("Ngưỡng cảnh báo hết hàng không hợp lệ");
      return;
    }

    let finalUsageUnit = usageUnit.trim();
    let finalVolume = parseFloat(volume);

    if (!isLiquid) {
      // Smart Defaults: usage unit = base unit name, volume = 1
      const selectedUnit = units.find((u: { medicineUnitId: string; name: string }) => u.medicineUnitId === medicineUnitId);
      finalUsageUnit = selectedUnit ? selectedUnit.name : "";
      finalVolume = 1;
    }

    if (isLiquid && (!finalUsageUnit || isNaN(finalVolume) || finalVolume <= 0)) {
      toast.error("Vui lòng nhập đúng Đơn vị dùng và Hàm lượng (Dung tích)");
      return;
    }

    try {
      if (medicineToEdit) {
        await updateMutation.mutateAsync({
          id: medicineToEdit.medicineId,
          request: { 
            name: name.trim(),
            usageUnit: finalUsageUnit,
            volumePerBaseUnit: finalVolume,
            lowStockThreshold: thresholdVal
          },
        });
        toast.success("Cập nhật thuốc thành công");
        onClose();
      } else {
        const newMed = await createMutation.mutateAsync({ 
          name: name.trim(),
          medicineUnitId,
          salePrice: parseFloat(salePrice) || 0,
          usageUnit: finalUsageUnit,
          volumePerBaseUnit: finalVolume,
          lowStockThreshold: thresholdVal
        });
        toast.success("Thêm thuốc thành công");
        onClose();
        if (onSuccessCreate) {
          onSuccessCreate(newMed as unknown as MedicineResponse);
        }
      }
    } catch (error) {
      toast.error(getApiErrorMessage(error, "Có lỗi xảy ra"));
    }
  };

  const isPending = createMutation.isPending || updateMutation.isPending;

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{medicineToEdit ? "Sửa thông tin thuốc" : "Thêm thuốc mới (Master Data)"}</DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-6 mt-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Tên thuốc <span className="text-red-500">*</span></Label>
              <Input 
                value={name} 
                onChange={e => setName(e.target.value)} 
                placeholder="VD: Prospan Syrup 100ml" 
                disabled={!!medicineToEdit}
                autoFocus
              />
            </div>
            
            <div className="space-y-2">
              <Label>Ngưỡng cảnh báo hết hàng (Tính theo Đơn vị tồn kho)</Label>
              <Input 
                type="number"
                min="0"
                value={lowStockThreshold} 
                onChange={e => setLowStockThreshold(e.target.value)} 
                placeholder="Nhập 0 để bỏ qua theo dõi"
                className="bg-orange-50/50 border-orange-200"
              />
            </div>
          </div>

          {!medicineToEdit && (
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 bg-gray-50 p-4 rounded-md border">
              <div className="space-y-2">
                <Label>Đơn vị cơ sở (Tồn kho) <span className="text-red-500">*</span></Label>
                <Select value={medicineUnitId} onValueChange={setMedicineUnitId}>
                  <SelectTrigger className="bg-white">
                    <SelectValue placeholder="Chọn đơn vị vật lý" />
                  </SelectTrigger>
                  <SelectContent>
                    {units.map((u: { medicineUnitId: string; name: string }) => (
                      <SelectItem key={u.medicineUnitId} value={u.medicineUnitId}>{u.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <Label>Giá bán mặc định (VNĐ)</Label>
                <Input 
                  type="number"
                  min="0"
                  value={salePrice}
                  onChange={e => setSalePrice(e.target.value)}
                  className="bg-white font-mono text-green-700 font-medium"
                />
              </div>
            </div>
          )}

          <div className="border rounded-md p-4 space-y-4">
            <div className="flex items-center space-x-3">
              <Checkbox 
                id="isLiquid" 
                checked={isLiquid} 
                onCheckedChange={(c) => setIsLiquid(!!c)} 
                className="h-5 w-5"
              />
              <Label htmlFor="isLiquid" className="font-medium cursor-pointer text-base">
                Thuốc dạng lỏng/mỡ (Bác sĩ kê theo liều nhỏ ml, g...)
              </Label>
            </div>

            {isLiquid ? (
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mt-2">
                <div className="space-y-2">
                  <Label>Đơn vị dùng (Usage Unit)</Label>
                  <Input 
                    value={usageUnit} 
                    onChange={e => setUsageUnit(e.target.value)} 
                    placeholder="VD: ml, g, giọt..." 
                  />
                </div>
                <div className="space-y-2">
                  <Label>Hàm lượng (Tính trên 1 Đơn vị cơ sở)</Label>
                  <Input 
                    type="number" 
                    min="0.01" 
                    step="0.01"
                    value={volume} 
                    onChange={e => setVolume(e.target.value)} 
                    placeholder="VD: 100 (vì 1 Chai = 100 ml)" 
                  />
                </div>
              </div>
            ) : (
              <p className="text-sm text-gray-500 pl-8">
                Mặc định: Hệ thống tự hiểu 1 Đơn vị cơ sở = 1 Đơn vị dùng (Ví dụ 1 Viên = 1 Viên).
              </p>
            )}
          </div>

          <div className="flex justify-end gap-3 pt-4 border-t">
            <Button type="button" variant="outline" onClick={onClose} disabled={isPending}>
              Hủy
            </Button>
            <Button type="submit" disabled={isPending}>
              {isPending && <Loader2 className="w-4 h-4 mr-2 animate-spin" />}
              {medicineToEdit ? "Cập nhật" : "Tạo Master Data"}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
