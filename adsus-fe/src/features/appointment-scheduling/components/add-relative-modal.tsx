"use client";

import { useState } from "react";
import { format } from "date-fns";
import {
  AlertCircle,
  Calendar,
  CheckCircle2,
  HeartHandshake,
  Loader2,
  Phone,
  User,
  UserPlus,
} from "lucide-react";
import toast from "react-hot-toast";

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
import { getApiErrorMessage } from "@/lib/api-client";
import { isValidPhoneNumber } from "@/lib/phone-number";

import { useAddRelativeForGuardian, useCheckPhone } from "../hooks/use-relatives";
import type { RelativeResponse } from "../types/relatives.types";

export interface AddRelativeModalProps {
  guardianUserId?: string;
  guardianName?: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSuccess?: (created: RelativeResponse) => void;
}

const COMMON_RELATIONSHIPS = [
  "Con",
  "Vợ",
  "Chồng",
  "Bố",
  "Mẹ",
  "Ông",
  "Bà",
  "Anh",
  "Chị",
  "Em",
  "Khác",
];

export function AddRelativeModal({
  guardianUserId,
  guardianName,
  open,
  onOpenChange,
  onSuccess,
}: AddRelativeModalProps) {
  const [fullName, setFullName] = useState("");
  const [phone, setPhone] = useState("");
  const [dateOfBirth, setDateOfBirth] = useState("");
  const [relationshipName, setRelationshipName] = useState("");
  const [phoneChecked, setPhoneChecked] = useState(false);
  const [isPhoneRegistered, setIsPhoneRegistered] = useState<boolean | null>(null);

  const addRelativeMutation = useAddRelativeForGuardian(guardianUserId);
  const checkPhoneMutation = useCheckPhone();

  const handleReset = () => {
    setFullName("");
    setPhone("");
    setDateOfBirth("");
    setRelationshipName("");
    setPhoneChecked(false);
    setIsPhoneRegistered(null);
  };

  const handlePhoneChange = (val: string) => {
    setPhone(val);
    setPhoneChecked(false);
    setIsPhoneRegistered(null);
  };

  const handleCheckPhone = async () => {
    const trimmed = phone.trim();
    if (!trimmed) {
      toast.error("Vui lòng nhập số điện thoại cần kiểm tra.");
      return;
    }
    if (!isValidPhoneNumber(trimmed)) {
      toast.error("Số điện thoại không hợp lệ (phải bắt đầu bằng 0 và có 10 chữ số).");
      return;
    }

    try {
      const res = await checkPhoneMutation.mutateAsync(trimmed);
      setPhoneChecked(true);
      setIsPhoneRegistered(res.isRegistered);
      if (res.isRegistered) {
        toast.error("Số điện thoại này đã được đăng ký tài khoản bệnh nhân.");
      } else {
        toast.success("Số điện thoại hợp lệ (chưa có tài khoản riêng).");
      }
    } catch (err) {
      toast.error(getApiErrorMessage(err, "Không thể kiểm tra số điện thoại."));
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!guardianUserId) {
      toast.error("Không tìm thấy thông tin tài khoản người bảo hộ.");
      return;
    }

    if (!fullName.trim()) {
      toast.error("Vui lòng nhập họ và tên người thân.");
      return;
    }

    if (phone.trim() && !isValidPhoneNumber(phone.trim())) {
      toast.error("Số điện thoại không hợp lệ (phải bắt đầu bằng 0 và có 10 chữ số).");
      return;
    }

    try {
      const created = await addRelativeMutation.mutateAsync({
        fullName: fullName.trim(),
        phone: phone.trim() || undefined,
        dateOfBirth: dateOfBirth || undefined,
        relationshipName: relationshipName.trim() || undefined,
      });

      toast.success(`Đã thêm người thân "${created.patientName}" thành công.`);
      handleReset();
      onOpenChange(false);
      onSuccess?.(created);
    } catch (err) {
      toast.error(getApiErrorMessage(err, "Thêm người thân thất bại."));
    }
  };

  const todayStr = format(new Date(), "yyyy-MM-dd");

  return (
    <Dialog
      open={open}
      onOpenChange={(nextOpen) => {
        if (!nextOpen) {
          handleReset();
        }
        onOpenChange(nextOpen);
      }}
    >
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <div className="flex items-center gap-2">
            <UserPlus className="h-5 w-5 text-emerald-600" />
            <DialogTitle>Thêm người thân cho bệnh nhân</DialogTitle>
          </div>
          <DialogDescription>
            {guardianName
              ? `Tạo hồ sơ người thân liên kết với tài khoản bệnh nhân ${guardianName}.`
              : "Tạo hồ sơ người thân liên kết với tài khoản bệnh nhân này."}
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-4 py-2">
          {/* 1. Họ và tên */}
          <div className="space-y-1.5">
            <Label htmlFor="relative-fullName" className="text-xs font-semibold">
              Họ tên người thân <span className="text-destructive">*</span>
            </Label>
            <div className="relative">
              <User className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                id="relative-fullName"
                placeholder="Ví dụ: Nguyễn Văn Con"
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                className="pl-9 h-9 text-sm"
                required
              />
            </div>
          </div>

          {/* 2. Mối quan hệ */}
          <div className="space-y-1.5">
            <Label htmlFor="relative-relationship" className="text-xs font-semibold">
              Quan hệ với bệnh nhân <span className="text-xs font-normal text-muted-foreground">(tùy chọn)</span>
            </Label>
            <div className="flex flex-wrap gap-1.5 mb-1.5">
              {COMMON_RELATIONSHIPS.map((rel) => (
                <button
                  key={rel}
                  type="button"
                  onClick={() => setRelationshipName(rel)}
                  className={`px-2.5 py-1 text-xs rounded-md border transition-colors ${
                    relationshipName === rel
                      ? "border-primary bg-primary text-primary-foreground font-medium"
                      : "border-border bg-background hover:bg-muted/50 text-foreground"
                  }`}
                >
                  {rel}
                </button>
              ))}
            </div>
            <div className="relative">
              <HeartHandshake className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                id="relative-relationship"
                placeholder="Hoặc tự nhập: Cháu, Bác, Cô..."
                value={relationshipName}
                onChange={(e) => setRelationshipName(e.target.value)}
                className="pl-9 h-9 text-sm"
              />
            </div>
          </div>

          {/* 3. Số điện thoại */}
          <div className="space-y-1.5">
            <Label htmlFor="relative-phone" className="text-xs font-semibold">
              Số điện thoại <span className="text-xs font-normal text-muted-foreground">(tùy chọn)</span>
            </Label>
            <div className="flex gap-2">
              <div className="relative flex-1">
                <Phone className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  id="relative-phone"
                  placeholder="0912345678"
                  value={phone}
                  onChange={(e) => handlePhoneChange(e.target.value)}
                  className="pl-9 h-9 text-sm"
                  maxLength={10}
                />
              </div>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={handleCheckPhone}
                disabled={!phone.trim() || checkPhoneMutation.isPending}
                className="h-9 shrink-0 text-xs"
              >
                {checkPhoneMutation.isPending ? (
                  <Loader2 className="h-3.5 w-3.5 animate-spin" />
                ) : (
                  "Kiểm tra SĐT"
                )}
              </Button>
            </div>

            {phoneChecked && isPhoneRegistered === true && (
              <div className="flex items-start gap-1.5 rounded-md border border-destructive/30 bg-destructive/10 p-2 text-xs text-destructive">
                <AlertCircle className="h-3.5 w-3.5 shrink-0 mt-0.5" />
                <span>Số điện thoại này đã có tài khoản. Người này có thể tự đặt lịch.</span>
              </div>
            )}
            {phoneChecked && isPhoneRegistered === false && (
              <div className="flex items-start gap-1.5 rounded-md border border-emerald-500/30 bg-emerald-500/10 p-2 text-xs text-emerald-700 dark:text-emerald-300">
                <CheckCircle2 className="h-3.5 w-3.5 shrink-0 mt-0.5" />
                <span>Số điện thoại hợp lệ và chưa đăng ký tài khoản.</span>
              </div>
            )}
          </div>

          {/* 4. Ngày sinh */}
          <div className="space-y-1.5">
            <Label htmlFor="relative-dob" className="text-xs font-semibold">
              Ngày sinh <span className="text-xs font-normal text-muted-foreground">(tùy chọn)</span>
            </Label>
            <div className="relative">
              <Calendar className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                id="relative-dob"
                type="date"
                max={todayStr}
                value={dateOfBirth}
                onChange={(e) => setDateOfBirth(e.target.value)}
                className="pl-9 h-9 text-sm"
              />
            </div>
          </div>

          <DialogFooter className="pt-2">
            <Button
              type="button"
              variant="outline"
              onClick={() => onOpenChange(false)}
              disabled={addRelativeMutation.isPending}
            >
              Hủy
            </Button>
            <Button
              type="submit"
              disabled={!fullName.trim() || addRelativeMutation.isPending}
            >
              {addRelativeMutation.isPending ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Đang lưu...
                </>
              ) : (
                "Lưu người thân"
              )}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
