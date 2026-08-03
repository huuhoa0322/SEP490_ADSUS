"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

import { useCreateScheduleSlot } from "../hooks/use-schedule-slots";
import type { CreateScheduleSlotRequest } from "../types/schedule-slot.types";

interface Props {
  doctorId: string;
  doctorName: string;
}

export function ScheduleSlotCreateForm({ doctorId, doctorName }: Props) {
  const router = useRouter();
  const today = new Date().toISOString().slice(0, 10);
  const [slotDate, setSlotDate] = useState(today);
  const [startTime, setStartTime] = useState("09:00");
  const [endTime, setEndTime] = useState("10:00");
  const [error, setError] = useState<string | null>(null);

  const create = useCreateScheduleSlot();

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    const payload: CreateScheduleSlotRequest = {
      doctorId,
      slotDate,
      startTime,
      endTime,
    };
    try {
      const created = await create.mutateAsync(payload);
      router.push(`/schedule-slots/${created.slotId}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Tạo thất bại.");
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <p className="text-sm text-muted-foreground">Bác sĩ: {doctorName}</p>
      <div className="grid gap-3 md:grid-cols-3">
        <div>
          <label className="mb-1 block text-xs font-medium">Ngày</label>
          <input
            type="date"
            value={slotDate}
            min={today}
            onChange={(e) => setSlotDate(e.target.value)}
            required
            className="w-full rounded-md border bg-background px-3 py-2 text-sm"
          />
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium">Bắt đầu</label>
          <input
            type="time"
            value={startTime}
            onChange={(e) => setStartTime(e.target.value)}
            required
            className="w-full rounded-md border bg-background px-3 py-2 text-sm"
          />
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium">Kết thúc</label>
          <input
            type="time"
            value={endTime}
            onChange={(e) => setEndTime(e.target.value)}
            required
            className="w-full rounded-md border bg-background px-3 py-2 text-sm"
          />
        </div>
      </div>
      {error && <p className="text-sm text-red-500">{error}</p>}
      <button
        type="submit"
        disabled={create.isPending || !slotDate}
        className="rounded-full bg-[#1cba9f] px-6 py-2 font-medium text-white hover:bg-[#1cba9f]/90 disabled:opacity-50"
      >
        {create.isPending ? "Đang tạo..." : "Tạo khung giờ"}
      </button>
    </form>
  );
}