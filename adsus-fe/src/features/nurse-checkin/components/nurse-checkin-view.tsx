"use client";

import { useState } from "react";
import { format } from "date-fns";
import { vi } from "date-fns/locale";
import { Search, RefreshCw, Check, Clock } from "lucide-react";
import { useCheckinQueue } from "../hooks/use-checkin-queue";
import { useCheckin } from "../hooks/use-checkin";
import { CheckinQueueTable } from "./checkin-queue-table";

export function NurseCheckinView() {
  const [search, setSearch] = useState("");
  const today = format(new Date(), "yyyy-MM-dd");

  const { data, isLoading, refetch, isRefetching } = useCheckinQueue(today);
  const { mutate: checkin, isPending: isCheckingIn } = useCheckin();

  const queue = data?.items ?? [];
  const checkedIn = queue.filter((item) => item.status === "Approved");
  const pending = queue.filter((item) => item.status === "Booked");

  // Filter by search (client-side for instant feedback)
  const filteredQueue = search
    ? queue.filter(
        (item) =>
          item.patientFullName.toLowerCase().includes(search.toLowerCase()) ||
          item.patientPhone?.includes(search)
      )
    : queue;

  const handleCheckin = (item: (typeof queue)[0]) => {
    checkin({ appointmentId: item.appointmentId, caseId: item.caseId });
  };

  return (
    <div className="mx-auto w-full max-w-screen-2xl px-6 py-10">
      <header className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Nurse Check-In</h1>
          <p className="text-muted-foreground">
            {format(new Date(), "EEEE, dd/MM/yyyy", { locale: vi })}
          </p>
        </div>
        <button
          onClick={() => refetch()}
          disabled={isRefetching}
          className="flex items-center gap-2 rounded-md border bg-background px-3 py-2 text-sm text-muted-foreground hover:text-foreground disabled:opacity-50"
        >
          <RefreshCw className={`h-4 w-4 ${isRefetching ? "animate-spin" : ""}`} />
          Làm mới
        </button>
      </header>

      {/* Search */}
      <div className="mb-6">
        <div className="relative">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <input
            type="text"
            placeholder="Tìm kiếm bệnh nhân (tên, số điện thoại)..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full rounded-md border border-input bg-background py-2 pl-10 pr-4 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
          />
        </div>
      </div>

      {/* Stats */}
      <div className="mb-6 flex gap-4">
        <div className="flex items-center gap-3 rounded-lg bg-green-50 p-4 text-green-700">
          <div className="flex h-10 w-10 items-center justify-center rounded-full bg-green-100">
            <Check className="h-5 w-5" />
          </div>
          <div>
            <p className="text-sm font-medium">Đã check-in</p>
            <p className="text-2xl font-bold">{checkedIn.length}</p>
          </div>
        </div>
        <div className="flex items-center gap-3 rounded-lg bg-yellow-50 p-4 text-yellow-700">
          <div className="flex h-10 w-10 items-center justify-center rounded-full bg-yellow-100">
            <Clock className="h-5 w-5" />
          </div>
          <div>
            <p className="text-sm font-medium">Đang chờ</p>
            <p className="text-2xl font-bold">{pending.length}</p>
          </div>
        </div>
      </div>

      {/* Progress bar */}
      {queue.length > 0 && (
        <div className="mb-6">
          <div className="mb-1 flex items-center justify-between text-sm text-muted-foreground">
            <span>Tiến độ check-in</span>
            <span>
              {checkedIn.length} / {queue.length} bệnh nhân
            </span>
          </div>
          <div className="h-2 overflow-hidden rounded-full bg-muted">
            <div
              className="h-full rounded-full bg-green-500 transition-all duration-300"
              style={{ width: `${(checkedIn.length / queue.length) * 100}%` }}
            />
          </div>
        </div>
      )}

      {/* Queue Table */}
      <CheckinQueueTable
        queue={filteredQueue}
        isLoading={isLoading}
        onCheckin={handleCheckin}
        checkingInId={isCheckingIn ? "checking" : null}
      />
    </div>
  );
}
