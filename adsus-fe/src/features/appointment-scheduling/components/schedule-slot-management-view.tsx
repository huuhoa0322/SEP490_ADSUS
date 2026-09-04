"use client";

import { useState } from "react";
import { Plus, Coffee } from "lucide-react";
import { format, addMonths } from "date-fns";
import { Button } from "@/components/ui/button";

import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";

import { useMonthSummary } from "../hooks/use-shift-request";
import { MonthCalendar } from "./month-calendar";
import { DayShiftDetail } from "./day-shift-detail";
import { ShiftRequestForm } from "./shift-request-form";
import { DoctorShiftRequestsList } from "./doctor-shift-requests-list";
import { DayShiftSummary } from "../types/shift-request.types";

/**
 * Lịch quản lý của Doctor (Month View)
 */
export function ScheduleSlotManagementView() {
  const [currentDate, setCurrentDate] = useState(new Date());
  const [selectedDay, setSelectedDay] = useState<{ summary?: DayShiftSummary; date: Date } | null>(null);
  const [isRequestFormOpen, setIsRequestFormOpen] = useState(false);
  const [requestFormType, setRequestFormType] = useState<'LEAVE' | 'OVERTIME'>('LEAVE');
  const [requestFormDate, setRequestFormDate] = useState<Date | undefined>(undefined);

  const year = currentDate.getFullYear();
  const month = currentDate.getMonth() + 1; // 1-12

  const { data: summaries, isLoading } = useMonthSummary(year, month);

  const handlePrevMonth = () => setCurrentDate(addMonths(currentDate, -1));
  const handleNextMonth = () => setCurrentDate(addMonths(currentDate, 1));

  return (
    <div className="space-y-6">
      <header className="flex items-center justify-between">
        <div>
          <h1 className="font-heading text-2xl font-semibold">Lịch khám của tôi</h1>
          <p className="text-sm text-slate-500">
            Quản lý ca làm việc, đăng ký nghỉ phép và tăng ca.
          </p>
        </div>
        <div className="flex gap-3">
          <Button
            variant="outline"
            className="gap-2"
            onClick={() => {
              setRequestFormType('LEAVE');
              setRequestFormDate(undefined);
              setIsRequestFormOpen(true);
            }}
          >
            <Coffee className="h-4 w-4" />
            Xin nghỉ phép
          </Button>
          <Button
            className="gap-2"
            onClick={() => {
              setRequestFormType('OVERTIME');
              setRequestFormDate(undefined);
              setIsRequestFormOpen(true);
            }}
          >
            <Plus className="h-4 w-4" />
            Đăng ký tăng ca
          </Button>
        </div>
      </header>

      <Tabs defaultValue="calendar" className="w-full">
        <TabsList className="mb-4">
          <TabsTrigger value="calendar">Lịch làm việc</TabsTrigger>
          <TabsTrigger value="requests">Lịch sử xin phép / Tăng ca</TabsTrigger>
        </TabsList>

        <TabsContent value="calendar" className="space-y-4">
          {isLoading ? (
            <div className="flex justify-center py-20 text-slate-500">
              Đang tải dữ liệu lịch...
            </div>
          ) : (
            <MonthCalendar
              currentDate={currentDate}
              onPrevMonth={handlePrevMonth}
              onNextMonth={handleNextMonth}
              summaries={summaries ?? []}
              onDayClick={(summary, date) => setSelectedDay({ summary, date })}
            />
          )}
        </TabsContent>

        <TabsContent value="requests">
          <DoctorShiftRequestsList />
        </TabsContent>
      </Tabs>

      {/* Modal chi tiết ca làm việc của 1 ngày */}
      <DayShiftDetail
        open={selectedDay !== null}
        onOpenChange={(open) => !open && setSelectedDay(null)}
        date={selectedDay?.date ?? null}
        summary={selectedDay?.summary}
        onRequestClick={(type, date) => {
          setRequestFormType(type);
          setRequestFormDate(date);
          setSelectedDay(null); // Close the detail popup
          setIsRequestFormOpen(true);
        }}
      />

      {/* Modal đăng ký nghỉ/tăng ca */}
      <ShiftRequestForm
        open={isRequestFormOpen}
        onOpenChange={setIsRequestFormOpen}
        defaultRequestType={requestFormType}
        defaultDate={requestFormDate}
      />
    </div>
  );
}
