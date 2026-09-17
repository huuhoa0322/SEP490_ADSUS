"use client";

import { useState, useMemo } from "react";
import { useRouter } from "next/navigation";
import { CalendarX, ChevronLeft, ChevronRight, RefreshCw } from "lucide-react";
import toast from "react-hot-toast";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from "@/components/ui/tabs";
import { Skeleton } from "@/components/ui/skeleton";
import { AppointmentHistoryCard } from "./appointment-history-card";
import { AppointmentHistoryDetailDialog } from "./appointment-history-detail-dialog";
import { CancelAppointmentDialog } from "./cancel-appointment-dialog";
import { useAppointmentHistory, useCancelMyAppointment } from "../hooks/use-appointment-history";
import type { AppointmentSummaryResponse } from "../types/booking.types";

const PAGE_SIZE = 10;

export function AppointmentHistoryView() {
  const router = useRouter();
  const [activeTab, setActiveTab] = useState<"SELF" | "RELATIVE">("SELF");
  const [selfPage, setSelfPage] = useState(1);
  const [relativePage, setRelativePage] = useState(1);
  const [detailId, setDetailId] = useState<string | null>(null);
  const [cancelTarget, setCancelTarget] = useState<string | null>(null);
  const [rescheduleConfirmTarget, setRescheduleConfirmTarget] = useState<string | null>(null);

  const { data, isLoading, error, refetch } = useAppointmentHistory();
  const cancelMutation = useCancelMyAppointment();

  const allAppointments: AppointmentSummaryResponse[] = data ?? [];
  const selfList = allAppointments.filter((a) => !a.isBookedForOthers);
  const relativeList = allAppointments.filter((a) => !!a.isBookedForOthers);

  // Pagination: slice theo PAGE_SIZE cho mỗi tab, độc lập state.
  const selfTotalPages = Math.max(1, Math.ceil(selfList.length / PAGE_SIZE));
  const relativeTotalPages = Math.max(1, Math.ceil(relativeList.length / PAGE_SIZE));
  const currentPage = activeTab === "SELF" ? selfPage : relativePage;
  const currentList = useMemo(() => {
    const fullList = activeTab === "SELF" ? selfList : relativeList;
    const start = (currentPage - 1) * PAGE_SIZE;
    return fullList.slice(start, start + PAGE_SIZE);
  }, [activeTab, selfList, relativeList, currentPage]);

  const selectedAppointment = detailId
    ? allAppointments.find((a) => a.appointmentId === detailId) ?? null
    : null;

  function handleCardClick(appointment: AppointmentSummaryResponse) {
    setDetailId(appointment.appointmentId);
  }

  function handleCancelRequest(appointmentId: string) {
    setCancelTarget(appointmentId);
  }

  function handleRescheduleRequest(appointmentId: string) {
    setRescheduleConfirmTarget(appointmentId);
  }

  function _handleRescheduleConfirm() {
    const apptId = rescheduleConfirmTarget;
    if (!apptId) return;
    void cancelMutation.mutate(
      { appointmentId: apptId, reason: "Đặt lại lịch" },
      {
        onSuccess: () => {
          toast.success(
            "Đã hủy lịch cũ thành công, bạn được chuyển đến màn Đặt lịch để thực hiện đặt lịch hẹn mới"
          );
          void router.push("/dat-lich");
        },
      }
    );
    setRescheduleConfirmTarget(null);
  }

  function _handleRescheduleCancel() {
    setRescheduleConfirmTarget(null);
  }

  function handleCancelClose() {
    setCancelTarget(null);
  }

  function handleDetailClose() {
    setDetailId(null);
  }

  function navigateToBooking() {
    void router.push("/dat-lich");
  }

  function handlePrevPage() {
    if (activeTab === "SELF") {
      setSelfPage((p) => Math.max(1, p - 1));
    } else {
      setRelativePage((p) => Math.max(1, p - 1));
    }
  }

  function handleNextPage() {
    if (activeTab === "SELF") {
      setSelfPage((p) => Math.min(selfTotalPages, p + 1));
    } else {
      setRelativePage((p) => Math.min(relativeTotalPages, p + 1));
    }
  }

  return (
    <div className="mx-auto w-full max-w-screen-md px-4 py-8">
      <h1
        className="mb-6 text-2xl font-bold"
        style={{ color: "var(--ink-navy)" }}
      >
        Lịch hẹn của tôi
      </h1>

      <Tabs
        value={activeTab}
        onValueChange={(v) => setActiveTab(v as "SELF" | "RELATIVE")}
      >
        <TabsList className="mb-4 grid w-full grid-cols-2 rounded-xl p-1 [&>[data-state=active]]:bg-white [&>[data-state=active]]:text-[var(--lp-teal)] [&>[data-state=active]]:shadow-sm">
          <TabsTrigger value="SELF">
            Lịch của tôi ({selfList.length})
          </TabsTrigger>
          <TabsTrigger value="RELATIVE">
            Lịch người thân ({relativeList.length})
          </TabsTrigger>
        </TabsList>

        <TabsContent value="SELF" className="mt-0">
          {isLoading ? (
            <div className="flex flex-col gap-3">
              {[1, 2, 3].map((i) => (
                <div
                  key={i}
                  className="flex gap-3"
                  data-testid="appointment-history-card-skeleton"
                >
                  <Skeleton className="h-24 w-full rounded-xl" />
                </div>
              ))}
            </div>
          ) : error ? (
            <ErrorState onRetry={refetch} />
          ) : selfList.length === 0 ? (
            <EmptyState onNavigate={navigateToBooking} dataTestId="self-empty-state" />
          ) : (
            <>
              <div className="flex flex-col gap-3">
                {currentList.map((appt) => (
                  <AppointmentHistoryCard
                    key={appt.appointmentId}
                    appointment={appt}
                    onClick={handleCardClick}
                  />
                ))}
              </div>
              <Pagination
                currentPage={selfPage}
                totalPages={selfTotalPages}
                onPrev={handlePrevPage}
                onNext={handleNextPage}
              />
            </>
          )}
        </TabsContent>

        <TabsContent value="RELATIVE" className="mt-0">
          {isLoading ? (
            <div className="flex flex-col gap-3">
              {[1, 2, 3].map((i) => (
                <div
                  key={i}
                  className="flex gap-3"
                  data-testid="appointment-history-card-skeleton"
                >
                  <Skeleton className="h-24 w-full rounded-xl" />
                </div>
              ))}
            </div>
          ) : error ? (
            <ErrorState onRetry={refetch} />
          ) : relativeList.length === 0 ? (
            <EmptyState onNavigate={navigateToBooking} dataTestId="relative-empty-state" />
          ) : (
            <>
              <div className="flex flex-col gap-3">
                {currentList.map((appt) => (
                  <AppointmentHistoryCard
                    key={appt.appointmentId}
                    appointment={appt}
                    onClick={handleCardClick}
                  />
                ))}
              </div>
              <Pagination
                currentPage={relativePage}
                totalPages={relativeTotalPages}
                onPrev={handlePrevPage}
                onNext={handleNextPage}
              />
            </>
          )}
        </TabsContent>
      </Tabs>

      {/* Detail Dialog */}
      <AppointmentHistoryDetailDialog
        appointment={selectedAppointment}
        onClose={handleDetailClose}
        onCancel={handleCancelRequest}
        onReschedule={handleRescheduleRequest}
      />

      {/* Cancel Dialog */}
      <CancelAppointmentDialog
        appointmentId={cancelTarget}
        onClose={handleCancelClose}
      />

      {/* Reschedule Confirm Dialog */}
      <Dialog open={rescheduleConfirmTarget !== null} onOpenChange={(open) => { if (!open) _handleRescheduleCancel(); }}>
        <DialogContent className="max-w-sm">
          <DialogHeader>
            <DialogTitle>Đặt lịch mới</DialogTitle>
          </DialogHeader>
          <p className="text-sm text-muted-foreground">
            Bạn có muốn đặt lịch mới không?<br />
            Lịch khám hiện tại sẽ bị hủy.
          </p>
          <DialogFooter className="gap-2 sm:gap-2 sm:justify-between">
            <Button
              type="button"
              variant="outline"
              className="flex-1"
              onClick={_handleRescheduleCancel}
            >
              Hủy bỏ
            </Button>
            <Button
              type="button"
              className="flex-1"
              style={{ backgroundColor: "#128C82", color: "#fff" }}
              onClick={_handleRescheduleConfirm}
            >
              Xác nhận
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function EmptyState({
  onNavigate,
  dataTestId,
}: {
  onNavigate: () => void;
  dataTestId?: string;
}) {
  return (
    <div
      className="flex flex-col items-center justify-center py-16 text-center"
      data-testid={dataTestId}
    >
      <CalendarX className="mb-4 size-12 text-muted-foreground" />
      <p className="mb-4 text-sm text-muted-foreground">
        Bạn chưa có lịch hẹn nào
      </p>
      <Button
        onClick={onNavigate}
        style={{ backgroundColor: "var(--teal-primary)", color: "#fff" }}
      >
        Đặt lịch ngay
      </Button>
    </div>
  );
}

function ErrorState({ onRetry }: { onRetry: () => void }) {
  return (
    <div className="flex flex-col items-center justify-center py-16 text-center">
      <p className="mb-4 text-sm text-destructive">Đã xảy ra lỗi khi tải dữ liệu.</p>
      <Button
        variant="outline"
        onClick={() => void onRetry()}
        className="gap-2"
      >
        <RefreshCw className="size-4" />
        Thử lại
      </Button>
    </div>
  );
}

function Pagination({
  currentPage,
  totalPages,
  onPrev,
  onNext,
}: {
  currentPage: number;
  totalPages: number;
  onPrev: () => void;
  onNext: () => void;
}) {
  if (totalPages <= 1) return null;
  return (
    <div
      className="mt-4 flex items-center justify-center gap-3"
      data-testid="pagination"
    >
      <Button
        type="button"
        variant="outline"
        size="sm"
        onClick={onPrev}
        disabled={currentPage <= 1}
        className="gap-1"
        aria-label="Trang trước"
      >
        <ChevronLeft className="size-4" />
        Trang trước
      </Button>
      <span
        className="text-sm font-medium tabular-nums"
        style={{ color: "var(--ink-navy)" }}
        data-testid="pagination-label"
      >
        Trang {currentPage} / {totalPages}
      </span>
      <Button
        type="button"
        variant="outline"
        size="sm"
        onClick={onNext}
        disabled={currentPage >= totalPages}
        className="gap-1"
        aria-label="Trang sau"
      >
        Trang sau
        <ChevronRight className="size-4" />
      </Button>
    </div>
  );
}
