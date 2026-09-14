"use client";

import { AlertCircle, Plus, User, Users } from "lucide-react";
import type { RelativeResponse } from "../types/relatives.types";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";

interface RelativeSectionProps {
  isBookingForSelf: boolean;
  onChangeIsBookingForSelf: (forSelf: boolean) => void;
  relatives: RelativeResponse[];
  isLoadingRelatives: boolean;
  selectedRelativeId: string | null;
  onSelectRelative: (relativeId: string | null) => void;
  onSwitchToAddRelative: () => void;
}

export function RelativeSection({
  isBookingForSelf,
  onChangeIsBookingForSelf,
  relatives,
  isLoadingRelatives,
  selectedRelativeId,
  onSelectRelative,
  onSwitchToAddRelative,
}: RelativeSectionProps) {
  return (
    <div className="space-y-3">
      <label className="text-xs font-bold tracking-wider text-muted-foreground uppercase">
        Đặt lịch cho
      </label>

      {/* Radio Cho tôi / Người thân */}
      <div className="grid grid-cols-2 gap-3">
        <button
          type="button"
          onClick={() => onChangeIsBookingForSelf(true)}
          className={`flex items-center justify-center gap-2 rounded-lg border p-3 text-sm font-medium transition-all ${
            isBookingForSelf
              ? "border-primary bg-primary text-primary-foreground shadow-sm"
              : "border-border bg-background text-foreground hover:bg-muted/50"
          }`}
        >
          <User className="h-4 w-4" />
          <span>Tôi (Bản thân)</span>
        </button>

        <button
          type="button"
          onClick={() => onChangeIsBookingForSelf(false)}
          className={`flex items-center justify-center gap-2 rounded-lg border p-3 text-sm font-medium transition-all ${
            !isBookingForSelf
              ? "border-primary bg-primary text-primary-foreground shadow-sm"
              : "border-border bg-background text-foreground hover:bg-muted/50"
          }`}
        >
          <Users className="h-4 w-4" />
          <span>Người thân</span>
        </button>
      </div>

      {/* Nếu chọn Người thân */}
      {!isBookingForSelf && (
        <div className="pt-2 animate-in fade-in duration-200">
          {isLoadingRelatives ? (
            <Skeleton className="h-11 w-full rounded-lg" />
          ) : relatives.length === 0 ? (
            // Chưa có người thân nào
            <div className="rounded-lg border border-amber-200 bg-amber-50 p-4 dark:border-amber-900/50 dark:bg-amber-950/30">
              <div className="flex items-start gap-2.5 text-sm text-amber-800 dark:text-amber-300">
                <AlertCircle className="h-5 w-5 shrink-0 text-amber-600 dark:text-amber-400 mt-0.5" />
                <p>
                  Bạn chưa có thông tin người thân nào trong danh sách. Hãy thêm người thân để đặt lịch hộ.
                </p>
              </div>
              <Button
                type="button"
                variant="default"
                size="sm"
                onClick={onSwitchToAddRelative}
                className="mt-3 w-full sm:w-auto"
              >
                <Plus className="mr-1.5 h-4 w-4" />
                Thêm người thân ngay
              </Button>
            </div>
          ) : (
            // Dropdown chọn người thân
            <div className="space-y-2">
              <Select
                value={selectedRelativeId ?? "none"}
                onValueChange={(val) => onSelectRelative(val === "none" ? null : val)}
              >
                <SelectTrigger className="h-11 w-full border-border bg-background text-sm">
                  <div className="flex items-center gap-2 truncate">
                    <Users className="h-4 w-4 shrink-0 text-muted-foreground" />
                    <SelectValue placeholder="Chọn người thân đặt lịch..." />
                  </div>
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">-- Chọn người thân --</SelectItem>
                  {relatives.map((r) => {
                    const label = `${r.relationshipName ? `[${r.relationshipName}] ` : ""}${r.patientName}${
                      r.patientPhone ? ` (${r.patientPhone})` : ""
                    }`;
                    return (
                      <SelectItem key={r.relationshipId} value={r.relationshipId}>
                        {label}
                      </SelectItem>
                    );
                  })}
                </SelectContent>
              </Select>

              <div className="flex justify-end">
                <button
                  type="button"
                  onClick={onSwitchToAddRelative}
                  className="flex items-center gap-1 text-xs font-semibold text-primary hover:underline"
                >
                  <Plus className="h-3.5 w-3.5" />
                  <span>Thêm người thân mới</span>
                </button>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
