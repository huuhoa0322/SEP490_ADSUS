"use client";

import { AlertCircle, ArrowLeft, Loader2, Save } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { type FormEvent, useEffect, useState } from "react";

import { getApiErrorMessage } from "@/lib/api-client";
import {
  useAiModelDetail,
  useRegisterAiModel,
  useUpdateAiModel,
} from "../hooks/use-ai-models";
import type { RegisterModelVersionRequest } from "../types/ai-model.types";

interface AiModelFormProps {
  id?: string;
}

export function AiModelForm({ id }: AiModelFormProps) {
  const router = useRouter();
  const isEditing = !!id;

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
            router.push("/admin/ai-models");
          },
          onError: () => setIsSubmitError(true),
        }
      );
    } else {
      register(formData, {
        onSuccess: () => {
          router.push("/admin/ai-models");
        },
        onError: () => setIsSubmitError(true),
      });
    }
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    const { name, value, type } = e.target;
    
    setFormData((prev) => ({
      ...prev,
      [name]: type === "number" ? (value === "" ? undefined : parseFloat(value)) : value,
    }));
  };

  if (isEditing && isLoadingDetail) {
    return (
      <div className="flex min-h-96 items-center justify-center">
        <Loader2 className="size-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const isPending = isRegistering || isUpdating;
  const apiError = isEditing ? updateError : registerError;
  const disabledForm = isEditing && detail?.status === "Active";

  return (
    <div className="mx-auto w-full max-w-2xl">
      <div className="mb-6 flex items-center gap-4">
        <Link
          href="/admin/ai-models"
          className="flex size-10 items-center justify-center rounded-full bg-secondary text-muted-foreground transition-colors hover:bg-border"
        >
          <ArrowLeft className="size-5" />
        </Link>
        <div>
          <h1 className="font-heading text-2xl font-bold tracking-tight text-foreground">
            {isEditing ? `Sửa phiên bản: ${detail?.versionCode}` : "Đăng ký phiên bản AI mới"}
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            {isEditing 
              ? "Cập nhật thông tin cấu hình cho mô hình AI." 
              : "Thêm một mô hình AI mới từ HuggingFace vào hệ thống."}
          </p>
        </div>
      </div>

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

      <form onSubmit={handleSubmit} className="space-y-6 rounded-3xl border border-border bg-background p-6 shadow-sm md:p-8">
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
              disabled={isEditing}
              maxLength={50}
              placeholder="VD: yolov8-seg-v1"
              value={formData.versionCode}
              onChange={handleChange}
              className="h-11 w-full rounded-xl border border-border bg-background px-4 text-sm outline-none transition-colors focus:border-accent disabled:bg-secondary/50 disabled:text-muted-foreground"
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
              disabled={disabledForm}
              rows={3}
              placeholder="Mô tả về điểm mới, dữ liệu huấn luyện của phiên bản này..."
              value={formData.description}
              onChange={handleChange}
              className="w-full rounded-xl border border-border bg-background p-4 text-sm outline-none transition-colors focus:border-accent disabled:bg-secondary/50 disabled:text-muted-foreground"
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
                disabled={disabledForm}
                placeholder="VD: adsus/endometriosis-ai"
                value={formData.hfRepoId}
                onChange={handleChange}
                className="h-11 w-full rounded-xl border border-border bg-background px-4 text-sm font-mono outline-none transition-colors focus:border-accent disabled:bg-secondary/50 disabled:text-muted-foreground"
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
                disabled={disabledForm}
                placeholder="VD: best.pt"
                value={formData.hfFilename}
                onChange={handleChange}
                className="h-11 w-full rounded-xl border border-border bg-background px-4 text-sm font-mono outline-none transition-colors focus:border-accent disabled:bg-secondary/50 disabled:text-muted-foreground"
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
                  disabled={disabledForm}
                  value={formData.metricsPrecision ?? ""}
                  onChange={handleChange}
                  className="h-10 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition-colors focus:border-accent disabled:bg-secondary/50"
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
                  disabled={disabledForm}
                  value={formData.metricsMap50 ?? ""}
                  onChange={handleChange}
                  className="h-10 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition-colors focus:border-accent disabled:bg-secondary/50"
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
                  disabled={disabledForm}
                  value={formData.metricsRecall ?? ""}
                  onChange={handleChange}
                  className="h-10 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none transition-colors focus:border-accent disabled:bg-secondary/50"
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
    </div>
  );
}
