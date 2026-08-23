"use client";

import { useMemo } from "react";
import { PatientAllergyInput } from "../types/medical-record.types";
import { useAllergyTypes } from "../hooks/use-medical-dictionaries";

interface AllergySelectorProps {
  value: PatientAllergyInput[];
  onChange: (value: PatientAllergyInput[]) => void;
}

export function AllergySelector({ value, onChange }: AllergySelectorProps) {
  const { data: allergies, isLoading } = useAllergyTypes();

  const sortedAllergies = useMemo(() => {
    if (!allergies) return [];
    return [...allergies].sort((a, b) => {
      if (a.isOther && !b.isOther) return 1;
      if (!a.isOther && b.isOther) return -1;
      return 0;
    });
  }, [allergies]);

  if (isLoading) {
    return <div className="text-sm text-muted-foreground">Đang tải danh mục dị ứng...</div>;
  }

  function handleToggleAllergy(allergyTypeId: string, isChecked: boolean, isOther: boolean) {
    if (isChecked) {
      // Vấn đề 4: TẤT CẢ các dị ứng đều có ô nhập liệu (khởi tạo chuỗi rỗng)
      onChange([...value, { allergyTypeId, note: "" }]);
    } else {
      onChange(value.filter((v) => v.allergyTypeId !== allergyTypeId));
    }
  }

  function handleNoteChange(allergyTypeId: string, note: string) {
    onChange(
      value.map((v) => (v.allergyTypeId === allergyTypeId ? { ...v, note } : v))
    );
  }

  return (
    <div className="space-y-4 rounded-lg border border-border p-4 bg-muted/10">
      <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
        {sortedAllergies?.map((allergy) => {
          const selectedItem = value.find((v) => v.allergyTypeId === allergy.id);
          const isSelected = !!selectedItem;

          return (
            <div key={allergy.id} className="flex flex-col gap-2">
              <label className="flex items-start gap-2 cursor-pointer text-sm">
                <input
                  type="checkbox"
                  checked={isSelected}
                  onChange={(e) =>
                    handleToggleAllergy(allergy.id, e.target.checked, allergy.isOther)
                  }
                  className="mt-1 shrink-0 rounded border-primary text-primary focus:ring-primary"
                />
                <span className="leading-snug">{allergy.name}</span>
              </label>

              {isSelected && (
                <div className="pl-6 animate-in fade-in slide-in-from-top-1 duration-200">
                  <textarea
                    value={selectedItem?.note || ""}
                    onChange={(e) => handleNoteChange(allergy.id, e.target.value)}
                    placeholder="Nhập chi tiết dị ứng..."
                    autoFocus
                    rows={2}
                    className="w-full resize-y rounded-md border border-input bg-background px-3 py-2 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
                  />
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}
