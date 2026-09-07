"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import { Loader2 } from "lucide-react";
import { useEffect } from "react";
import { toast } from "react-hot-toast";
import { useQueryClient } from "@tanstack/react-query";

import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { useAdjustInventory } from "../api/inventory.api";

const adjustSchema = z.object({
  newQuantityBase: z.coerce.number().min(0, "Số lượng không được âm"),
  reason: z.string().min(5, "Lý do phải có ít nhất 5 ký tự").max(500, "Lý do không được vượt quá 500 ký tự"),
});

type AdjustFormValues = z.infer<typeof adjustSchema>;

interface AdjustInventoryModalProps {
  isOpen: boolean;
  onClose: () => void;
  batchId: string | null;
  medicineName: string;
  lotNumber: string;
  currentQuantity: number;
  baseUnitName: string;
}

export function AdjustInventoryModal({
  isOpen,
  onClose,
  batchId,
  medicineName,
  lotNumber,
  currentQuantity,
  baseUnitName,
}: AdjustInventoryModalProps) {
  const queryClient = useQueryClient();
  const { mutateAsync: adjustInventory, isPending } = useAdjustInventory();

  const form = useForm<AdjustFormValues>({
    resolver: zodResolver(adjustSchema),
    defaultValues: {
      newQuantityBase: currentQuantity,
      reason: "",
    },
  });

  useEffect(() => {
    if (isOpen) {
      form.reset({
        newQuantityBase: currentQuantity,
        reason: "",
      });
    }
  }, [isOpen, currentQuantity, form]);

  const onSubmit = async (data: AdjustFormValues) => {
    if (!batchId) return;

    if (data.newQuantityBase === currentQuantity) {
      toast.error("Số lượng không thay đổi so với hệ thống.");
      return;
    }

    try {
      await adjustInventory({
        batchId,
        newQuantityBase: data.newQuantityBase,
        reason: data.reason,
      });
      
      toast.success("Điều chỉnh kho thành công");
      queryClient.invalidateQueries({ queryKey: ['medicine-batches'] });
      queryClient.invalidateQueries({ queryKey: ['inventory-history'] });
      onClose();
    } catch (error) {
      const err = error as { response?: { data?: { message?: string } } };
      toast.error(err.response?.data?.message || "Có lỗi xảy ra khi điều chỉnh kho");
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="text-xl font-heading text-foreground">
            Kiểm kê / Điều chỉnh kho
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-4 pt-4">
          <div className="rounded-lg bg-secondary/50 p-3 text-sm">
            <p><span className="font-medium text-muted-foreground">Thuốc:</span> <span className="font-semibold text-foreground">{medicineName}</span></p>
            <p><span className="font-medium text-muted-foreground">Số lô:</span> <span className="font-semibold text-foreground">{lotNumber}</span></p>
            <p><span className="font-medium text-muted-foreground">Tồn kho HT:</span> <span className="font-semibold text-blue-600">{currentQuantity} {baseUnitName}</span></p>
          </div>

          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="newQuantityBase">Số lượng thực tế ({baseUnitName})</Label>
              <Input 
                id="newQuantityBase"
                type="number" 
                {...form.register("newQuantityBase")} 
              />
              {form.formState.errors.newQuantityBase && (
                <p className="text-sm text-destructive">{form.formState.errors.newQuantityBase.message}</p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="reason">Lý do điều chỉnh</Label>
              <Textarea 
                id="reason"
                placeholder={`VD: Kiểm kê định kỳ thấy thiếu 5 ${baseUnitName.toLowerCase() || 'đơn vị'} do hỏng...`} 
                className="resize-none h-24"
                {...form.register("reason")} 
              />
              {form.formState.errors.reason && (
                <p className="text-sm text-destructive">{form.formState.errors.reason.message}</p>
              )}
            </div>

            <div className="flex justify-end gap-3 pt-4 border-t border-border mt-6">
              <Button type="button" variant="outline" onClick={onClose} disabled={isPending}>
                Hủy
              </Button>
              <Button type="submit" disabled={isPending}>
                {isPending && <Loader2 className="mr-2 size-4 animate-spin" />}
                Lưu thay đổi
              </Button>
            </div>
          </form>
        </div>
      </DialogContent>
    </Dialog>
  );
}
