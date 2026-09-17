"use client";

import { useMemo } from "react";
import type { CaseDiagnosisInput } from "../types/medical-record.types";
import { useDiagnosisItems } from "../hooks/use-diagnosis";

interface DiagnosisSelectorProps {
  value: CaseDiagnosisInput[];
  onChange: (value: CaseDiagnosisInput[]) => void;
  disabled?: boolean;
}

export function DiagnosisSelector({ value, onChange, disabled }: DiagnosisSelectorProps) {
  const { data: diagnosisItems, isLoading } = useDiagnosisItems();

  const sortedItems = useMemo(() => {
    if (!diagnosisItems) return [];
    return [...diagnosisItems].sort((a, b) => a.displayOrder - b.displayOrder);
  }, [diagnosisItems]);

  if (isLoading) {
    return <div className="text-sm font-semibold text-foreground">Đang tải danh mục chẩn đoán...</div>;
  }

  function handleToggleDiagnosis(diagnosisItemId: string, isChecked: boolean, hasNoteOrOther: boolean) {
    if (isChecked) {
      onChange([...value, { diagnosisItemId, note: hasNoteOrOther ? "" : null }]);
    } else {
      onChange(value.filter((v) => v.diagnosisItemId !== diagnosisItemId));
    }
  }

  function handleNoteChange(diagnosisItemId: string, note: string) {
    onChange(
      value.map((v) => (v.diagnosisItemId === diagnosisItemId ? { ...v, note } : v)),
    );
  }

  return (
    <div className="space-y-4 rounded-lg border border-border p-4 bg-muted/10">
      <div className="grid grid-cols-1 md:grid-cols-2 gap-x-6 gap-y-3.5 items-start">
        {sortedItems.map((item) => {
          const selectedItem = value.find((v) => v.diagnosisItemId === item.id);
          const isSelected = !!selectedItem;

          return (
            <div
              key={item.id}
              className="flex flex-col gap-2"
            >
              <label className="flex items-start gap-2 cursor-pointer text-sm">
                <input
                  type="checkbox"
                  checked={isSelected}
                  disabled={disabled}
                  onChange={(e) =>
                    handleToggleDiagnosis(item.id, e.target.checked, item.requiresNote || item.isOther)
                  }
                  className="mt-1 shrink-0 rounded border-primary text-primary focus:ring-primary"
                />
                <span className="leading-snug font-medium text-foreground">{item.name}</span>
              </label>

              {isSelected && item.isOther && (
                <div className="pl-6 animate-in fade-in slide-in-from-top-1 duration-200">
                  <input
                    type="text"
                    value={selectedItem?.note || ""}
                    disabled={disabled}
                    onChange={(e) => handleNoteChange(item.id, e.target.value)}
                    placeholder="Nhập chẩn đoán khác..."
                    className="w-full rounded-md border border-input bg-background px-3 py-1.5 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
                  />
                </div>
              )}

              {isSelected && item.requiresNote && !item.isOther && (
                <div className="pl-6 animate-in fade-in slide-in-from-top-1 duration-200">
                  <textarea
                    value={selectedItem?.note || ""}
                    disabled={disabled}
                    onChange={(e) => handleNoteChange(item.id, e.target.value)}
                    placeholder="Nhập chi tiết chẩn đoán..."
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
