"use client";

import { Loader2, Plus, X } from "lucide-react";
import { useMemo, useState } from "react";

import { getApiErrorMessage } from "@/lib/api-client";

import {
  useCloseScheduleSlot,
  useCreateScheduleSlot,
  useScheduleSlots,
} from "../hooks/use-schedule-slot";
import type {
  CreateScheduleSlotRequest,
  ScheduleSlotResponse,
  SlotStatus,
} from "../types/schedule-slot.types";

const STATUS_LABELS: Record<SlotStatus, string> = {
  OPEN: "Đang mở",
  CLOSED: "Đã đóng",
};

const STATUS_STYLES: Record<SlotStatus, string> = {
  OPEN: "bg-green-100 text-green-800",
  CLOSED: "bg-slate-200 text-slate-700",
};

/**
 * SCR-20 — Manage Schedule Slots (UC-15, Web).
 * Doctor/Nurse: list slot trong tuần, tạo slot mới, đóng slot (có confirm nếu có booking).
 */
export function ScheduleSlotManagementView() {
  const today = useMemo(() => isoDate(new Date()), []);
  const inSevenDays = useMemo(() => isoDate(addDays(new Date(), 7)), []);

  const [fromDate, setFromDate] = useState<string>(today);
  const [toDate, setToDate] = useState<string>(inSevenDays);
  const [showCreate, setShowCreate] = useState(false);

  const listQuery = useScheduleSlots({ fromDate, toDate });
  const createMutation = useCreateScheduleSlot();
  const closeMutation = useCloseScheduleSlot();

  const slots = listQuery.data ?? [];

  return (
    <div className="space-y-6">
      <header className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Quản lý lịch khám</h1>
          <p className="text-sm text-slate-500">
            Tạo và đóng khung giờ khám (UC-15).
          </p>
        </div>
        <button
          type="button"
          onClick={() => setShowCreate(true)}
          className="inline-flex items-center gap-2 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
        >
          <Plus className="h-4 w-4" /> Thêm khung giờ
        </button>
      </header>

      <section className="flex flex-wrap items-end gap-3 rounded-md border border-slate-200 bg-white p-4">
        <label className="flex flex-col text-sm">
          <span className="mb-1 text-slate-600">Từ ngày</span>
          <input
            type="date"
            value={fromDate}
            onChange={(e) => setFromDate(e.target.value)}
            className="rounded-md border border-slate-300 px-3 py-2"
          />
        </label>
        <label className="flex flex-col text-sm">
          <span className="mb-1 text-slate-600">Đến ngày</span>
          <input
            type="date"
            value={toDate}
            onChange={(e) => setToDate(e.target.value)}
            className="rounded-md border border-slate-300 px-3 py-2"
          />
        </label>
      </section>

      <section className="rounded-md border border-slate-200 bg-white">
        {listQuery.isLoading ? (
          <div className="flex items-center justify-center gap-2 p-8 text-slate-500">
            <Loader2 className="h-4 w-4 animate-spin" /> Đang tải…
          </div>
        ) : listQuery.isError ? (
          <div className="p-8 text-sm text-red-600">
            {getApiErrorMessage(listQuery.error, "Không tải được danh sách khung giờ.")}
          </div>
        ) : slots.length === 0 ? (
          <div className="p-8 text-sm text-slate-500">
            Không có khung giờ nào trong khoảng ngày đã chọn.
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-left text-slate-600">
              <tr>
                <th className="px-4 py-2">Ngày</th>
                <th className="px-4 py-2">Giờ</th>
                <th className="px-4 py-2">Bác sĩ</th>
                <th className="px-4 py-2">Trạng thái</th>
                <th className="px-4 py-2">Booking</th>
                <th className="px-4 py-2"></th>
              </tr>
            </thead>
            <tbody>
              {slots.map((slot) => (
                <SlotRow
                  key={slot.slotId}
                  slot={slot}
                  onClose={async (force) => {
                    try {
                      const impact = await closeMutation.mutateAsync({
                        id: slot.slotId,
                        force,
                      });
                      if (impact.affectedBookingsCount > 0 && force) {
                        alert(
                          `Đã đóng khung giờ. ${impact.affectedBookingsCount} booking vẫn ở trạng thái BOOKED.`,
                        );
                      }
                    } catch (err) {
                      const apiErr = err as {
                        response?: { data?: { data?: { affectedBookingsCount?: number } } };
                      };
                      const bookings = apiErr?.response?.data?.data?.affectedBookingsCount;
                      if (bookings && bookings > 0) {
                        const ok = confirm(
                          `Khung giờ này có ${bookings} lịch hẹn đang BOOKED. Bạn có chắc muốn đóng?`,
                        );
                        if (ok) {
                          await closeMutation.mutateAsync({
                            id: slot.slotId,
                            force: true,
                          });
                        }
                      } else {
                        alert(getApiErrorMessage(err, "Không đóng được khung giờ."));
                      }
                    }
                  }}
                />
              ))}
            </tbody>
          </table>
        )}
      </section>

      {showCreate && (
        <CreateSlotModal
          onClose={() => setShowCreate(false)}
          onSubmit={async (payload) => {
            try {
              await createMutation.mutateAsync(payload);
              setShowCreate(false);
            } catch (err) {
              alert(getApiErrorMessage(err, "Không tạo được khung giờ."));
            }
          }}
          submitting={createMutation.isPending}
        />
      )}
    </div>
  );
}

function SlotRow({
  slot,
  onClose,
}: {
  slot: ScheduleSlotResponse;
  onClose: (force: boolean) => void | Promise<void>;
}) {
  return (
    <tr className="border-t border-slate-100">
      <td className="px-4 py-2">{slot.slotDate}</td>
      <td className="px-4 py-2 font-mono">
        {slot.startTime.slice(0, 5)}–{slot.endTime.slice(0, 5)}
      </td>
      <td className="px-4 py-2">{slot.doctorName || slot.doctorId.slice(0, 8)}</td>
      <td className="px-4 py-2">
        <span className={`rounded-full px-2 py-0.5 text-xs ${STATUS_STYLES[slot.status]}`}>
          {STATUS_LABELS[slot.status]}
        </span>
      </td>
      <td className="px-4 py-2">{slot.activeAppointmentsCount}</td>
      <td className="px-4 py-2 text-right">
        {slot.status === "OPEN" && (
          <button
            type="button"
            onClick={() => void onClose(false)}
            className="inline-flex items-center gap-1 rounded-md border border-slate-300 px-3 py-1 text-xs hover:bg-slate-50"
          >
            <X className="h-3 w-3" /> Đóng
          </button>
        )}
      </td>
    </tr>
  );
}

function CreateSlotModal({
  onClose,
  onSubmit,
  submitting,
}: {
  onClose: () => void;
  onSubmit: (payload: CreateScheduleSlotRequest) => void | Promise<void>;
  submitting: boolean;
}) {
  const [doctorId, setDoctorId] = useState("");
  const [visitDate, setVisitDate] = useState(isoDate(new Date()));
  const [startTime, setStartTime] = useState("08:00:00");
  const [endTime, setEndTime] = useState("09:00:00");

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl">
        <h2 className="mb-4 text-lg font-semibold">Tạo khung giờ mới</h2>
        <form
          className="space-y-3"
          onSubmit={(e) => {
            e.preventDefault();
            void onSubmit({ doctorId, visitDate, startTime, endTime });
          }}
        >
          <label className="block text-sm">
            <span className="text-slate-600">Doctor ID (UUID)</span>
            <input
              required
              value={doctorId}
              onChange={(e) => setDoctorId(e.target.value)}
              placeholder="00000000-0000-0000-0000-000000000000"
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2"
            />
          </label>
          <label className="block text-sm">
            <span className="text-slate-600">Ngày khám</span>
            <input
              required
              type="date"
              value={visitDate}
              onChange={(e) => setVisitDate(e.target.value)}
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2"
            />
          </label>
          <div className="grid grid-cols-2 gap-3">
            <label className="block text-sm">
              <span className="text-slate-600">Bắt đầu (HH:mm:ss, 24h)</span>
              <input
                required
                type="text"
                pattern="^\d{2}:\d{2}:\d{2}$"
                placeholder="08:00:00"
                value={startTime}
                onChange={(e) => setStartTime(e.target.value)}
                className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 font-mono"
              />
            </label>
            <label className="block text-sm">
              <span className="text-slate-600">Kết thúc (HH:mm:ss, 24h)</span>
              <input
                required
                type="text"
                pattern="^\d{2}:\d{2}:\d{2}$"
                placeholder="09:00:00"
                value={endTime}
                onChange={(e) => setEndTime(e.target.value)}
                className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 font-mono"
              />
            </label>
          </div>
          <p className="text-xs text-slate-500">
            BR-01: range &gt; 15 phút, không trùng giờ với khung khác cùng Doctor.
          </p>
          <div className="mt-4 flex justify-end gap-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded-md border border-slate-300 px-4 py-2 text-sm"
            >
              Hủy
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="inline-flex items-center gap-2 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {submitting && <Loader2 className="h-4 w-4 animate-spin" />}
              Tạo khung giờ
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function isoDate(d: Date): string {
  const yyyy = d.getFullYear();
  const mm = String(d.getMonth() + 1).padStart(2, "0");
  const dd = String(d.getDate()).padStart(2, "0");
  return `${yyyy}-${mm}-${dd}`;
}

function addDays(d: Date, n: number): Date {
  const next = new Date(d);
  next.setDate(d.getDate() + n);
  return next;
}
