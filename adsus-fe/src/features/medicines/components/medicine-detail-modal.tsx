"use client";

import { Loader2, Plus, Trash2, Edit2, CheckCircle2, Save, Package, Info } from "lucide-react";
import { useState, useEffect } from "react";
import toast from "react-hot-toast";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import { Badge } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";


import {
  useMedicineUnits,
  useMedicinePackagings,
  useAddPackaging,
  useUpdatePackaging,
  useDeletePackaging,
  useUpdateMedicine
} from "../hooks/use-medicines";
import type { MedicinePackagingResponse, MedicineResponse } from "../api/medicines-api";
import { getApiErrorMessage } from "@/lib/api-client";
import { ConfirmDialog } from "@/features/user-role-management/components/confirm-dialog";

interface Props {
  medicine: MedicineResponse;
  isOpen: boolean;
  onClose: () => void;
}

export function MedicineDetailModal({ medicine, isOpen, onClose }: Props) {
  // === Data hooks ===
  const { data: units = [] } = useMedicineUnits();
  const { data: packagings = [], isLoading } = useMedicinePackagings(medicine.medicineId);
  const updateMedicineMutation = useUpdateMedicine();
  const addMutation = useAddPackaging();
  const updateMutation = useUpdatePackaging();
  const deleteMutation = useDeletePackaging();

  // === General Info State ===
  const [usageUnit, setUsageUnit] = useState(medicine.usageUnit || "");
  const [volume, setVolume] = useState(medicine.volumePerBaseUnit ? medicine.volumePerBaseUnit.toString() : "");

  // Sync general info state when medicine changes
  useEffect(() => {
    if (isOpen) {
      setUsageUnit(medicine.usageUnit || "");
      setVolume(medicine.volumePerBaseUnit ? medicine.volumePerBaseUnit.toString() : "");
    }
  }, [medicine, isOpen]);

  const handleUpdateGeneralInfo = async () => {
    const finalVolume = parseFloat(volume);
    const finalUsageUnit = usageUnit.trim();

      if (finalUsageUnit && (isNaN(finalVolume) || finalVolume <= 0)) {
        toast.error("Vui lòng nhập đúng Hàm lượng (lớn hơn 0) khi đã nhập Đơn vị dùng.");
        return;
      }
      
      if (!isNaN(finalVolume) && finalVolume > 0 && !finalUsageUnit) {
        toast.error("Vui lòng nhập Đơn vị dùng (Usage Unit) khi đã nhập Hàm lượng.");
        return;
      }

    try {
      await updateMedicineMutation.mutateAsync({
        id: medicine.medicineId,
        request: {
          name: medicine.name,
          usageUnit: finalUsageUnit,
          volumePerBaseUnit: isNaN(finalVolume) ? undefined : finalVolume
        }
      });
      toast.success("Cập nhật thông tin cơ bản thành công.");
    } catch (error) {
      toast.error(getApiErrorMessage(error, "Có lỗi xảy ra"));
    }
  };

  // === Packaging State ===
  const [editingId, setEditingId] = useState<string | null>(null);
  const [unitId, setUnitId] = useState("");
  const [conversion, setConversion] = useState("1");
  const [price, setPrice] = useState("0");
  const [isBase, setIsBase] = useState(false);
  const [isSellable, setIsSellable] = useState(true);

  const resetPackagingForm = () => {
    setEditingId(null);
    setUnitId("");
    setConversion("1");
    setPrice("0");
    setIsBase(false);
    setIsSellable(true);
  };

  const handleEditPackaging = (p: MedicinePackagingResponse) => {
    setEditingId(p.id);
    setUnitId(p.medicineUnitId);
    setConversion(p.conversionFactor.toString());
    setPrice(p.salePrice.toString());
    setIsBase(p.isBaseUnit);
    setIsSellable(p.isSellable);
  };

  const handleSubmitPackaging = (e: React.FormEvent) => {
    e.preventDefault();
    if (!unitId) {
      toast.error("Vui lòng chọn đơn vị.");
      return;
    }
    
    const req = {
      medicineUnitId: unitId,
      conversionFactor: parseInt(conversion) || 1,
      salePrice: parseFloat(price) || 0,
      isBaseUnit: isBase,
      isSellable: isSellable,
    };

    if (editingId) {
      updateMutation.mutate(
        { packagingId: editingId, request: req },
        {
          onSuccess: () => {
            toast.success("Cập nhật quy cách thành công.");
            resetPackagingForm();
          },
          onError: (err) => toast.error(getApiErrorMessage(err, "Có lỗi xảy ra")),
        }
      );
    } else {
      addMutation.mutate(
        { medicineId: medicine.medicineId, request: req },
        {
          onSuccess: () => {
            toast.success("Thêm quy cách mới thành công.");
            resetPackagingForm();
          },
          onError: (err) => toast.error(getApiErrorMessage(err, "Có lỗi xảy ra")),
        }
      );
    }
  };

  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null);

  const handleDeletePackaging = (id: string) => {
    setPendingDeleteId(id);
  };

  const handleConfirmDelete = async () => {
    if (!pendingDeleteId) return;
    try {
      await deleteMutation.mutateAsync(pendingDeleteId);
      toast.success("Đã xóa quy cách đóng gói.");
    } catch (err) {
      toast.error(getApiErrorMessage(err, "Có lỗi xảy ra"));
    } finally {
      setTimeout(() => setPendingDeleteId(null), 10);
    }
  };

  return (
    <>
      <Dialog open={isOpen} onOpenChange={(open) => {
        if (!open && !pendingDeleteId) onClose();
      }}>
      <DialogContent className="sm:max-w-4xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="text-xl">
            Chi tiết thuốc: <span className="text-primary">{medicine.name}</span>
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-8 mt-2">
          
          {/* SECTION 1: General Info */}
          <section>
            <div className="flex items-center gap-2 mb-4">
              <Info className="w-5 h-5 text-blue-600" />
              <h3 className="font-semibold text-lg text-slate-800">Thông tin cơ bản</h3>
            </div>
            
            <div className="bg-slate-50 p-5 rounded-lg border">
              <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                <div className="space-y-2">
                  <Label>Tên thuốc (Master Data)</Label>
                  <Input 
                    value={medicine.name} 
                    disabled 
                    className="bg-gray-100 text-gray-500 font-medium" 
                    title="Tên thuốc không được phép chỉnh sửa sau khi tạo."
                  />
                </div>
                <div className="space-y-2">
                  <Label>Đơn vị dùng (Usage Unit)</Label>
                  <Input 
                    placeholder="VD: ml, viên..." 
                    value={usageUnit} 
                    onChange={e => setUsageUnit(e.target.value)} 
                    className="bg-white"
                  />
                </div>
                <div className="space-y-2">
                  <Label>Hàm lượng / Base Unit</Label>
                  <Input 
                    type="number" 
                    placeholder="VD: 5" 
                    value={volume} 
                    onChange={e => setVolume(e.target.value)} 
                    className="bg-white font-mono"
                  />
                </div>
              </div>
              <div className="mt-4 flex justify-end">
                <Button 
                  onClick={handleUpdateGeneralInfo} 
                  disabled={updateMedicineMutation.isPending}
                >
                  {updateMedicineMutation.isPending ? (
                    <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                  ) : (
                    <Save className="w-4 h-4 mr-2" />
                  )}
                  Lưu thông tin cơ bản
                </Button>
              </div>
            </div>
          </section>

          <hr className="my-6 border-slate-200" />

          {/* SECTION 2: Packagings */}
          <section>
            <div className="flex items-center gap-2 mb-4">
              <Package className="w-5 h-5 text-blue-600" />
              <h3 className="font-semibold text-lg text-slate-800">Quy cách đóng gói</h3>
            </div>

            {/* Table of existing packagings */}
            <div className="border rounded-md overflow-hidden mb-6">
              <table className="w-full text-sm">
                <thead className="bg-muted">
                  <tr>
                    <th className="p-3 text-left font-medium">Đơn vị</th>
                    <th className="p-3 text-right font-medium">Hệ số</th>
                    <th className="p-3 text-right font-medium">Giá bán</th>
                    <th className="p-3 text-center font-medium">Trạng thái</th>
                    <th className="p-3 text-right font-medium">Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {isLoading ? (
                    <tr>
                      <td colSpan={5} className="p-4 text-center text-muted-foreground">
                        <Loader2 className="w-5 h-5 animate-spin mx-auto" />
                      </td>
                    </tr>
                  ) : packagings.length === 0 ? (
                    <tr>
                      <td colSpan={5} className="p-4 text-center text-muted-foreground">
                        Chưa có cấu hình đóng gói nào. Vui lòng thêm mới.
                      </td>
                    </tr>
                  ) : (
                    packagings.map((p) => (
                      <tr key={p.id} className="border-t">
                        <td className="p-3 font-medium">
                          {p.unitName}
                          {p.isBaseUnit && <Badge variant="secondary" className="ml-2 bg-blue-100 text-blue-800 border-blue-200"><CheckCircle2 className="w-3 h-3 mr-1" /> Cơ sở</Badge>}
                        </td>
                        <td className="p-3 text-right font-mono">{p.conversionFactor}</td>
                        <td className="p-3 text-right text-green-600 font-medium">
                          {p.salePrice.toLocaleString('vi-VN')} đ
                        </td>
                        <td className="p-3 text-center">
                          {p.isSellable ? (
                            <Badge variant="outline" className="text-green-600 border-green-200">Được bán</Badge>
                          ) : (
                            <Badge variant="outline" className="text-gray-400">Không bán</Badge>
                          )}
                        </td>
                        <td className="p-3 text-right">
                          <Button variant="ghost" size="icon" onClick={() => handleEditPackaging(p)}>
                            <Edit2 className="w-4 h-4 text-blue-600" />
                          </Button>
                          <Button variant="ghost" size="icon" onClick={() => handleDeletePackaging(p.id)}>
                            <Trash2 className="w-4 h-4 text-red-600" />
                          </Button>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>

            {/* Form for Packagings */}
            <div className="bg-slate-50 p-5 rounded-lg border">
              <h4 className="font-medium flex items-center gap-2 mb-4 text-slate-700">
                <Plus className="w-4 h-4" /> 
                {editingId ? "Cập nhật Quy cách" : "Thêm Quy cách Mới"}
              </h4>
              
              <form onSubmit={handleSubmitPackaging} className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4 items-start">
                  <div className="space-y-2">
                    <Label>Đơn vị tính <span className="text-red-500">*</span></Label>
                    <Select value={unitId} onValueChange={setUnitId}>
                      <SelectTrigger className="bg-white">
                        <SelectValue placeholder="Chọn đơn vị" />
                      </SelectTrigger>
                      <SelectContent>
                        {units.map((u: any) => (
                          <SelectItem key={u.medicineUnitId} value={u.medicineUnitId}>{u.name}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>

                  <div className="space-y-2">
                    <Label>Hệ số quy đổi (sang Base)</Label>
                    <Input 
                      type="number" 
                      min="1" 
                      value={conversion} 
                      onChange={e => setConversion(e.target.value)} 
                      disabled={isBase}
                      className="font-mono bg-white"
                    />
                  </div>

                  <div className="space-y-2">
                    <Label>Giá bán (VNĐ)</Label>
                    <Input 
                      type="number" 
                      min="0" 
                      value={price} 
                      onChange={e => setPrice(e.target.value)} 
                      className="font-mono text-green-700 bg-white font-semibold"
                    />
                  </div>
                </div>

                <div className="flex flex-col sm:flex-row gap-6 p-4 rounded-md border bg-white shadow-sm mt-2">
                  <div className="flex items-center space-x-3">
                    <Checkbox 
                      id="isBase" 
                      checked={isBase} 
                      onCheckedChange={(c) => {
                        setIsBase(!!c);
                        if (c) setConversion("1");
                      }} 
                      className="h-5 w-5"
                    />
                    <Label htmlFor="isBase" className="font-medium cursor-pointer text-base">
                      Là đơn vị cơ sở
                    </Label>
                  </div>
                  <div className="flex items-center space-x-3">
                    <Checkbox 
                      id="isSellable" 
                      checked={isSellable} 
                      onCheckedChange={(c) => setIsSellable(!!c)} 
                      className="h-5 w-5"
                    />
                    <Label htmlFor="isSellable" className="font-medium cursor-pointer text-base">
                      Cho phép bán lẻ
                    </Label>
                  </div>
                </div>

                <div className="flex justify-end gap-3 pt-4 border-t mt-4">
                  {editingId && (
                    <Button type="button" variant="outline" onClick={resetPackagingForm}>
                      Hủy sửa
                    </Button>
                  )}
                  <Button type="submit" disabled={addMutation.isPending || updateMutation.isPending}>
                    {(addMutation.isPending || updateMutation.isPending) && <Loader2 className="w-4 h-4 mr-2 animate-spin" />}
                    {editingId ? "Cập nhật quy cách" : "Thêm mới quy cách"}
                  </Button>
                </div>
              </form>
            </div>
          </section>

        </div>
      </DialogContent>
    </Dialog>
      <ConfirmDialog
        open={!!pendingDeleteId}
        title="Xóa quy cách đóng gói"
        message="Bạn có chắc chắn muốn xóa quy cách đóng gói này? Hành động này không thể hoàn tác."
        confirmLabel="Xóa quy cách"
        onConfirm={handleConfirmDelete}
        onCancel={() => setTimeout(() => setPendingDeleteId(null), 10)}
        isPending={deleteMutation.isPending}
        destructive={true}
      />
    </>
  );
}
