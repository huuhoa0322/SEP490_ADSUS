/**
 * UC-05 FT-10 — số liệu màn thống kê (SCR-08).
 *
 * BR-01: mọi thứ ở đây là số đếm và tỉ lệ đã tổng hợp. Không có trường nào chứa tên, số
 * điện thoại hay thông tin nhận dạng bệnh nhân — và cũng không được thêm vào.
 */
export interface DashboardStatistics {
  fromDate: string;
  toDate: string;
  accounts: AccountStatistics;
  clinical: ClinicalStatistics;
  appointments: AppointmentStatistics;
  adherence: AdherenceStatistics;
  activeAiModel: AiModelMetrics;
  /** Luôn đủ mọi ngày trong khoảng, ngày không phát sinh có giá trị 0. */
  trend: DailyPoint[];
}

export interface AiModelMetrics {
  versionCode: string;
  precision?: number;
  recall?: number;
  map50?: number;
  lastEvaluatedAt?: string;
}

/** Một điểm trên biểu đồ xu hướng (UC-05 bước 3). */
export interface DailyPoint {
  date: string;
  newAccounts: number;
  cases: number;
  appointments: number;
}

export interface AccountStatistics {
  total: number;
  adminCount: number;
  doctorCount: number;
  nurseCount: number;
  patientCount: number;
  activeCount: number;
  lockedCount: number;
  deactivatedCount: number;
  newInRange: number;
  activeRate: number;
}

export interface ClinicalStatistics {
  caseCount: number;
  aiRunCount: number;
  aiConfirmedCount: number;
  aiRejectedCount: number;
  aiPendingCount: number;
  /** Tính trên số kết quả ĐÃ duyệt, không tính phần đang chờ vào mẫu số. */
  aiConfirmRate: number;
}

export interface AppointmentStatistics {
  bookedCount: number;
  cancelledCount: number;
  slotCount: number;
  cancellationRate: number;
  /*
   * ĐÃ BỎ averageBookingsPerSlot — xem chú thích ở AppointmentStatistics phía backend.
   * Tóm tắt: ScheduleSlot không có Capacity nên không tính được tỉ lệ lấp đầy, mà số trung
   * bình thì dễ bị đọc nhầm thành tỉ lệ đó.
   */
}

export interface AdherenceStatistics {
  scheduledDoseCount: number;
  takenDoseCount: number;
  adherenceRate: number;
}

export interface DashboardQuery {
  fromDate?: string;
  toDate?: string;
}
