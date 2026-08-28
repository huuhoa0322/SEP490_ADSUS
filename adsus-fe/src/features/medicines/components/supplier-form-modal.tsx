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
import { getApiErrorMessage } from "@/lib/api-client";
import { useCreateSupplier, useUpdateSupplier } from "../hooks/use-suppliers";
import type { SupplierResponse } from "../api/suppliers.api";

interface SupplierFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  supplierToEdit: SupplierResponse | null;
}

export function SupplierFormModal({ isOpen, onClose, supplierToEdit }: SupplierFormModalProps) {
  const createMutation = useCreateSupplier();
  const updateMutation = useUpdateSupplier();

  const [name, setName] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [email, setEmail] = useState("");
  const [address, setAddress] = useState("");
  const [taxCode, setTaxCode] = useState("");

  // Reset form when modal opens or editing changes
  const [prevIsOpen, setPrevIsOpen] = useState(isOpen);
  const [prevSupplierId, setPrevSupplierId] = useState(supplierToEdit?.supplierId);
  if (isOpen !== prevIsOpen || supplierToEdit?.supplierId !== prevSupplierId) {
    setPrevIsOpen(isOpen);
    setPrevSupplierId(supplierToEdit?.supplierId);
    if (isOpen) {
      if (supplierToEdit) {
        setName(supplierToEdit.name);
        setPhoneNumber(supplierToEdit.phoneNumber || "");
        setEmail(supplierToEdit.email || "");
        setAddress(supplierToEdit.address || "");
        setTaxCode(supplierToEdit.taxCode || "");
      } else {
        setName("");
        setPhoneNumber("");
        setEmail("");
        setAddress("");
        setTaxCode("");
      }
    }
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || !phoneNumber.trim() || !email.trim() || !address.trim() || !taxCode.trim()) {
      toast.error("Vui lòng nhập đầy đủ thông tin bắt buộc");
      return;
    }

    const payload = {
      name: name.trim(),
      phoneNumber: phoneNumber.trim(),
      email: email.trim(),
      address: address.trim(),
      taxCode: taxCode.trim(),
    };

    try {
      if (supplierToEdit) {
        await updateMutation.mutateAsync({
          id: supplierToEdit.supplierId,
          request: {
            name: name.trim(),
            phoneNumber: phoneNumber.trim(),
            email: email.trim(),
            address: address.trim(),
          },
        });
      } else {
        await createMutation.mutateAsync(payload);
      }
      onClose();
    } catch (error) {
      // toast is already handled in the mutation hook
    }
  };

  const isPending = createMutation.isPending || updateMutation.isPending;

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <DialogTitle>{supplierToEdit ? "Sửa nhà cung cấp" : "Thêm nhà cung cấp mới"}</DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-4 py-4">
          <div className="space-y-2">
            <Label htmlFor="name" className="text-destructive">Tên nhà cung cấp *</Label>
            <Input
              id="name"
              placeholder="VD: Dược Hậu Giang"
              value={name}
              onChange={(e) => setName(e.target.value)}
              disabled={isPending}
              required
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="phoneNumber" className="text-destructive">Số điện thoại *</Label>
              <Input
                id="phoneNumber"
                placeholder="VD: 0987654321"
                value={phoneNumber}
                onChange={(e) => setPhoneNumber(e.target.value)}
                disabled={isPending}
                required
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="email" className="text-destructive">Email *</Label>
              <Input
                id="email"
                type="email"
                placeholder="VD: dhg@dhgpharma.com.vn"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                disabled={isPending}
                required
              />
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="taxCode" className="text-destructive">Mã số thuế *</Label>
            <Input
              id="taxCode"
              placeholder="VD: 1800156801"
              value={taxCode}
              onChange={(e) => setTaxCode(e.target.value)}
              disabled={isPending || !!supplierToEdit}
              required
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="address" className="text-destructive">Địa chỉ *</Label>
            <Input
              id="address"
              placeholder="VD: 288 Bis Nguyễn Văn Cừ, P. An Hòa, Q. Ninh Kiều, Cần Thơ"
              value={address}
              onChange={(e) => setAddress(e.target.value)}
              disabled={isPending}
              required
            />
          </div>

          <div className="flex justify-end gap-3 pt-4">
            <Button type="button" variant="outline" onClick={onClose} disabled={isPending}>
              Hủy
            </Button>
            <Button type="submit" disabled={isPending} className="bg-accent hover:bg-accent/90">
              {isPending && <Loader2 className="mr-2 size-4 animate-spin" />}
              {supplierToEdit ? "Lưu thay đổi" : "Tạo mới"}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
