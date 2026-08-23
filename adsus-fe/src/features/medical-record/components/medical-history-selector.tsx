"use client";

import { useMemo } from "react";
import { PatientDiseaseInput } from "../types/medical-record.types";
import { useDiseases } from "../hooks/use-medical-dictionaries";

interface MedicalHistorySelectorProps {
  value: PatientDiseaseInput[];
  onChange: (value: PatientDiseaseInput[]) => void;
}

export function MedicalHistorySelector({ value, onChange }: MedicalHistorySelectorProps) {
  const { data: diseases, isLoading } = useDiseases();

  // Sort: "isOther" items should be at the end, although backend already sorted it, we can ensure here.
  const sortedDiseases = useMemo(() => {
    if (!diseases) return [];
    return [...diseases].sort((a, b) => {
      // "Khác" luôn ở cuối cùng
      if (a.isOther && !b.isOther) return 1;
      if (!a.isOther && b.isOther) return -1;
      // Những bệnh cần ghi chú (Lao, Ung thư...) nằm sát trên "Khác"
      if (a.requiresNote && !b.requiresNote) return 1;
      if (!a.requiresNote && b.requiresNote) return -1;
      return 0;
    });
  }, [diseases]);

  if (isLoading) {
    return <div className="text-sm text-muted-foreground">Đang tải danh mục tiền sử bệnh...</div>;
  }

  function handleToggleDisease(diseaseId: string, isChecked: boolean, requiresNote: boolean) {
    if (isChecked) {
      onChange([...value, { diseaseId, note: requiresNote ? "" : null }]);
    } else {
      onChange(value.filter((v) => v.diseaseId !== diseaseId));
    }
  }

  function handleNoteChange(diseaseId: string, note: string) {
    onChange(
      value.map((v) => (v.diseaseId === diseaseId ? { ...v, note } : v))
    );
  }

  return (
    <div className="space-y-4 rounded-lg border border-border p-4 bg-muted/10">
      <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
        {sortedDiseases?.map((disease) => {
          const selectedItem = value.find((v) => v.diseaseId === disease.id);
          const isSelected = !!selectedItem;

          return (
            <div
              key={disease.id}
              className={`flex flex-col gap-2 ${disease.requiresNote ? "md:col-span-2" : ""}`}
            >
              <label className="flex items-start gap-2 cursor-pointer text-sm">
                <input
                  type="checkbox"
                  checked={isSelected}
                  onChange={(e) =>
                    handleToggleDisease(disease.id, e.target.checked, disease.requiresNote)
                  }
                  className="mt-1 shrink-0 rounded border-primary text-primary focus:ring-primary"
                />
                <span className="leading-snug">{disease.name}</span>
              </label>

              {disease.requiresNote && isSelected && (
                <div className="pl-6 animate-in fade-in slide-in-from-top-1 duration-200">
                  <textarea
                    value={selectedItem?.note || ""}
                    onChange={(e) => handleNoteChange(disease.id, e.target.value)}
                    placeholder="Nhập chi tiết bệnh..."
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
