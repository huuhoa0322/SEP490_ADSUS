/**
 * Types cho người thân bệnh nhân (Patient Relationship / Relatives).
 * Đồng bộ với Backend API `/api/v1/relatives` và Mobile.
 */

export interface RelativeResponse {
  relationshipId: string;
  patientProfileId: string;
  patientName: string;
  patientPhone: string | null;
  dateOfBirth: string | null; // "YYYY-MM-DD"
  gender: string | null;
  relationshipName: string | null;
  isRegisteredAccount: boolean;
  createdAt: string;
}

export interface RelativesListResponse {
  relatives: RelativeResponse[];
}

export interface AddRelativeRequest {
  fullName: string;
  phone?: string | null;
  dateOfBirth?: string | null; // "YYYY-MM-DD"
  relationshipName?: string | null;
}

export interface CheckPhoneResponse {
  phone: string;
  isRegistered: boolean;
}
