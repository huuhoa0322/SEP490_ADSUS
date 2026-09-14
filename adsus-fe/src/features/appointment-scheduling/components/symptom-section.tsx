"use client";

import { useMemo } from "react";
import { ChevronDown, ChevronUp, Stethoscope } from "lucide-react";
import { SymptomSelector } from "@/features/medical-record/components/symptom-selector";
import type { CreateCaseSymptomInput } from "@/features/medical-record/types/medical-record.types";

interface SymptomSectionProps {
  symptoms: CreateCaseSymptomInput[];
  onChange: (symptoms: CreateCaseSymptomInput[]) => void;
  isExpanded: boolean;
  onToggle: () => void;
}

export function SymptomSection({
  symptoms,
  onChange,
  isExpanded,
  onToggle,
}: SymptomSectionProps) {
  // Đếm số triệu chứng hoặc nhóm đã được chọn
  const activeCount = useMemo(() => {
    return symptoms.filter(
      (s) =>
        Boolean(s.categoryId?.trim()) &&
        (Boolean(s.symptomId?.trim()) || Boolean(s.otherNote?.trim()))
    ).length;
  }, [symptoms]);

  return (
    <div className="rounded-xl border border-border bg-card overflow-hidden">
      {/* Accordion Header */}
      <button
        type="button"
        onClick={onToggle}
        className="flex w-full items-center justify-between p-4 text-left hover:bg-muted/40 transition-colors"
      >
        <div className="flex items-center gap-2.5">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary/10 text-primary">
            <Stethoscope className="h-4 w-4" />
          </div>
          <div>
            <span className="text-sm font-semibold text-foreground">
              Triệu chứng (tùy chọn)
            </span>
            <p className="text-xs text-muted-foreground">
              Cung cấp triệu chứng giúp bác sĩ chuẩn bị tốt hơn trước buổi khám
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          {activeCount > 0 && (
            <span className="inline-flex items-center justify-center rounded-full bg-primary px-2.5 py-0.5 text-xs font-bold text-primary-foreground">
              {activeCount}
            </span>
          )}
          {isExpanded ? (
            <ChevronUp className="h-4 w-4 text-muted-foreground" />
          ) : (
            <ChevronDown className="h-4 w-4 text-muted-foreground" />
          )}
        </div>
      </button>

      {/* Accordion Body */}
      {isExpanded && (
        <div className="border-t border-border p-4 bg-muted/10">
          <SymptomSelector value={symptoms} onChange={onChange} />
        </div>
      )}
    </div>
  );
}
