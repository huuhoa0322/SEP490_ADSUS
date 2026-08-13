"use client";

import { X } from "lucide-react";
import type { AiModelVersion } from "../types/ai-model.types";
import { formatDateTime } from "@/features/user-role-management/lib/user-labels";

interface AiModelDetailDialogProps {
  open: boolean;
  model: AiModelVersion | null;
  onClose: () => void;
}

export function AiModelDetailDialog({ open, model, onClose }: AiModelDetailDialogProps) {
  if (!open || !model) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-foreground/40 p-4 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="detail-dialog-title"
    >
      <div className="w-full max-w-2xl rounded-3xl bg-background p-7 shadow-2xl">
        <div className="flex items-center justify-between border-b border-border pb-4">
          <h2 id="detail-dialog-title" className="font-heading text-xl font-bold text-foreground">
            Chi tiết phiên bản: {model.versionCode}
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="rounded-full p-2 text-muted-foreground hover:bg-secondary transition-colors"
          >
            <X className="size-5" />
          </button>
        </div>

        <div className="mt-5 space-y-4 text-sm text-foreground">
          <div className="grid grid-cols-3 gap-4 border-b border-border pb-4">
            <div className="col-span-1 text-muted-foreground font-500">ID Model</div>
            <div className="col-span-2 font-mono text-xs">{model.modelVersionId}</div>
          </div>
          
          <div className="grid grid-cols-3 gap-4 border-b border-border pb-4">
            <div className="col-span-1 text-muted-foreground font-500">Mô tả</div>
            <div className="col-span-2">{model.description || "Không có mô tả"}</div>
          </div>

          <div className="grid grid-cols-3 gap-4 border-b border-border pb-4">
            <div className="col-span-1 text-muted-foreground font-500">HuggingFace Repo ID</div>
            <div className="col-span-2 font-mono text-xs">{model.hfRepoId}</div>
          </div>

          <div className="grid grid-cols-3 gap-4 border-b border-border pb-4">
            <div className="col-span-1 text-muted-foreground font-500">HuggingFace Filename</div>
            <div className="col-span-2 font-mono text-xs">{model.hfFilename}</div>
          </div>

          <div className="grid grid-cols-3 gap-4 border-b border-border pb-4">
            <div className="col-span-1 text-muted-foreground font-500">Trạng thái</div>
            <div className="col-span-2">
              <span
                className={`inline-flex rounded-full px-3 py-1 text-xs font-600 ${
                  model.status === "Active"
                    ? "bg-emerald-500/10 text-emerald-600"
                    : "bg-secondary text-muted-foreground"
                }`}
              >
                {model.status === "Active" ? "Đang chạy" : "Ngưng hoạt động"}
              </span>
            </div>
          </div>

          <div className="grid grid-cols-3 gap-4 border-b border-border pb-4">
            <div className="col-span-1 text-muted-foreground font-500">Metrics (Hiệu năng)</div>
            <div className="col-span-2 grid grid-cols-3 gap-2 text-center">
              <div className="rounded-xl border border-border bg-secondary/30 p-2">
                <div className="text-xs text-muted-foreground">Precision</div>
                <div className="font-600">{model.metricsPrecision ?? "—"}</div>
              </div>
              <div className="rounded-xl border border-border bg-secondary/30 p-2">
                <div className="text-xs text-muted-foreground">mAP50</div>
                <div className="font-600">{model.metricsMap50 ?? "—"}</div>
              </div>
              <div className="rounded-xl border border-border bg-secondary/30 p-2">
                <div className="text-xs text-muted-foreground">Recall</div>
                <div className="font-600">{model.metricsRecall ?? "—"}</div>
              </div>
            </div>
          </div>

          <div className="grid grid-cols-3 gap-4 pt-2">
            <div className="col-span-1 text-muted-foreground font-500">Ngày tạo</div>
            <div className="col-span-2">{formatDateTime(model.registeredAt)}</div>
          </div>
        </div>
      </div>
    </div>
  );
}
