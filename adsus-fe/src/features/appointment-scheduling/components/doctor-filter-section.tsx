"use client";

import { User } from "lucide-react";
import type { DoctorOption } from "../hooks/use-booking-form";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

interface DoctorFilterSectionProps {
  selectedGender: string | null;
  onSelectGender: (gender: string | null) => void;
  doctors: DoctorOption[];
  selectedDoctorId: string | null;
  onSelectDoctor: (doctorId: string | null) => void;
}

export function DoctorFilterSection({
  selectedGender,
  onSelectGender,
  doctors,
  selectedDoctorId,
  onSelectDoctor,
}: DoctorFilterSectionProps) {
  const isAll = selectedGender === null;
  const isMale = selectedGender?.toUpperCase() === "MALE";
  const isFemale = selectedGender?.toUpperCase() === "FEMALE";

  return (
    <div className="space-y-4">
      {/* 1. Gender Filter Chips */}
      <div>
        <span className="text-xs font-bold tracking-wider text-muted-foreground uppercase">
          Giới tính bác sĩ (tùy chọn)
        </span>
        <div className="mt-2 grid grid-cols-3 gap-2 sm:gap-3">
          <button
            type="button"
            onClick={() => onSelectGender(null)}
            className={`h-11 rounded-lg border text-sm font-medium transition-all ${
              isAll
                ? "border-primary bg-primary text-primary-foreground shadow-sm"
                : "border-border bg-background text-foreground hover:bg-muted/50"
            }`}
          >
            Tất cả
          </button>
          <button
            type="button"
            onClick={() => onSelectGender(isMale ? null : "MALE")}
            className={`h-11 rounded-lg border text-sm font-medium transition-all ${
              isMale
                ? "border-primary bg-primary text-primary-foreground shadow-sm"
                : "border-border bg-background text-foreground hover:bg-muted/50"
            }`}
          >
            Nam
          </button>
          <button
            type="button"
            onClick={() => onSelectGender(isFemale ? null : "FEMALE")}
            className={`h-11 rounded-lg border text-sm font-medium transition-all ${
              isFemale
                ? "border-primary bg-primary text-primary-foreground shadow-sm"
                : "border-border bg-background text-foreground hover:bg-muted/50"
            }`}
          >
            Nữ
          </button>
        </div>
      </div>

      {/* 2. Doctor Dropdown */}
      <div>
        <span className="text-xs font-bold tracking-wider text-muted-foreground uppercase">
          Bác sĩ phụ trách
        </span>
        <div className="mt-2">
          <Select
            value={selectedDoctorId ?? "none"}
            onValueChange={(val) => onSelectDoctor(val === "none" ? null : val)}
          >
            <SelectTrigger className="h-11 w-full border-border bg-background text-sm">
              <div className="flex items-center gap-2 truncate">
                <User className="h-4 w-4 shrink-0 text-muted-foreground" />
                <SelectValue placeholder="Chọn bác sĩ phụ trách..." />
              </div>
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="none">-- Chọn bác sĩ --</SelectItem>
              {doctors.map((d) => (
                <SelectItem key={d.id} value={d.id}>
                  BS. {d.name}
                  {d.gender ? ` (${d.gender === "MALE" ? "Nam" : d.gender === "FEMALE" ? "Nữ" : d.gender})` : ""}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>
    </div>
  );
}
