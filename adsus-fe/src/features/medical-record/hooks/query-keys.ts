import type { CaseListQuery, PatientListQuery } from "../types/medical-record.types";

/**
 * Khoá cache gom về một chỗ, theo đúng tiền lệ `usersQueryKeys` ở `features/users`.
 *
 * Mọi khoá đều bắt đầu bằng `all`, nên một lệnh invalidate ở gốc là làm mới được tất cả —
 * cần thiết vì các màn Module 04 phụ thuộc chéo nhau (tạo tài khoản đổi danh sách bệnh nhân;
 * tạo ca khám đổi cả danh sách lần khám lẫn trạng thái lần khám gần nhất trên SCR-09).
 */
export const medicalRecordQueryKeys = {
  all: ["medical-record"] as const,

  patients: (query: PatientListQuery) => [...medicalRecordQueryKeys.all, "patients", query] as const,
  doctors: () => [...medicalRecordQueryKeys.all, "doctors"] as const,

  profile: (profileId: string) => [...medicalRecordQueryKeys.all, "profile", profileId] as const,
  account: (userId: string) => [...medicalRecordQueryKeys.all, "account", userId] as const,

  cases: (query: CaseListQuery) => [...medicalRecordQueryKeys.all, "cases", query] as const,
  case: (caseId: string) => [...medicalRecordQueryKeys.all, "case", caseId] as const,
  images: (caseId: string) => [...medicalRecordQueryKeys.all, "images", caseId] as const,
  symptoms: () => [...medicalRecordQueryKeys.all, "symptoms"] as const,
};
