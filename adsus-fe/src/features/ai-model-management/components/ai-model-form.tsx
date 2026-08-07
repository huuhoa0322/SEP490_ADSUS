"use client";

import { AlertCircle, Loader2, Save, Upload, X } from "lucide-react";
import { type FormEvent, useEffect, useState, useRef } from "react";
import toast from "react-hot-toast";

import { getApiErrorMessage } from "@/lib/api-client";
import {
  useAiModelDetail,
  useRegisterAiModel,
  useUpdateAiModel,
} from "../hooks/use-ai-models";
import type { RegisterModelVersionRequest } from "../types/ai-model.types";

interface AiModelFormDialogProps {
  id?: string;
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export function AiModelFormDialog({ id, open, onClose, onSuccess }: AiModelFormDialogProps) {
  const isEditing = !!id;
  const fileInputRef = useRef<HTMLInputElement>(null);

  const { data: detail, isLoading: isLoadingDetail } = useAiModelDetail(id);
  const { mutate: register, isPending: isRegistering, error: registerError } = useRegisterAiModel();
  const { mutate: update, isPending: isUpdating, error: updateError } = useUpdateAiModel();

  const [formData, setFormData] = useState<RegisterModelVersionRequest>({
    versionCode: "",
    description: "",
    hfRepoId: "",
    hfFilename: "",
    metricsPrecision: undefined,
    metricsMap50: undefined,
    metricsRecall: undefined,
  });

  const [isSubmitError, setIsSubmitError] = useState(false);

  useEffect(() => {
    if (detail) {
      setFormData({
        versionCode: detail.versionCode,
        description: detail.description || "",
        hfRepoId: detail.hfRepoId,
        hfFilename: detail.hfFilename,
        metricsPrecision: detail.metricsPrecision ?? undefined,
        metricsMap50: detail.metricsMap50 ?? undefined,
        metricsRecall: detail.metricsRecall ?? undefined,
      });
    }
  }, [detail]);

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = (event) => {
      try {
        const text = event.target?.result as string;
        let parsed: Record<string, string> = {};
        
        if (text.trim().startsWith('{')) {
          parsed = JSON.parse(text);
        } else {
          // Parse Key=Value
          text.split('\n').forEach(line => {
            if (!line.trim() || line.startsWith('#')) return;
            const parts = line.split('=');
            if (parts.length >= 2) {
              const key = parts[0].trim();
              const val = parts.slice(1).join('=').trim();
              // Normalizing keys: VersionCode -> versionCode
              const normalizedKey = key.charAt(0).toLowerCase() + key.slice(1);
              parsed[normalizedKey] = val;
            }
          });
        }
        
        setFormData(prev => ({
          ...prev,
          versionCode: (!isEditing && parsed.versionCode) ? parsed.versionCode : prev.versionCode,
          description: parsed.description ?? prev.description,
          hfRepoId: parsed.hfRepoId ?? prev.hfRepoId,
          hfFilename: parsed.hfFilename ?? prev.hfFilename,
          metricsPrecision: parsed.metricsPrecision ? parseFloat(parsed.metricsPrecision) : prev.metricsPrecision,
          metricsMap50: parsed.metricsMap50 ? parseFloat(parsed.metricsMap50) : prev.metricsMap50,
          metricsRecall: parsed.metricsRecall ? parseFloat(parsed.metricsRecall) : prev.metricsRecall,
        }));
        
        if (fileInputRef.current) fileInputRef.current.value = '';
      } catch (err) {
        console.error("Error parsing file", err);
        toast.error("File không đúng định dạng. Vui lòng dùng JSON hoặc chuẩn Key=Value");
      }
    };
    reader.readAsText(file);
  };

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    setIsSubmitError(false);

    if (isEditing) {
      if (detail?.status === "Active") {
        setIsSubmitError(true);
        return; // Không cho phép sửa nếu đang Active (Backend cũng chặn)
      }
      
      update(
        {
          id,
          payload: {
            description: formData.description,
            hfRepoId: formData.hfRepoId,
            hfFilename: formData.hfFilename,
            metricsPrecision: formData.metricsPrecision,
            metricsMap50: formData.metricsMap50,
            metricsRecall: formData.metricsRecall,
          },
        },
        {
          onSuccess: () => {
            onSuccess();
            onClose();
          },
          onError: () => setIsSubmitError(true),
        }
      );
    } else {
      register(formData, {
        onSuccess: () => {
          onSuccess();
          onClose();
        },
        onError: () => setIsSubmitError(true),
      });
    }
  };

  if (!open) return null;

  const isPending = isRegistering || isUpdating;
  const apiError = isEditing ? updateError : registerError;
  const disabledForm = isEditing && detail?.status === "Active";

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-foreground/40 p-4 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
    >
      <div className="mx-auto w-full max-w-2xl overflow-hidden rounded-3xl bg-background shadow-2xl flex flex-col max-h-full">
        <div className="flex items-center justify-between border-b border-border p-5 md:p-7 shrink-0">
          <div>
            <h2 className="font-heading text-xl font-bold tracking-tight text-foreground">
              {isEditing ? `Sửa phiên bản: ${detail?.versionCode}` : "Đăng ký phiên bản AI mới"}
            </h2>
            <p className="mt-1 text-sm text-muted-foreground">
              {isEditing 
                ? "Cập nhật thông tin cấu hình cho mô hình AI." 
                : "Thêm một mô hình AI mới từ HuggingFace vào hệ thống."}
            </p>
          </div>
          
          <div className="flex items-center gap-3">
            {!disabledForm && (
              <div>
                <input 
                  type="file" 
                  accept=".txt,.json" 
                  className="hidden" 
                  ref={fileInputRef}
                  onChange={handleFileUpload} 
                />
                <button
                  type="button"
                  onClick={() => fileInputRef.current?.click()}
                  className="flex h-10 items-center gap-2 rounded-full border border-border bg-background px-4 text-sm font-500 transition-colors hover:bg-secondary"
                >
                  <Upload className="size-4 text-muted-foreground" />
                  <span className="hidden sm:inline">Tải cấu hình từ File</span>
                  <span className="sm:hidden">Tải File</span>
                </button>
              </div>
            )}
            <button
              type="button"
              onClick={onClose}
              className="rounded-full p-2 text-muted-foreground hover:bg-secondary transition-colors"
            >
              <X className="size-5" />
            </button>
          </div>
        </div>

        <div className="overflow-y-auto p-5 md:p-7">
          {isEditing && isLoadingDetail ? (
            <div className="flex min-h-64 items-center justify-center">
              <Loader2 className="size-6 animate-spin text-muted-foreground" />
            </div>
          ) : (
            <>
              {(isSubmitError && apiError) && (
                <div
                  role="alert"
                  className="mb-6 flex items-start gap-2.5 rounded-2xl border border-destructive/25 bg-destructive/5 px-4 py-3 text-sm text-destructive"
                >
                  <AlertCircle aria-hidden className="mt-0.5 size-4 shrink-0" />
                  <span>
                    {getApiErrorMessage(apiError, "Lưu thất bại. Vui lòng kiểm tra lại dữ liệu.")}
                  </span>
                </div>
              )}

              {disabledForm && (
                <div
                  role="alert"
                  className="mb-6 flex items-start gap-2.5 rounded-2xl border border-amber-500/25 bg-amber-500/5 px-4 py-3 text-sm text-amber-600"
                >
                  <AlertCircle aria-hidden className="mt-0.5 size-4 shrink-0" />
                  <span>
                    Phiên bản này đang được <strong>Kích hoạt (ACTIVE)</strong> và đang phục vụ hệ thống. Bạn không thể sửa đổi cấu hình của phiên bản đang chạy. Vui lòng tạo phiên bản mới.
                  </span>
                </div>
              )}

              <form onSubmit={handleSubmit} className="space-y-6">
        <div className="space-y-4">
          <div>
            <label htmlFor="versionCode" className="mb-1.5 block text-sm font-600 text-foreground">
              Mã phiên bản (Version Code) <span className="text-destructive">*</span>
            </label>
            <input
              id="versionCode"
              name="versionCode"
              type="text"
              required
              readOnly
              maxLength={50}
              placeholder="Vui lòng tải file cấu hình để tự điền..."
              value={formData.versionCode}
              className="h-11 w-full rounded-xl border border-border bg-secondary/30 px-4 text-sm outline-none cursor-default text-muted-foreground"
            />
            {isEditing && (
              <p className="mt-1 text-xs text-muted-foreground">Không thể thay đổi mã phiên bản sau khi tạo.</p>
            )}
          </div>

          <div>
            <label htmlFor="description" className="mb-1.5 block text-sm font-600 text-foreground">
              Mô tả
            </label>
            <textarea
              id="description"
              name="description"
              readOnly
              rows={3}
              placeholder="Vui lòng tải file cấu hình để tự điền..."
              value={formData.description}
              className="w-full rounded-xl border border-border bg-secondary/30 p-4 text-sm outline-none cursor-default text-muted-foreground"
            />
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <label htmlFor="hfRepoId" className="mb-1.5 block text-sm font-600 text-foreground">
                HuggingFace Repo ID <span className="text-destructive">*</span>
              </label>
              <input
                id="hfRepoId"
                name="hfRepoId"
                type="text"
                required
                maxLength={255}
                readOnly
                placeholder="Tự điền..."
                value={formData.hfRepoId}
                className="h-11 w-full rounded-xl border border-border bg-secondary/30 px-4 text-sm font-mono outline-none cursor-default text-muted-foreground"
              />
            </div>
            <div>
              <label htmlFor="hfFilename" className="mb-1.5 block text-sm font-600 text-foreground">
                HuggingFace Filename <span className="text-destructive">*</span>
              </label>
              <input
                id="hfFilename"
                name="hfFilename"
                type="text"
                required
                maxLength={255}
                readOnly
                placeholder="Tự điền..."
                value={formData.hfFilename}
                className="h-11 w-full rounded-xl border border-border bg-secondary/30 px-4 text-sm font-mono outline-none cursor-default text-muted-foreground"
              />
            </div>
          </div>
          <p className="text-xs text-muted-foreground">
            Repo ID và Filename phải chính xác để Python Backend có thể tải file mô hình từ HuggingFace khi phiên bản được kích hoạt.
          </p>

          <div className="pt-2 border-t border-border">
            <h3 className="mb-4 text-sm font-600 text-foreground">Hiệu năng mô hình (Metrics)</h3>
            <div className="grid gap-4 sm:grid-cols-3">
              <div>
                <label htmlFor="metricsPrecision" className="mb-1.5 block text-xs font-500 text-muted-foreground">
                  Precision (0-100)
                </label>
                <input
                  id="metricsPrecision"
                  name="metricsPrecision"
                  type="number"
                  step="0.01"
                  min="0"
                  max="100"
                  readOnly
                  placeholder="Tự điền..."
                  value={formData.metricsPrecision ?? ""}
                  className="h-10 w-full rounded-lg border border-border bg-secondary/30 px-3 text-sm outline-none cursor-default text-muted-foreground"
                />
              </div>
              <div>
                <label htmlFor="metricsMap50" className="mb-1.5 block text-xs font-500 text-muted-foreground">
                  mAP50 (0-100)
                </label>
                <input
                  id="metricsMap50"
                  name="metricsMap50"
                  type="number"
                  step="0.01"
                  min="0"
                  max="100"
                  readOnly
                  placeholder="Tự điền..."
                  value={formData.metricsMap50 ?? ""}
                  className="h-10 w-full rounded-lg border border-border bg-secondary/30 px-3 text-sm outline-none cursor-default text-muted-foreground"
                />
              </div>
              <div>
                <label htmlFor="metricsRecall" className="mb-1.5 block text-xs font-500 text-muted-foreground">
                  Recall (0-1)
                </label>
                <input
                  id="metricsRecall"
                  name="metricsRecall"
                  type="number"
                  step="0.001"
                  min="0"
                  max="1"
                  readOnly
                  placeholder="Tự điền..."
                  value={formData.metricsRecall ?? ""}
                  className="h-10 w-full rounded-lg border border-border bg-secondary/30 px-3 text-sm outline-none cursor-default text-muted-foreground"
                />
              </div>
            </div>
          </div>
        </div>

        <div className="flex justify-end pt-4">
          <button
            type="submit"
            disabled={isPending || disabledForm}
            className="flex h-11 items-center gap-2 rounded-full bg-accent px-8 font-heading text-sm font-600 tracking-wider text-white transition-colors hover:bg-accent/90 disabled:opacity-60"
          >
            {isPending ? <Loader2 className="size-4 animate-spin" /> : <Save className="size-4" />}
            {isEditing ? "Lưu thay đổi" : "Đăng ký mô hình"}
          </button>
        </div>
              </form>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
