"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Plus, Trash2 } from "lucide-react";
import {
  Controller,
  FormProvider,
  useFieldArray,
  useForm,
  useFormContext,
} from "react-hook-form";
import toast from "react-hot-toast";
import { z } from "zod";

// ─── Schema ───────────────────────────────────────────────────────────────────

const ScheduleSlotEnum = z.enum(["Morning", "Noon", "Evening"]);

const PrescriptionItemSchema = z.object({
  medicineId: z.string().min(1, "Chọn thuốc"),
  dosage: z.string().min(1, "Nhập liều dùng (vd: 1 viên)"),
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
  // caseId chỉ bắt buộc khi KHÔNG có prefilledPatient
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
  /** Thông tin bệnh nhân đã prefilled (từ case detail - Module 7 Phương án A). */
  prefilledPatient?: PrefilledPatient;
  /** Danh sách ca khám để chọn (mode cũ - dropdown). */
  cases?: Array<{ caseId: string; patientName: string; patientCode: string }>;
  /** Danh mục thuốc (GET /api/v1/medication-catalog). */
  medications: Array<{ medicineId: string; name: string }>;
  /** Gọi khi submit hợp lệ. */
  onSubmit: (data: PrescriptionFormData) => Promise<void>;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function PrescriptionForm({
  prefilledPatient,
  cases,
  medications,
  onSubmit,
}: PrescriptionFormProps) {
  const methods = useForm<PrescriptionFormData>({
    resolver: zodResolver(PrescriptionFormSchema),
    defaultValues: {
      caseId: "",
      items: [
        {
          medicineId: "",
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
      toast.success("Đơn thuốc đã được gửi đến bệnh nhân");
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
            // Module 7 Phương án A: hiển thị thông tin bệnh nhân cố định
            <div className="flex items-center gap-4 rounded-full border border-border bg-surface px-5 py-3">
              <span className="font-semibold text-navy">
                {prefilledPatient.patientName}
              </span>
              <span className="text-sm text-muted-foreground">
                ({prefilledPatient.patientCode})
              </span>
            </div>
          ) : (
            // Mode cũ: dropdown chọn bệnh nhân
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
                  medicineId: "",
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
            <div className="col-span-3">Tên thuốc</div>
            <div className="col-span-2">Liều dùng</div>
            <div className="col-span-3">Khung giờ</div>
            <div className="col-span-1 text-center">Ngày</div>
            <div className="col-span-2">Cách dùng</div>
            <div className="col-span-1" />
          </div>

          {/* Rows */}
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
        <div className="flex justify-end">
          <button
            type="submit"
            disabled={isSubmitting}
            className="flex items-center gap-2 rounded-full bg-teal px-8 py-3.5 font-semibold text-white transition hover:bg-teal/90 disabled:opacity-60"
          >
            {isSubmitting ? (
              <span>Đang gửi…</span>
            ) : (
              <>
                <span>✓</span>
                <span>XÁC NHẬN KÊ ĐƠN & GỬI ĐẾN APP MOBILE</span>
              </>
            )}
          </button>
        </div>
      </form>
    </FormProvider>
  );
}

// ─── MedicationRow ───────────────────────────────────────────────────────────

interface MedicationRowProps {
  index: number;
  medications: Array<{ medicineId: string; name: string }>;
  allSlots: ScheduleSlot[];
  register: ReturnType<typeof useForm<PrescriptionFormData>>["register"];
  /** Errors flatten từ useFormState.errors.items[index]. */
  errors?: {
    medicineId?: { message?: string };
    dosage?: { message?: string };
    scheduleSlots?: { message?: string };
    durationDays?: { message?: string };
    startDate?: { message?: string };
  };
  onRemove?: () => void;
  /** Dùng cho Controller checkbox slots. */
  control: ReturnType<typeof useForm<PrescriptionFormData>>["control"];
}

/** MedicationRow dùng useFormContext để truy cập watch() mà không cần prop drilling.
 *  Yêu cầu: component cha (PrescriptionForm) phải bọc <FormProvider {...methods}>. */

function MedicationRow({
  index,
  medications,
  allSlots,
  register,
  control,
  errors,
  onRemove,
}: MedicationRowProps) {
  const { watch } = useFormContext<PrescriptionFormData>();
  const watchedSlots = watch(`items.${index}.scheduleSlots`) as ScheduleSlot[];

  return (
    <div className="grid grid-cols-12 items-start gap-2 rounded-2xl border border-border bg-surface p-3">
      {/* Medicine select */}
      <div className="col-span-3">
        <select
          {...register(`items.${index}.medicineId`)}
          className="w-full rounded-full border border-border bg-white px-3 py-2 text-xs focus:border-teal focus:outline-none"
        >
          <option value="">— Chọn thuốc —</option>
          {medications.map((m) => (
            <option key={m.medicineId} value={m.medicineId}>
              {m.name}
            </option>
          ))}
        </select>
        {errors?.medicineId && (
          <p className="mt-0.5 text-xs text-red-500">{errors.medicineId.message}</p>
        )}
      </div>

      {/* Dosage */}
      <div className="col-span-2">
        <input
          type="text"
          {...register(`items.${index}.dosage`)}
          placeholder="1 viên"
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

      {/* Start date */}
      <div className="col-span-2">
        <input
          type="date"
          {...register(`items.${index}.startDate`)}
          className="w-full rounded-full border border-border bg-white px-2 py-2 text-xs focus:border-teal focus:outline-none"
        />
        {errors?.startDate && (
          <p className="mt-0.5 text-xs text-red-500">{errors.startDate.message}</p>
        )}
      </div>

      {/* Instructions */}
      <div className="col-span-1 flex items-start justify-center pt-2">
        {onRemove && (
          <button
            type="button"
            onClick={onRemove}
            className="rounded-full p-1 text-muted-foreground transition hover:bg-red-50 hover:text-red-500"
          >
            <Trash2 className="size-4" />
          </button>
        )}
      </div>
    </div>
  );
}