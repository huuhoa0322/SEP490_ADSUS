"use client";

import { useState, useMemo, useCallback } from "react";
import {
  startOfDay,
  addDays,
  startOfWeek,
  endOfDay,
  isBefore,
  isAfter,
  parseISO,
  format,
} from "date-fns";
import { useOpenSlots } from "./use-booking";
import { MAX_BOOKING_DAYS, type OpenSlotResponse } from "../types/booking.types";
import type { CreateCaseSymptomInput } from "@/features/medical-record/types/medical-record.types";

export interface DoctorOption {
  id: string;
  name: string;
  status: string;
  gender: string | null;
}

export function useBookingForm() {
  // --- 1. Selection State ---
  const [selectedDoctorGender, setSelectedDoctorGender] = useState<string | null>(null);
  const [selectedDoctorId, setSelectedDoctorId] = useState<string | null>(null);
  const [selectedWeekIndex, setSelectedWeekIndex] = useState<number>(0);
  const [selectedDate, setSelectedDate] = useState<string | null>(null);
  const [selectedSlotId, setSelectedSlotId] = useState<string | null>(null);

  // --- 2. Additional Form Fields ---
  const [reason, setReason] = useState<string>("");
  const [symptoms, setSymptoms] = useState<CreateCaseSymptomInput[]>([]);
  const [isSymptomSectionExpanded, setIsSymptomSectionExpanded] = useState<boolean>(false);

  // --- 3. Relative Booking State ---
  const [isBookingForSelf, setIsBookingForSelf] = useState<boolean>(true);
  const [selectedRelativeId, setSelectedRelativeId] = useState<string | null>(null);

  // --- 4. Fetch Slots (30 ngày từ hôm nay) ---
  const today = useMemo(() => startOfDay(new Date()), []);
  const maxDate = useMemo(() => addDays(today, MAX_BOOKING_DAYS), [today]);

  const fromDate = useMemo(() => format(today, "yyyy-MM-dd"), [today]);
  const toDate = useMemo(() => format(maxDate, "yyyy-MM-dd"), [maxDate]);

  const { data: allSlots = [], isLoading: isLoadingSlots } = useOpenSlots({
    fromDate,
    toDate,
  });

  // --- 5. Derived: Doctor Options ---
  const doctorOptions = useMemo<DoctorOption[]>(() => {
    const map = new Map<string, DoctorOption>();
    for (const s of allSlots) {
      if (!s.doctorId) continue;
      // Chỉ lấy bác sĩ có trạng thái active (nếu có trường doctorStatus)
      if (s.doctorStatus && s.doctorStatus.toLowerCase() !== "active") continue;
      if (!map.has(s.doctorId)) {
        map.set(s.doctorId, {
          id: s.doctorId,
          name: s.doctorName,
          status: s.doctorStatus,
          gender: s.doctorGender,
        });
      }
    }
    return Array.from(map.values()).sort((a, b) => a.name.localeCompare(b.name, "vi"));
  }, [allSlots]);

  const filteredDoctorOptions = useMemo<DoctorOption[]>(() => {
    if (!selectedDoctorGender) return doctorOptions;
    return doctorOptions.filter(
      (d) => d.gender?.toUpperCase() === selectedDoctorGender.toUpperCase()
    );
  }, [doctorOptions, selectedDoctorGender]);

  const selectedDoctor = useMemo(() => {
    if (!selectedDoctorId) return undefined;
    return doctorOptions.find((d) => d.id === selectedDoctorId);
  }, [doctorOptions, selectedDoctorId]);

  // --- 6. Derived: Available Dates (có slot và trong 30 ngày từ hôm nay) ---
  const availableDates = useMemo<Date[]>(() => {
    const seen = new Set<string>();
    const dates: Date[] = [];

    for (const s of allSlots) {
      if (!s.slotDate) continue;
      if (seen.has(s.slotDate)) continue;
      const d = parseISO(s.slotDate);
      if (isBefore(d, today)) continue;
      if (isAfter(d, maxDate)) continue;
      seen.add(s.slotDate);
      dates.push(d);
    }

    dates.sort((a, b) => a.getTime() - b.getTime());
    return dates;
  }, [allSlots, today, maxDate]);

  // --- 7. Derived: Display Dates theo tuần đang chọn (0..3) ---
  const displayDates = useMemo<Date[]>(() => {
    const currentMonday = startOfWeek(today, { weekStartsOn: 1 });
    const weekMonday = addDays(currentMonday, selectedWeekIndex * 7);
    const weekSunday = addDays(weekMonday, 6);
    const weekSundayEnd = endOfDay(weekSunday);

    return availableDates.filter((d) => {
      return !isBefore(d, weekMonday) && !isAfter(d, weekSundayEnd);
    });
  }, [availableDates, today, selectedWeekIndex]);

  // --- 8. Derived: Visible Slots (lọc theo Bác sĩ + Ngày + kiểm tra giờ hôm nay) ---
  const visibleSlots = useMemo<OpenSlotResponse[]>(() => {
    if (!selectedDoctorId || !selectedDate) return [];

    const now = new Date();
    const todayStr = format(now, "yyyy-MM-dd");
    const currentTimeStr = format(now, "HH:mm:ss");

    return allSlots
      .filter((s) => {
        // Bác sĩ đã chọn
        if (s.doctorId !== selectedDoctorId) return false;
        // Ngày đã chọn
        if (s.slotDate !== selectedDate) return false;
        // Nếu là hôm nay, giờ bắt đầu phải chưa qua
        if (s.slotDate === todayStr) {
          if (s.startTime <= currentTimeStr) return false;
        }
        return true;
      })
      .sort((a, b) => a.startTime.localeCompare(b.startTime));
  }, [allSlots, selectedDoctorId, selectedDate]);

  const selectedSlot = useMemo(() => {
    if (!selectedSlotId) return undefined;
    return allSlots.find((s) => s.slotId === selectedSlotId);
  }, [allSlots, selectedSlotId]);

  // --- 9. Selection Handlers với CHUỖI RESET CHUẨN Mobile ---

  /**
   * Chọn giới tính bác sĩ (hoặc toggle bỏ chọn nếu click lại).
   * Reset CẢ 3: doctor, date, slot.
   */
  const selectDoctorGender = useCallback((gender: string | null) => {
    setSelectedDoctorGender((prev) => (prev === gender ? null : gender));
    setSelectedDoctorId(null);
    setSelectedDate(null);
    setSelectedSlotId(null);
  }, []);

  /**
   * Chọn bác sĩ.
   * Reset: slot (GIỮ NGUYÊN selectedDate).
   */
  const selectDoctor = useCallback((doctorId: string | null) => {
    setSelectedDoctorId(doctorId);
    setSelectedSlotId(null);
  }, []);

  /**
   * Chọn tuần (0..3).
   * Reset: date, slot.
   */
  const selectWeek = useCallback((weekIndex: number) => {
    setSelectedWeekIndex(weekIndex);
    setSelectedDate(null);
    setSelectedSlotId(null);
  }, []);

  /**
   * Chọn ngày khám.
   * Reset: slot.
   */
  const selectDate = useCallback((date: string | Date | null) => {
    const dateStr = date instanceof Date ? format(date, "yyyy-MM-dd") : date;
    setSelectedDate(dateStr);
    setSelectedSlotId(null);
  }, []);

  /**
   * Chọn khung giờ slot.
   */
  const selectSlot = useCallback((slotId: string | null) => {
    setSelectedSlotId(slotId);
  }, []);

  /**
   * Đổi đối tượng đặt lịch: Cho tôi / Cho người thân.
   */
  const handleSetIsBookingForSelf = useCallback((forSelf: boolean) => {
    setIsBookingForSelf(forSelf);
    if (forSelf) {
      setSelectedRelativeId(null);
    }
  }, []);

  /**
   * Reset toàn bộ form sau khi đặt lịch thành công hoặc mở mới.
   */
  const resetForm = useCallback(() => {
    setSelectedDoctorGender(null);
    setSelectedDoctorId(null);
    setSelectedWeekIndex(0);
    setSelectedDate(null);
    setSelectedSlotId(null);
    setReason("");
    setSymptoms([]);
    setIsSymptomSectionExpanded(false);
    setIsBookingForSelf(true);
    setSelectedRelativeId(null);
  }, []);

  return {
    // State
    selectedDoctorGender,
    selectedDoctorId,
    selectedWeekIndex,
    selectedDate,
    selectedSlotId,
    reason,
    symptoms,
    isSymptomSectionExpanded,
    isBookingForSelf,
    selectedRelativeId,
    isLoadingSlots,

    // Derived
    allSlots,
    doctorOptions,
    filteredDoctorOptions,
    selectedDoctor,
    availableDates,
    displayDates,
    visibleSlots,
    selectedSlot,

    // Handlers
    selectDoctorGender,
    selectDoctor,
    selectWeek,
    selectDate,
    selectSlot,
    setReason,
    setSymptoms,
    setIsSymptomSectionExpanded,
    setIsBookingForSelf: handleSetIsBookingForSelf,
    setSelectedRelativeId,
    resetForm,
  };
}
