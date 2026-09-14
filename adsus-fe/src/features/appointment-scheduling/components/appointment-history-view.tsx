"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { CalendarX, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
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

export function AppointmentHistoryView() {
  const router = useRouter();
  const [activeTab, setActiveTab] = useState<"SELF" | "RELATIVE">("SELF");
  const [detailId, setDetailId] = useState<string | null>(null);
  const [cancelTarget, setCancelTarget] = useState<string | null>(null);

  const { data, isLoading, error, refetch } = useAppointmentHistory();
  const cancelMutation = useCancelMyAppointment();

  const allAppointments: AppointmentSummaryResponse[] = data ?? [];
  const selfList = allAppointments.filter((a) => !a.isBookedForOthers);
  const relativeList = allAppointments.filter((a) => !!a.isBookedForOthers);

  const currentList = activeTab === "SELF" ? selfList : relativeList;
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
    setDetailId(null);
    void cancelMutation.mutate(
      { appointmentId, reason: "Đặt lại lịch" },
      {
        onSuccess: () => {
          void router.push("/dat-lich");
        },
      }
    );
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
        <TabsList className="mb-4 grid w-full grid-cols-2">
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
            <div className="flex flex-col gap-3">
              {selfList.map((appt) => (
                <AppointmentHistoryCard
                  key={appt.appointmentId}
                  appointment={appt}
                  onClick={handleCardClick}
                />
              ))}
            </div>
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
            <div className="flex flex-col gap-3">
              {relativeList.map((appt) => (
                <AppointmentHistoryCard
                  key={appt.appointmentId}
                  appointment={appt}
                  onClick={handleCardClick}
                />
              ))}
            </div>
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
