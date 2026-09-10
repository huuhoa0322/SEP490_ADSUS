/**
 * Feedback types for Admin Feedback Management (R3)
 */

export interface AdminFeedbackItem {
  id: string;
  rating: number; // 1 to 5
  content: string | null;
  submittedAt: string; // ISO DateTime
  caseId: string;
  doctorName: string;
  doctorId: string;
  patientProfileId: string;
  patientName: string;
  patientPhone: string | null;
}

export interface FeedbackListQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  minRating?: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  totalCount?: number;
}
