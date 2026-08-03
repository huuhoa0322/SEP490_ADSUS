"use client";

import { useState } from "react";

import { useCreatePrescription } from "../hooks/use-prescriptions";
import type {
  CreatePrescriptionItemRequest,
  MedicineItem,
  ScheduleSlotType,
} from "../types/prescription.types";
import { MedicineCombobox } from "./medicine-combobox";

interface PrescribeMedicationFormProps {
  caseId: string;
  patientProfileId: string;
  onSuccess?: (prescriptionId: string) => void;
}

interface ItemDraft {
  medicine: MedicineItem | null;
  dosage: string;
  durationDays: number;
  startDate: string;
  instructions: string;
  slots: { morning: boolean; noon: boolean; evening: boolean };
}

const SLOT_LABELS: Record<"morning" | "noon" | "evening", string> = {
  morning: "Sáng",
  noon: "Trưa",
  evening: "Tối",
};

const emptyItem = (): ItemDraft => ({
  medicine: null,
  dosage: "",
  durationDays: 7,
  startDate: new Date().toISOString().slice(0, 10),
  instructions: "",
  slots: { morning: true, noon: false, evening: false },
});

/**
 * Module 7 UC-18 — form kê đơn (Doctor only).
 *   - 1 đơn ≥ 1 thuốc, mỗi thuốc có ít nhất 1 khung giờ trong ngày
 *   - DoctorId lấy từ JWT (BE), không truyền từ form
 *   - Sau khi kê thành công → gọi onSuccess(prescriptionId) để redirect
 */
export function PrescribeMedicationForm({
  caseId,
  patientProfileId: _patientProfileId,
  onSuccess,
}: PrescribeMedicationFormProps) {
  const [generalNote, setGeneralNote] = useState("");
  const [items, setItems] = useState<ItemDraft[]>([emptyItem()]);
  const [error, setError] = useState<string | null>(null);

  const create = useCreatePrescription();

  function updateItem(idx: number, patch: Partial<ItemDraft>) {
    setItems((prev) => prev.map((it, i) => (i === idx ? { ...it, ...patch } : it)));
  }

  function removeItem(idx: number) {
    setItems((prev) => prev.filter((_, i) => i !== idx));
  }

  function addItem() {
    setItems((prev) => [...prev, emptyItem()]);
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);

    const payloadItems: CreatePrescriptionItemRequest[] = items
      .filter((it) => it.medicine && it.dosage && it.durationDays > 0)
      .map((it) => {
        const scheduleSlots: { slot: ScheduleSlotType }[] = [];
        if (it.slots.morning) scheduleSlots.push({ slot: "MORNING" });
        if (it.slots.noon) scheduleSlots.push({ slot: "NOON" });
        if (it.slots.evening) scheduleSlots.push({ slot: "EVENING" });
        return {
          medicineId: it.medicine!.medicineId,
          dosage: it.dosage,
          durationDays: it.durationDays,
          startDate: it.startDate,
          instructions: it.instructions || null,
          scheduleSlots,
        };
      });

    if (payloadItems.length === 0) {
      setError("Đơn thuốc phải có ít nhất 1 dòng thuốc hợp lệ (có tên, liều, số ngày > 0).");
      return;
    }

    try {
      const created = await create.mutateAsync({
        caseId,
        generalNote: generalNote || null,
        items: payloadItems,
      });
      onSuccess?.(created.prescriptionId);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Kê đơn thất bại.");
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      <section>
        <label className="mb-1 block text-sm font-medium">Ghi chú chung (tùy chọn)</label>
        <textarea
          value={generalNote}
          onChange={(e) => setGeneralNote(e.target.value)}
          rows={2}
          className="w-full rounded-md border bg-background px-3 py-2 text-sm"
          placeholder="VD: Uống sau bữa ăn, tái khám sau 2 tuần..."
        />
      </section>

      <section className="space-y-4">
        {items.map((it, idx) => (
          <div key={idx} className="rounded-lg border bg-card p-4">
            <div className="mb-3 flex items-center justify-between">
              <h3 className="font-medium">Thuốc #{idx + 1}</h3>
              {items.length > 1 && (
                <button
                  type="button"
                  onClick={() => removeItem(idx)}
                  className="text-sm text-red-500 hover:underline"
                >
                  Xóa
                </button>
              )}
            </div>

            <div className="grid gap-3 md:grid-cols-2">
              <div>
                <label className="mb-1 block text-xs font-medium">Tên thuốc</label>
                <MedicineCombobox
                  value={it.medicine}
                  onChange={(m) => updateItem(idx, { medicine: m })}
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium">Liều lượng</label>
                <input
                  type="text"
                  value={it.dosage}
                  onChange={(e) => updateItem(idx, { dosage: e.target.value })}
                  placeholder="500mg"
                  className="w-full rounded-md border bg-background px-3 py-2 text-sm"
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium">Số ngày</label>
                <input
                  type="number"
                  min={1}
                  max={365}
                  value={it.durationDays}
                  onChange={(e) => updateItem(idx, { durationDays: Number(e.target.value) })}
                  className="w-full rounded-md border bg-background px-3 py-2 text-sm"
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium">Ngày bắt đầu</label>
                <input
                  type="date"
                  value={it.startDate}
                  onChange={(e) => updateItem(idx, { startDate: e.target.value })}
                  className="w-full rounded-md border bg-background px-3 py-2 text-sm"
                />
              </div>
            </div>

            <div className="mt-3">
              <label className="mb-1 block text-xs font-medium">Hướng dẫn</label>
              <input
                type="text"
                value={it.instructions}
                onChange={(e) => updateItem(idx, { instructions: e.target.value })}
                placeholder="Uống sau bữa ăn"
                className="w-full rounded-md border bg-background px-3 py-2 text-sm"
              />
            </div>

            <div className="mt-3">
              <label className="mb-1 block text-xs font-medium">Khung giờ uống</label>
              <div className="flex gap-3">
                {(Object.keys(SLOT_LABELS) as Array<keyof typeof SLOT_LABELS>).map((slot) => (
                  <label key={slot} className="flex items-center gap-1 text-sm">
                    <input
                      type="checkbox"
                      checked={it.slots[slot]}
                      onChange={(e) =>
                        updateItem(idx, { slots: { ...it.slots, [slot]: e.target.checked } })
                      }
                    />
                    {SLOT_LABELS[slot]}
                  </label>
                ))}
              </div>
            </div>
          </div>
        ))}

        <button
          type="button"
          onClick={addItem}
          className="rounded-md border border-dashed px-4 py-2 text-sm text-[#4488be] hover:bg-muted"
        >
          + Thêm thuốc
        </button>
      </section>

      {error && <p className="text-sm text-red-500">{error}</p>}

      <button
        type="submit"
        disabled={create.isPending}
        className="rounded-full bg-[#1cba9f] px-6 py-2 font-medium text-white transition hover:bg-[#1cba9f]/90 disabled:opacity-50"
      >
        {create.isPending ? "Đang kê đơn..." : "Hoàn tất đơn thuốc"}
      </button>
    </form>
  );
}
