"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { ChevronDown, Plus, Trash2, X } from "lucide-react";
import { useRouter } from "next/navigation";
import {
  Controller,
  FormProvider,
  useFieldArray,
  useForm,
  useFormContext,
} from "react-hook-form";
import toast from "react-hot-toast";
import { useRef, useState, useEffect } from "react";
import { z } from "zod";
import { useQuery } from "@tanstack/react-query";
import { searchMedicines } from "@/features/prescriptions/api/prescriptions.api";

// ─── Schema ───────────────────────────────────────────────────────────────────

const ScheduleSlotEnum = z.enum(["Morning", "Noon", "Evening"]);

const PrescriptionItemSchema = z.object({
  medicineName: z.string().min(1, "Nhập tên thuốc"),
  dosage: z.string().min(1, "Nhập liều dùng (vd: 1 viên/1 gói)"),
  scheduleSlots: z
    .array(ScheduleSlotEnum)
    .min(1, "Chọn ít nhất 1 khung giờ uống"),
  durationDays: z.coerce
    .number()
    .int()
    .min(1, "Thời gian tối thiểu 1 ngày")
    .max(365, "Thời gian tối đa 365 ngày"),
  startDate: z.string().min(1, "Chọn ngày bắt đầu"),
  instructions: z.string().max(500, "Tối đa 500 ký tự"),
});

export const PrescriptionFormSchema = z.object({
  caseId: z.string().optional(),
  items: z.array(PrescriptionItemSchema).min(1, "Thêm ít nhất 1 loại thuốc"),
  generalNote: z.string().max(2000, "Tối đa 2000 ký tự"),
});

export type PrescriptionFormData = z.infer<typeof PrescriptionFormSchema>;
export type ScheduleSlot = z.infer<typeof ScheduleSlotEnum>;

// ─── Props ─────────────────────────────────────────────────────────────────────

interface PrefilledPatient {
  caseId: string;
  patientName: string;
  patientCode?: string;
}

interface PrescriptionFormProps {
  prefilledPatient?: PrefilledPatient;
  cases?: Array<{ caseId: string; patientName: string; patientCode: string }>;
  /** Danh mục thuốc (GET /api/v1/medication-catalog) — dùng cho autocomplete. */
  medications: Array<{ medicineId: string; name: string }>;
  /** Gọi khi submit hợp lệ. Trả về prescriptionId để form tự điều hướng. */
  onSubmit: (data: PrescriptionFormData) => Promise<{ prescriptionId: string } | void>;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function PrescriptionForm({
  prefilledPatient,
  cases,
  medications, // Note: kept for backwards compatibility but not used
  onSubmit,
}: PrescriptionFormProps) {
  const methods = useForm<PrescriptionFormData>({
    resolver: zodResolver(PrescriptionFormSchema),
    defaultValues: {
      caseId: "",
      items: [
        {
          medicineName: "",
          dosage: "",
          scheduleSlots: [] as ScheduleSlot[],
          durationDays: 30,
          startDate: new Date().toISOString().split("T")[0],
          instructions: "",
        },
      ],
      generalNote: "",
    },
  });

  const {
    register,
    handleSubmit,
    control,
    formState: { errors, isSubmitting },
  } = methods;

  const { fields, append, remove } = useFieldArray({
    control,
    name: "items",
  });

  const allSlots: ScheduleSlot[] = ["Morning", "Noon", "Evening"];

  async function onValid(data: PrescriptionFormData) {
    try {
      await onSubmit(data);
      toast.success("Kê đơn thuốc và kết thúc ca khám thành công");
    } catch {
      toast.error("Không thể lưu đơn thuốc. Vui lòng thử lại.");
    }
  }

  return (
    <FormProvider {...methods}>
      <form onSubmit={handleSubmit(onValid)} noValidate>
        {/* ── Bệnh nhân ─────────────────────────────────────────── */}
        <section className="mb-6">
          <label className="mb-1.5 block text-sm font-semibold text-navy">
            Bệnh nhân / Ca khám <span className="text-red-500">*</span>
          </label>
          {prefilledPatient ? (
            <div className="flex items-center gap-4 rounded-full border border-border bg-surface px-5 py-3">
              <span className="font-semibold text-navy">
                {prefilledPatient.patientName}
              </span>
              <span className="text-sm text-muted-foreground">
                ({prefilledPatient.patientCode})
              </span>
            </div>
          ) : (
            <select
              {...register("caseId")}
              className="w-full rounded-full border border-border bg-surface px-5 py-3 text-sm focus:border-teal focus:outline-none"
            >
              <option value="">— Chọn bệnh nhân —</option>
              {cases?.map((c) => (
                <option key={c.caseId} value={c.caseId}>
                  {c.patientName} ({c.patientCode})
                </option>
              ))}
            </select>
          )}
        </section>

        {/* ── Bảng thuốc ─────────────────────────────────────────── */}
        <section className="mb-6">
          <div className="mb-3 flex items-center justify-between">
            <h3 className="font-exo text-base font-semibold text-navy">
              Bảng Kê Đơn thuốc Điều trị Nội khoa
            </h3>
            <button
              type="button"
              onClick={() =>
                append({
                  medicineName: "",
                  dosage: "",
                  scheduleSlots: [] as ScheduleSlot[],
                  durationDays: 30,
                  startDate: new Date().toISOString().split("T")[0],
                  instructions: "",
                })
              }
              className="flex items-center gap-1.5 rounded-full border border-teal px-4 py-1.5 text-xs font-semibold text-teal transition hover:bg-teal/5"
            >
              <Plus className="size-3.5" />
              THÊM THUỐC
            </button>
          </div>

          {/* Header */}
          <div className="mb-1 grid grid-cols-12 gap-2 px-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            <div className="col-span-4">Tên thuốc</div>
            <div className="col-span-2">Liều dùng</div>
            <div className="col-span-3">Khung giờ</div>
            <div className="col-span-1 text-center">Ngày</div>
            <div className="col-span-2">Cách dùng</div>
          </div>

          {/* rows */}
          <div className="space-y-3">
            {fields.map((field, index) => (
              <MedicationRow
                key={field.id}
                index={index}
                medications={medications}
                allSlots={allSlots}
                register={register}
                control={control}
                errors={errors?.items?.[index]}
                onRemove={fields.length > 1 ? () => remove(index) : undefined}
              />
            ))}
          </div>

          {errors.items?.root && (
            <p className="mt-2 text-xs text-red-500">
              {errors.items.root.message}
            </p>
          )}
        </section>

        {/* ── Ghi chú ─────────────────────────────────────────── */}
        <section className="mb-8">
          <label className="mb-1.5 block text-sm font-semibold text-navy">
            Ghi chú Chuyên môn & Lời dặn Bác sĩ
          </label>
          <input
            type="text"
            {...register("generalNote")}
            placeholder="Ví dụ: Theo dõi lượng máu kinh hàng ngày trên App Mobile..."
            className="w-full rounded-full border border-border bg-surface px-5 py-3 text-sm focus:border-teal focus:outline-none"
          />
          {errors.generalNote && (
            <p className="mt-1 text-xs text-red-500">
              {errors.generalNote.message}
            </p>
          )}
        </section>

        {/* ── Submit ─────────────────────────────────────────────── */}
        <div className="flex flex-col gap-2">
          <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-2 text-sm text-amber-800">
            ⚠ Đơn thuốc chỉ được tạo <strong>một lần duy nhất</strong>. Vui lòng kiểm tra kỹ trước khi bấm xác nhận.
          </div>
          <div className="flex justify-end">
            <button
              type="submit"
              disabled={isSubmitting}
              className="flex items-center gap-2 rounded-lg bg-primary px-6 py-3 text-base font-semibold text-primary-foreground shadow-sm transition hover:bg-primary/90 disabled:opacity-60"
            >
              {isSubmitting ? (
                <span>Đang gửi…</span>
              ) : (
                <>
                  <span>✓</span>
                  <span>Kê đơn thuốc và kết thúc ca khám</span>
                </>
              )}
            </button>
          </div>
        </div>
      </form>
    </FormProvider>
  );
}

// ─── MedicineCombobox ───────────────────────────────────────────────────────────

interface MedicineComboboxProps {
  value: string;
  onChange: (value: string) => void;
  medications: Array<{ medicineId: string; name: string }>;
  error?: string;
}

function MedicineCombobox({
  value,
  onChange,
  medications, // old static prop, no longer strictly used for filtering, but we'll ignore it
  error,
}: MedicineComboboxProps) {
  const [open, setOpen] = useState(false);
  const [inputValue, setInputValue] = useState(value);
  const [debouncedSearch, setDebouncedSearch] = useState(value);
  const [prevValue, setPrevValue] = useState(value);
  const wrapperRef = useRef<HTMLDivElement>(null);

  if (value !== prevValue) {
    setPrevValue(value);
    setInputValue(value);
  }

  // Debounce input value for API calls
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(inputValue), 300);
    return () => clearTimeout(timer);
  }, [inputValue]);

  const { data: searchResults, isLoading } = useQuery({
    queryKey: ["search-medicines", debouncedSearch],
    queryFn: () => searchMedicines(debouncedSearch),
    enabled: open, // Only fetch when dropdown is open
    staleTime: 60 * 1000,
  });

  const filtered = searchResults ?? [];

  function handleSelect(name: string) {
    onChange(name);
    setInputValue(name);
    setOpen(false);
  }

  function handleClear() {
    onChange("");
    setInputValue("");
    setOpen(false);
  }

  // Close on outside click
  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  return (
    <div className="relative" ref={wrapperRef}>
      <div className="flex items-center gap-1">
        <input
          type="text"
          value={inputValue}
          onChange={(e) => {
            setInputValue(e.target.value);
            onChange(e.target.value);
            setOpen(true);
          }}
          onFocus={() => setOpen(true)}
          placeholder="Nhập tên thuốc..."
          className="w-full rounded-full border border-border bg-white px-3 py-2 text-xs focus:border-teal focus:outline-none"
        />
        {inputValue && (
          <button
            type="button"
            onClick={handleClear}
            className="shrink-0 rounded-full p-1 text-muted-foreground hover:bg-red-50 hover:text-red-400"
          >
            <X className="size-3" />
          </button>
        )}
        <button
          type="button"
          onClick={() => setOpen((o) => !o)}
          className="shrink-0 rounded-full border border-border p-1.5 text-muted-foreground hover:bg-gray-50"
        >
          <ChevronDown className="size-3" />
        </button>
      </div>

      {open && (
        <ul className="absolute left-0 top-full z-50 mt-1 max-h-48 w-full overflow-auto rounded-xl border border-border bg-white shadow-lg">
          {isLoading ? (
            <li className="px-3 py-2 text-xs text-muted-foreground text-center">Đang tìm...</li>
          ) : filtered.length === 0 ? (
            <li className="px-3 py-2 text-xs text-muted-foreground">
              Chưa có trong danh mục (sẽ tự động thêm mới)
            </li>
          ) : (
            filtered.map((m) => (
              <li key={m.medicineId ?? m.name}>
                <button
                  type="button"
                  onClick={() => handleSelect(m.name)}
                  className="w-full px-3 py-2 text-left text-xs hover:bg-teal/5"
                >
                  {m.name}
                </button>
              </li>
            ))
          )}
        </ul>
      )}

      {error && <p className="mt-0.5 text-xs text-red-500">{error}</p>}
    </div>
  );
}

// ─── MedicationRow ───────────────────────────────────────────────────────────

interface MedicationRowProps {
  index: number;
  medications: Array<{ medicineId: string; name: string }>;
  allSlots: ScheduleSlot[];
  register: ReturnType<typeof useForm<PrescriptionFormData>>["register"];
  errors?: {
    medicineName?: { message?: string };
    dosage?: { message?: string };
    scheduleSlots?: { message?: string };
    durationDays?: { message?: string };
    startDate?: { message?: string };
    instructions?: { message?: string };
  };
  onRemove?: () => void;
  control: ReturnType<typeof useForm<PrescriptionFormData>>["control"];
}

function MedicationRow({
  index,
  medications,
  allSlots,
  register,
  control,
  errors,
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  onRemove,
}: MedicationRowProps) {
  const { watch } = useFormContext<PrescriptionFormData>();
  const watchedMedicineName = watch(`items.${index}.medicineName`) ?? "";

  return (
    <div className="grid grid-cols-12 items-start gap-2 rounded-2xl border border-border bg-surface p-3">
      {/* Medicine name — autocomplete combobox */}
      <div className="col-span-4">
        <MedicineCombobox
          value={watchedMedicineName}
          onChange={(v) => {
            const event = {
              target: { name: `items.${index}.medicineName`, value: v },
            } as React.ChangeEvent<HTMLInputElement>;
            register(`items.${index}.medicineName`).onChange(event);
          }}
          medications={medications}
          error={errors?.medicineName?.message}
        />
      </div>

      {/* Dosage — free text input */}
      <div className="col-span-2">
        <input
          type="text"
          {...register(`items.${index}.dosage`)}
          placeholder="VD: 1 viên, 1 gói"
          className="w-full rounded-full border border-border bg-white px-3 py-2 text-xs focus:border-teal focus:outline-none"
        />
        {errors?.dosage && (
          <p className="mt-0.5 text-xs text-red-500">{errors.dosage.message}</p>
        )}
      </div>

      {/* Schedule slots (checkboxes) */}
      <div className="col-span-3 flex flex-wrap gap-1.5 pt-2">
        {allSlots.map((slot) => {
          const slotKey = `items.${index}.scheduleSlots` as const;
          return (
            <Controller
              key={slot}
              name={slotKey}
              control={control}
              render={({ field }) => {
                const checked = (field.value as ScheduleSlot[] | undefined)?.includes(slot);
                return (
                  <label
                    className={`flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs font-medium transition cursor-pointer ${
                      checked
                        ? "border-teal bg-teal/10 text-teal"
                        : "border-border text-muted-foreground"
                    }`}
                  >
                    <input
                      type="checkbox"
                      className="sr-only"
                      checked={!!checked}
                      onChange={(e) => {
                        const current = (field.value as ScheduleSlot[]) ?? [];
                        if (e.target.checked) {
                          field.onChange([...current, slot]);
                        } else {
                          field.onChange(current.filter((s) => s !== slot));
                        }
                      }}
                    />
                    {slot === "Morning" ? "Sáng" : slot === "Noon" ? "Trưa" : "Tối"}
                  </label>
                );
              }}
            />
          );
        })}
        {errors?.scheduleSlots && (
          <p className="w-full text-xs text-red-500">
            {errors.scheduleSlots.message}
          </p>
        )}
      </div>

      {/* Duration days */}
      <div className="col-span-1">
        <input
          type="number"
          {...register(`items.${index}.durationDays`)}
          min={1}
          max={365}
          className="w-full rounded-full border border-border bg-white px-1 py-2 text-center text-xs focus:border-teal focus:outline-none"
        />
        {errors?.durationDays && (
          <p className="mt-0.5 text-xs text-red-500">
            {errors.durationDays.message}
          </p>
        )}
      </div>

      {/* Instructions — textarea */}
      <div className="col-span-2">
        <textarea
          {...register(`items.${index}.instructions`)}
          rows={2}
          placeholder="VD: Uống sau ăn 30 phút"
          className="w-full resize-none rounded-xl border border-border bg-white px-2 py-1.5 text-xs focus:border-teal focus:outline-none"
        />
        {errors?.instructions && (
          <p className="mt-0.5 text-xs text-red-500">
            {errors.instructions.message}
          </p>
        )}
      </div>

      {/* Remove button */}
      <div className="col-span-12 flex justify-end">
        {onRemove && (
          <button
            type="button"
            onClick={onRemove}
            className="flex items-center gap-1 rounded-full px-3 py-1 text-xs text-muted-foreground transition hover:bg-red-50 hover:text-red-400"
          >
            <Trash2 className="size-3.5" />
            Xóa
          </button>
        )}
      </div>
    </div>
  );
}
