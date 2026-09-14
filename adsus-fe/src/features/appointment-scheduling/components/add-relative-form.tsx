"use client";

import { useState } from "react";
import { AlertCircle, CheckCircle2, Loader2, Phone, User, Calendar, HeartHandshake } from "lucide-react";
import { format } from "date-fns";
import toast from "react-hot-toast";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getApiErrorMessage } from "@/lib/api-client";
import { isValidPhoneNumber } from "@/lib/phone-number";
import { useAddRelative, useCheckPhone } from "../hooks/use-relatives";

interface AddRelativeFormProps {
  onSuccess?: () => void;
}

export function AddRelativeForm({ onSuccess }: AddRelativeFormProps) {
  const [fullName, setFullName] = useState("");
  const [phone, setPhone] = useState("");
  const [dateOfBirth, setDateOfBirth] = useState("");
  const [relationshipName, setRelationshipName] = useState("");

  // Phone check state
  const [phoneChecked, setPhoneChecked] = useState(false);
  const [isPhoneRegistered, setIsPhoneRegistered] = useState<boolean | null>(null);

  const addMutation = useAddRelative();
  const checkPhoneMutation = useCheckPhone();

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
        toast.error("Số điện thoại này đã được đăng ký tài khoản.");
      } else {
        toast.success("Số điện thoại hợp lệ (chưa đăng ký tài khoản).");
      }
    } catch (err) {
      toast.error(getApiErrorMessage(err, "Không thể kiểm tra số điện thoại."));
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!fullName.trim()) {
      toast.error("Vui lòng nhập họ và tên người thân.");
      return;
    }

    if (phone.trim() && !isValidPhoneNumber(phone.trim())) {
      toast.error("Số điện thoại không hợp lệ (phải bắt đầu bằng 0 và có 10 chữ số).");
      return;
    }

    try {
      await addMutation.mutateAsync({
        fullName: fullName.trim(),
        phone: phone.trim() || undefined,
        dateOfBirth: dateOfBirth || undefined,
        relationshipName: relationshipName.trim() || undefined,
      });

      toast.success("Đã thêm người thân thành công.");
      // Reset form
      setFullName("");
      setPhone("");
      setDateOfBirth("");
      setRelationshipName("");
      setPhoneChecked(false);
      setIsPhoneRegistered(null);

      // Chuyển về Tab Đặt lịch
      onSuccess?.();
    } catch (err) {
      toast.error(getApiErrorMessage(err, "Thêm người thân thất bại."));
    }
  };

  const todayStr = format(new Date(), "yyyy-MM-dd");

  return (
    <Card className="border-border shadow-sm">
      <CardHeader>
        <CardTitle className="text-xl font-heading">Thêm người thân mới</CardTitle>
        <CardDescription>
          Khai báo thông tin người thân để bạn có thể đặt lịch khám hộ họ.
        </CardDescription>
      </CardHeader>

      <CardContent>
        <form onSubmit={handleSubmit} className="space-y-5">
          {/* 1. Họ tên */}
          <div className="space-y-1.5">
            <Label htmlFor="fullName" className="text-sm font-semibold">
              Họ tên người thân <span className="text-destructive">*</span>
            </Label>
            <div className="relative">
              <User className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
              <Input
                id="fullName"
                placeholder="Ví dụ: Nguyễn Văn A"
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                className="pl-9 h-10"
                required
              />
            </div>
          </div>

          {/* 2. Số điện thoại */}
          <div className="space-y-1.5">
            <Label htmlFor="phone" className="text-sm font-semibold">
              Số điện thoại <span className="text-xs font-normal text-muted-foreground">(tùy chọn cho người cao tuổi)</span>
            </Label>
            <div className="flex gap-2">
              <div className="relative flex-1">
                <Phone className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
                <Input
                  id="phone"
                  placeholder="0912345678"
                  value={phone}
                  onChange={(e) => handlePhoneChange(e.target.value)}
                  className="pl-9 h-10"
                  maxLength={10}
                />
              </div>
              <Button
                type="button"
                variant="outline"
                onClick={handleCheckPhone}
                disabled={!phone.trim() || checkPhoneMutation.isPending}
                className="h-10 shrink-0"
              >
                {checkPhoneMutation.isPending ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  "Kiểm tra SĐT"
                )}
              </Button>
            </div>

            {/* Banner kết quả kiểm tra SĐT */}
            {phoneChecked && isPhoneRegistered === true && (
              <div className="mt-2 flex items-start gap-2 rounded-lg border border-destructive/30 bg-destructive/10 p-3 text-xs text-destructive">
                <AlertCircle className="h-4 w-4 shrink-0 mt-0.5" />
                <span>
                  Số điện thoại này đã được đăng ký tài khoản. Người đó có thể tự đăng nhập và đặt lịch khám.
                </span>
              </div>
            )}

            {phoneChecked && isPhoneRegistered === false && (
              <div className="mt-2 flex items-start gap-2 rounded-lg border border-emerald-500/30 bg-emerald-500/10 p-3 text-xs text-emerald-700 dark:text-emerald-300">
                <CheckCircle2 className="h-4 w-4 shrink-0 mt-0.5" />
                <span>Số điện thoại hợp lệ và chưa đăng ký tài khoản trong hệ thống.</span>
              </div>
            )}
          </div>

          {/* 3. Ngày sinh */}
          <div className="space-y-1.5">
            <Label htmlFor="dateOfBirth" className="text-sm font-semibold">
              Ngày sinh <span className="text-xs font-normal text-muted-foreground">(tùy chọn)</span>
            </Label>
            <div className="relative">
              <Calendar className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
              <Input
                id="dateOfBirth"
                type="date"
                max={todayStr}
                value={dateOfBirth}
                onChange={(e) => setDateOfBirth(e.target.value)}
                className="pl-9 h-10"
              />
            </div>
          </div>

          {/* 4. Nhãn quan hệ */}
          <div className="space-y-1.5">
            <Label htmlFor="relationshipName" className="text-sm font-semibold">
              Nhãn quan hệ <span className="text-xs font-normal text-muted-foreground">(tùy chọn)</span>
            </Label>
            <div className="relative">
              <HeartHandshake className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
              <Input
                id="relationshipName"
                placeholder="Ví dụ: Mẹ, Vợ, Con gái, Bố, Em trai..."
                value={relationshipName}
                onChange={(e) => setRelationshipName(e.target.value)}
                className="pl-9 h-10"
              />
            </div>
            <p className="text-xs text-muted-foreground">
              Nhãn giúp bạn dễ dàng phân biệt khi chọn người thân đặt lịch.
            </p>
          </div>

          {/* Submit button */}
          <div className="pt-2">
            <Button
              type="submit"
              disabled={!fullName.trim() || addMutation.isPending}
              className="w-full h-11 text-base font-semibold"
            >
              {addMutation.isPending ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Đang lưu người thân...
                </>
              ) : (
                "Lưu người thân"
              )}
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}
