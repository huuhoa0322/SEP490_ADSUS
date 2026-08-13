"use client";

import { AlertCircle, BrainCircuit, Eye, Loader2, Pencil, PlayCircle, Plus, Search } from "lucide-react";
import Link from "next/link";
import { useState } from "react";

import { formatDateTime } from "@/features/user-role-management/lib/user-labels";
import { ConfirmDialog } from "@/features/user-role-management/components/confirm-dialog";
import { getApiErrorMessage } from "@/lib/api-client";
import toast from "react-hot-toast";

import { useActivateAiModel, useAiModelList, useCalculateMap50 } from "../hooks/use-ai-models";
import type { AiModelVersion } from "../types/ai-model.types";
import { AiModelDetailDialog } from "./ai-model-detail-dialog";
import { AiModelFormDialog } from "./ai-model-form";

const formatPercent = (val?: number | null) => {
  if (val === undefined || val === null) return "Chưa có dữ liệu";
  return (val * 100).toFixed(1) + "%";
};

function PagerButton({
  children,
  disabled,
  onClick,
}: {
  children: React.ReactNode;
  disabled: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      className="rounded-full border border-border px-4 py-2 transition-colors hover:bg-secondary disabled:cursor-not-allowed disabled:opacity-40"
    >
      {children}
    </button>
  );
}

export function AiModelList() {
  const [keyword, setKeyword] = useState("");
  const [page, setPage] = useState(1);
  const query = { keyword, page, pageSize: 10 };
  
  const { data, isLoading, isError, error } = useAiModelList(query);
  const models = data?.items;
  
  const { mutate: activateModel, isPending: isActivating, error: activateError, reset: resetActivate } = useActivateAiModel();
  const { mutate: calculateMap50, isPending: isCalculatingMap50 } = useCalculateMap50();

  // Dialog states
  const [selectedModel, setSelectedModel] = useState<AiModelVersion | null>(null);
  const [isDetailOpen, setIsDetailOpen] = useState(false);
  const [modelToActivate, setModelToActivate] = useState<AiModelVersion | null>(null);
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [editingModelId, setEditingModelId] = useState<string | undefined>(undefined);

  const handleOpenDetail = (model: AiModelVersion) => {
    setSelectedModel(model);
    setIsDetailOpen(true);
  };

  const handleActivate = () => {
    if (!modelToActivate) return;
    activateModel(modelToActivate.modelVersionId, {
      onSuccess: () => {
        setModelToActivate(null);
        setPage(1);
      },
    });
  };

  const handleCalculateMap50 = (modelId: string) => {
    const p = toast.loading("Đang quét Database tính toán mAP50...");
    calculateMap50(modelId, {
      onSuccess: () => {
        toast.success("Đã quét Database và tính toán xong mAP50!", { id: p });
      },
      onError: (err) => {
        toast.error(getApiErrorMessage(err, "Lỗi tính toán mAP50"), { id: p });
      }
    });
  };

  return (
    <div className="mx-auto w-full max-w-6xl">
      {/* Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="flex items-center gap-2 font-heading text-3xl font-bold tracking-tight text-foreground">
            <BrainCircuit className="size-8 text-accent" />
            Phiên bản AI Model
          </h1>
          <p className="mt-2 text-sm text-muted-foreground">
            Quản lý và kích hoạt các phiên bản mô hình AI nhận diện bệnh.
          </p>
        </div>
        <button
          onClick={() => {
            setEditingModelId(undefined);
            setIsFormOpen(true);
          }}
          className="flex h-12 items-center justify-center gap-2 rounded-full bg-accent px-6 font-heading text-sm font-600 tracking-wider text-white transition-colors hover:bg-accent/90"
        >
          <Plus className="size-5" />
          Đăng ký Model
        </button>
      </div>

      {/* Bộ lọc */}
      <div className="mt-8 flex flex-wrap gap-3">
        <div className="relative min-w-64 flex-1">
          <Search
            aria-hidden
            className="pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
          />
          <input
            value={keyword}
            onChange={(e) => {
              setKeyword(e.target.value);
              setPage(1);
            }}
            placeholder="Tìm theo Version Code hoặc HuggingFace File"
            aria-label="Tìm kiếm model"
            className="h-12 w-full rounded-full border border-border bg-background pl-11 pr-4 text-[15px] outline-none transition-colors focus:border-accent"
          />
        </div>
      </div>

      {/* Errors */}
      {isError && (
        <div
          role="alert"
          className="mt-6 flex items-start gap-2.5 rounded-2xl border border-destructive/25 bg-destructive/5 px-4 py-3 text-sm text-destructive"
        >
          <AlertCircle aria-hidden className="mt-0.5 size-4 shrink-0" />
          <span>
            {getApiErrorMessage(
              error,
              "Không thể tải danh sách phiên bản AI. Vui lòng thử lại."
            )}
          </span>
        </div>
      )}

      {/* Bảng danh sách */}
      <div className="mt-6 overflow-x-auto rounded-3xl border border-border bg-background">
        <table className="w-full min-w-4xl border-collapse text-left text-sm">
          <thead>
            <tr className="border-b border-border bg-secondary/40">
              <th className="px-5 py-4 font-600 text-muted-foreground">Version Code</th>
              <th className="px-5 py-4 font-600 text-muted-foreground">HuggingFace File</th>
              <th className="px-5 py-4 font-600 text-muted-foreground">P / R (Live)</th>
              <th className="px-5 py-4 font-600 text-muted-foreground">mAP50</th>
              <th className="px-5 py-4 font-600 text-muted-foreground">Ngày tạo</th>
              <th className="px-5 py-4 font-600 text-muted-foreground">Trạng thái</th>
              <th className="px-5 py-4 text-right font-600 text-muted-foreground">Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr>
                <td colSpan={6} className="px-5 py-14 text-center text-muted-foreground">
                  <Loader2 className="mx-auto size-5 animate-spin" />
                </td>
              </tr>
            )}

            {!isLoading && (!models || models.length === 0) && (
              <tr>
                <td colSpan={6} className="px-5 py-14 text-center text-muted-foreground">
                  Chưa có phiên bản AI nào được đăng ký hoặc không tìm thấy kết quả.
                </td>
              </tr>
            )}

            {models?.map((model) => (
              <tr key={model.modelVersionId} className="border-b border-border last:border-0 hover:bg-secondary/20">
                <td className="px-5 py-4 font-600 text-foreground">
                  {model.versionCode}
                </td>
                <td className="px-5 py-4 text-muted-foreground font-mono text-xs">
                  {model.hfFilename}
                </td>
                <td className="px-5 py-4 text-muted-foreground">
                  {formatPercent(model.livePrecision)} / {formatPercent(model.liveRecall)}
                </td>
                <td className="px-5 py-4 text-muted-foreground">
                  {formatPercent(model.liveMap50 != null ? model.liveMap50 / 100 : null)}
                  {model.lastEvaluatedAt && (
                    <div className="text-[10px] text-muted-foreground/60 mt-1">
                      (Cập nhật: {formatDateTime(model.lastEvaluatedAt)})
                    </div>
                  )}
                </td>
                <td className="px-5 py-4 text-muted-foreground">
                  {formatDateTime(model.registeredAt)}
                </td>
                <td className="px-5 py-4">
                  <span
                    className={`inline-flex rounded-full px-3 py-1 text-xs font-600 ${
                      model.status === "Active"
                        ? "bg-emerald-500/10 text-emerald-600"
                        : "bg-secondary text-muted-foreground"
                    }`}
                  >
                    {model.status === "Active" ? "Đang chạy" : "Inactive"}
                  </span>
                </td>
                <td className="px-5 py-4 text-right">
                  <div className="flex items-center justify-end gap-2">
                    <button
                      onClick={() => handleCalculateMap50(model.modelVersionId)}
                      disabled={isCalculatingMap50}
                      title="Tính lại mAP50"
                      className="flex size-9 items-center justify-center rounded-full text-indigo-500 hover:bg-indigo-50 hover:text-indigo-600 disabled:opacity-50"
                    >
                      {isCalculatingMap50 ? <Loader2 className="size-4 animate-spin" /> : <PlayCircle className="size-4" />}
                    </button>
                    <button
                      onClick={() => handleOpenDetail(model)}
                      title="Xem chi tiết"
                      className="flex size-9 items-center justify-center rounded-full text-muted-foreground hover:bg-secondary hover:text-foreground"
                    >
                      <Eye className="size-4" />
                    </button>
                    {model.status === "Inactive" && (
                      <>
                        <button
                          onClick={() => {
                            setEditingModelId(model.modelVersionId);
                            setIsFormOpen(true);
                          }}
                          title="Sửa thông tin"
                          className="flex size-9 items-center justify-center rounded-full text-muted-foreground hover:bg-secondary hover:text-foreground"
                        >
                          <Pencil className="size-4" />
                        </button>
                        <button
                          onClick={() => setModelToActivate(model)}
                          title="Kích hoạt mô hình này"
                          className="flex size-9 items-center justify-center rounded-full text-emerald-600 hover:bg-emerald-500/10"
                        >
                          <PlayCircle className="size-5" />
                        </button>
                      </>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Phân trang */}
      {data && data.totalPages > 1 && (
        <div className="mt-5 flex items-center justify-between text-sm text-muted-foreground">
          <span>
            Đang xem {data.items.length} / {data.totalItems} kết quả
          </span>
          <div className="flex gap-2">
            <PagerButton disabled={data.page <= 1} onClick={() => setPage((p) => p - 1)}>
              Trước
            </PagerButton>
            
            <div className="flex gap-1.5 items-center mx-2">
              {(() => {
                const total = data.totalPages;
                const current = data.page;
                let pages: number[] = [];
                if (total <= 5) {
                  pages = Array.from({ length: total }, (_, i) => i + 1);
                } else if (current <= 3) {
                  pages = [1, 2, 3, 4, 5];
                } else if (current >= total - 2) {
                  pages = [total - 4, total - 3, total - 2, total - 1, total];
                } else {
                  pages = [current - 2, current - 1, current, current + 1, current + 2];
                }

                return pages.map((p) => {
                  const active = p === current;
                  return (
                    <button
                      key={p}
                      onClick={() => setPage(p)}
                      className={`flex h-10 min-w-10 items-center justify-center rounded-full border px-3 text-sm transition-colors ${
                        active
                          ? "border-accent bg-accent font-bold text-white shadow-sm"
                          : "border-border hover:bg-secondary text-foreground"
                      }`}
                    >
                      {p}
                    </button>
                  );
                });
              })()}
            </div>

            <PagerButton
              disabled={data.page >= data.totalPages}
              onClick={() => setPage((p) => p + 1)}
            >
              Sau
            </PagerButton>
          </div>
        </div>
      )}

      <AiModelDetailDialog 
        open={isDetailOpen} 
        model={selectedModel} 
        onClose={() => setIsDetailOpen(false)} 
      />

      <ConfirmDialog
        open={!!modelToActivate}
        title="Kích hoạt mô hình AI"
        message={`Bạn có chắc chắn muốn kích hoạt phiên bản ${modelToActivate?.versionCode}? Hệ thống sẽ gọi Python Backend để nạp lại model này vào bộ nhớ phục vụ chẩn đoán.`}
        confirmLabel="Kích hoạt"
        isPending={isActivating}
        error={activateError}
        onConfirm={handleActivate}
        onCancel={() => {
          setModelToActivate(null);
          resetActivate();
        }}
      />

      <AiModelFormDialog
        open={isFormOpen}
        id={editingModelId}
        onClose={() => setIsFormOpen(false)}
        onSuccess={() => setIsFormOpen(false)}
      />
    </div>
  );
}
