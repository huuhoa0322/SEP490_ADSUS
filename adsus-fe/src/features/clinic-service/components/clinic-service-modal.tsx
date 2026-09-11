"use client";

import { useState } from "react";
import { Loader2 } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { useCreateClinicService, useUpdateClinicService } from "../queries";
import type { ClinicService } from "../types";

interface ClinicServiceModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  service?: ClinicService | null;
}

interface ClinicServiceFormProps {
  service?: ClinicService | null;
  onClose: () => void;
}

function ClinicServiceForm({ service, onClose }: ClinicServiceFormProps) {
  const isEdit = Boolean(service);

  const [code, setCode] = useState(service?.code ?? "");
  const [name, setName] = useState(service?.name ?? "");
  const [price, setPrice] = useState<string>(
    service ? String(service.price) : "",
  );
  const [description, setDescription] = useState(service?.description ?? "");
  const [errors, setErrors] = useState<Record<string, string>>({});

  const createMutation = useCreateClinicService();
  const updateMutation = useUpdateClinicService();

  const isPending = createMutation.isPending || updateMutation.isPending;

  const validate = () => {
    const errs: Record<string, string> = {};

    if (!isEdit) {
      if (!code.trim()) {
        errs.code = "Mã dịch vụ không được để trống.";
      } else if (!/^[A-Za-z0-9_]+$/.test(code.trim())) {
        errs.code =
          "Mã dịch vụ chỉ chứa chữ hoa, số và dấu gạch dưới (A-Z, 0-9, _).";
      }
    }

    if (!name.trim()) {
      errs.name = "Tên dịch vụ không được để trống.";
    }

    const parsedPrice = Number(price);
    if (price === "" || isNaN(parsedPrice) || parsedPrice < 0) {
      errs.price = "Đơn giá phải là số lớn hơn hoặc bằng 0.";
    }

    setErrors(errs);
    return Object.keys(errs).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!validate()) return;

    const numPrice = Number(price);

    if (isEdit && service) {
      updateMutation.mutate(
        {
          id: service.id,
          dto: {
            name: name.trim(),
            price: numPrice,
            description: description.trim() || null,
          },
        },
        {
          onSuccess: () => {
            onClose();
          },
        },
      );
    } else {
      createMutation.mutate(
        {
          code: code.trim().toUpperCase(),
          name: name.trim(),
          price: numPrice,
          description: description.trim() || null,
        },
        {
          onSuccess: () => {
            onClose();
          },
        },
      );
    }
  };

  return (
    <>
      <DialogHeader>
        <DialogTitle className="font-heading text-lg font-bold text-foreground">
          {isEdit ? "Chỉnh sửa dịch vụ phòng khám" : "Thêm dịch vụ phòng khám mới"}
        </DialogTitle>
        <DialogDescription className="text-sm text-muted-foreground">
          {isEdit
            ? "Cập nhật tên, đơn giá hoặc mô tả cho dịch vụ phòng khám."
            : "Điền thông tin bên dưới để khởi tạo dịch vụ phòng khám mới trong hệ thống."}
        </DialogDescription>
      </DialogHeader>

      <form onSubmit={handleSubmit} className="space-y-4 py-2">
        <div className="space-y-1.5">
          <Label htmlFor="service-code" className="font-semibold text-foreground">
            Mã dịch vụ <span className="text-destructive">*</span>
          </Label>
          <Input
            id="service-code"
            placeholder="Ví dụ: GENERAL_EXAM, XRAY_CHEST"
            value={code}
            onChange={(e) => setCode(e.target.value.toUpperCase())}
            disabled={isEdit || isPending}
            className="uppercase font-mono font-medium"
          />
          {isEdit && (
            <p className="text-xs text-muted-foreground">
              Mã dịch vụ là định danh duy nhất và không thể thay đổi sau khi tạo.
            </p>
          )}
          {errors.code && <p className="text-xs text-destructive">{errors.code}</p>}
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="service-name" className="font-semibold text-foreground">
            Tên dịch vụ <span className="text-destructive">*</span>
          </Label>
          <Input
            id="service-name"
            placeholder="Ví dụ: Khám tổng quát, Siêu âm ổ bụng"
            value={name}
            onChange={(e) => setName(e.target.value)}
            disabled={isPending}
            className="font-medium"
          />
          {errors.name && <p className="text-xs text-destructive">{errors.name}</p>}
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="service-price" className="font-semibold text-foreground">
            Đơn giá (VNĐ) <span className="text-destructive">*</span>
          </Label>
          <Input
            id="service-price"
            type="number"
            min="0"
            step="1000"
            placeholder="100000"
            value={price}
            onChange={(e) => setPrice(e.target.value)}
            disabled={isPending}
            className="font-medium"
          />
          {errors.price && <p className="text-xs text-destructive">{errors.price}</p>}
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="service-description" className="font-semibold text-foreground">
            Mô tả dịch vụ
          </Label>
          <Textarea
            id="service-description"
            placeholder="Nhập mô tả chi tiết quy trình hoặc thông tin dịch vụ..."
            rows={3}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            disabled={isPending}
          />
        </div>

        <DialogFooter className="pt-3 gap-2">
          <Button
            type="button"
            variant="outline"
            onClick={onClose}
            disabled={isPending}
          >
            Hủy bỏ
          </Button>
          <Button type="submit" disabled={isPending}>
            {isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {isEdit ? "Lưu thay đổi" : "Thêm dịch vụ"}
          </Button>
        </DialogFooter>
      </form>
    </>
  );
}

export function ClinicServiceModal({
  open,
  onOpenChange,
  service,
}: ClinicServiceModalProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[500px]">
        {open && (
          <ClinicServiceForm
            key={service?.id ?? "new"}
            service={service}
            onClose={() => onOpenChange(false)}
          />
        )}
      </DialogContent>
    </Dialog>
  );
}
