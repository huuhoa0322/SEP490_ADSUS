import type { CaseStatus, Gender, VisitStatusFilter, PatientDiseaseResponse, PatientAllergyResponse } from "../types/medical-record.types";

const CASE_STATUS_LABELS: Record<CaseStatus, string> = {
  CREATED: "Mới tạo",
  END: "Đã kết thúc ca",
  CONFIRMED: "Đã kết luận",
};

const GENDER_LABELS: Record<Gender, string> = {
  FEMALE: "Nữ",
  MALE: "Nam",
  OTHER: "Khác",
};

const VISIT_STATUS_LABELS: Record<VisitStatusFilter, string> = {
  All: "Tất cả trạng thái",
  Pending: "Chờ kết luận",
  Confirmed: "Đã kết luận",
};

export function caseStatusLabel(status: CaseStatus): string {
  return CASE_STATUS_LABELS[status];
}

export function genderLabel(gender: Gender): string {
  return GENDER_LABELS[gender];
}

export function visitStatusLabel(filter: VisitStatusFilter): string {
  return VISIT_STATUS_LABELS[filter];
}

/** Placeholder dùng chung cho mọi ô không có dữ liệu — chuỗi rỗng trông như lỗi render. */
export const EMPTY_VALUE = "—";

/**
 * Đổi `DateOnly` của .NET ("2026-07-22") sang dd/MM/yyyy.
 *
 * Cắt chuỗi thay vì đi qua `new Date()`: chuỗi chỉ-ngày được JS hiểu là UTC midnight, nên
 * máy ở múi giờ âm sẽ hiển thị lùi một ngày. Ngày khám lệch một ngày trong hồ sơ y tế là
 * lỗi nghiêm trọng, không phải chi tiết hiển thị.
 */
export function formatIsoDate(value: string | null | undefined): string {
  if (!value) return EMPTY_VALUE;

  const [year, month, day] = value.slice(0, 10).split("-");
  if (!year || !month || !day) return EMPTY_VALUE;

  return `${day}/${month}/${year}`;
}

/** Đổi `DateTime` ISO sang dd/MM/yyyy HH:mm theo giờ máy người dùng. */
export function formatIsoDateTime(value: string | null | undefined): string {
  if (!value) return EMPTY_VALUE;

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return EMPTY_VALUE;

  const pad = (n: number) => String(n).padStart(2, "0");

  return `${pad(date.getDate())}/${pad(date.getMonth() + 1)}/${date.getFullYear()} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

export function formatDiseases(diseases: PatientDiseaseResponse[] | undefined | null): string {
  if (!diseases || diseases.length === 0) return EMPTY_VALUE;
  return diseases.map(d => {
    if (d.isOther) return d.note || d.diseaseName;
    return d.note ? `${d.diseaseName} (${d.note})` : d.diseaseName;
  }).join(", ");
}

export function formatAllergies(allergies: PatientAllergyResponse[] | undefined | null): string {
  if (!allergies || allergies.length === 0) return EMPTY_VALUE;
  return allergies.map(a => {
    if (a.isOther) return a.note || a.allergyName;
    return a.note ? `${a.allergyName} (${a.note})` : a.allergyName;
  }).join(", ");
}

