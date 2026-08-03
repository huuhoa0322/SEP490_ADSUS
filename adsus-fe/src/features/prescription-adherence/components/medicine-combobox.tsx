"use client";

import { useState } from "react";

import { useMedicineSearch } from "../hooks/use-prescriptions";
import type { MedicineItem } from "../types/prescription.types";

interface MedicineComboboxProps {
  value: MedicineItem | null;
  onChange: (medicine: MedicineItem | null) => void;
}

/**
 * Module 7 UC-18 BR-01 — autocomplete thuốc catalog.
 * Hook useMedicineSearch chỉ chạy khi keyword ≥ 2 ký tự (tránh spam server).
 */
export function MedicineCombobox({ value, onChange }: MedicineComboboxProps) {
  const [keyword, setKeyword] = useState(value?.name ?? "");
  const [open, setOpen] = useState(false);

  const { data: suggestions = [] } = useMedicineSearch(keyword);

  return (
    <div className="relative">
      <input
        type="text"
        value={keyword}
        onChange={(e) => {
          setKeyword(e.target.value);
          setOpen(true);
          onChange(null);
        }}
        onFocus={() => setOpen(true)}
        placeholder="Tìm thuốc (paracetamol, amoxicillin...)"
        className="w-full rounded-md border bg-background px-3 py-2 text-sm"
        aria-label="Tìm thuốc"
      />
      {open && suggestions.length > 0 && (
        <ul className="absolute z-10 mt-1 max-h-48 w-full overflow-auto rounded-md border bg-card shadow-lg">
          {suggestions.map((m) => (
            <li
              key={m.medicineId}
              onClick={() => {
                onChange(m);
                setKeyword(m.name);
                setOpen(false);
              }}
              className="cursor-pointer px-3 py-2 text-sm hover:bg-muted"
            >
              {m.name}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
