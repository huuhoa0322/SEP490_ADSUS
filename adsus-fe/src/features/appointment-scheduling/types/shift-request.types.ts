

export type ShiftRequestType = 'LEAVE' | 'OVERTIME';
export type ShiftRequestStatus = 'PENDING' | 'APPROVED' | 'REJECTED';
export type ShiftType = 'MORNING' | 'AFTERNOON' | 'EVENING' | 'FULL_DAY';

export interface CreateShiftRequestDto {
  requestType: ShiftRequestType;
  requestDate: string; // YYYY-MM-DD
  shiftType: ShiftType;
  reason: string;
}

export interface ReviewShiftRequestDto {
  decision: 'APPROVED' | 'REJECTED';
  rejectReason?: string;
}

export interface ShiftRequestResponse {
  requestId: string;
  userId: string;
  doctorName: string;
  requestType: ShiftRequestType;
  requestDate: string;
  shiftType: ShiftType;
  shiftLabel: string;
  reason: string;
  status: ShiftRequestStatus;
  reviewedByName?: string;
  reviewedAt?: string;
  rejectReason?: string;
  createdAt: string;
}

export interface ShiftInfo {
  status: 'WORKING' | 'OFF' | 'HAS_BOOKINGS' | 'PAST';
  totalSlots: number;
  bookedSlots: number;
  closedSlots: number;
  pendingRequestType?: ShiftRequestType;
}

export interface DayShiftSummary {
  date: string;
  morning: ShiftInfo;
  afternoon: ShiftInfo;
  evening?: ShiftInfo;
}
