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
  averageBookingsPerSlot: number;
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
