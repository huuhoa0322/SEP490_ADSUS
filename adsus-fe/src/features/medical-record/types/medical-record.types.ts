/**
 * Types khớp DTO thật của backend Module 04 (`ADSUS_BE.BLL/MedicalRecord/DTOs/`).
 *
 * Chỗ nào lệch với `Documents/05_APIs/API_Spec/04_Module04_Medical_Record_API_Spec.md`
 * (v0.1 draft) thì CODE BACKEND THẮNG — spec đó đã lỗi thời ở 4 điểm, xem spec Frontend §3.
 */

/**
 * PagedResult của `ADSUS_BE.BLL.Common` — 5 trường.
 *
 * KHÁC bản trong `features/users/types/user.types.ts`, vốn dùng `totalCount` vì Module 2 có
 * PagedResult riêng. Đừng dùng lẫn hai kiểu này.
 */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export type Gender = "FEMALE" | "MALE" | "OTHER";
export type CaseStatus = "CREATED" | "END" | "CONFIRMED";

/** Bộ lọc của #26. "Pending" gộp cả CREATED lẫn ANALYZED. */
export type VisitStatusFilter = "All" | "Pending" | "Confirmed";

/** Một dòng danh sách bệnh nhân (#26, SCR-09). */
export interface PatientSummary {
  /** NULL = tài khoản đã tồn tại nhưng CHƯA có hồ sơ nền → nút hành động đổi thành "Tạo hồ sơ nền". */
  patientProfileId: string | null;
  patientUserId: string;
  fullName: string;
  phone: string;
  /** DateOnly của .NET serialize thành "2026-07-22". */
  latestVisitDate: string | null;
  latestVisitStatus: CaseStatus | null;
}

/** Hồ sơ y tế nền (#17 #18 #19, SCR-10). */
export interface PatientProfile {
  patientProfileId: string;
  patientUserId: string;
  /** Chỉ đọc — lấy từ bảng users (UC-06 bước 2). #18 không nhận trường này. */
  fullName: string;
  /** Chỉ đọc — xem fullName. */
  phone: string;
  /** Chỉ đọc — xem fullName. */
  dateOfBirth: string | null;
  gender: Gender;
  diseases: PatientDiseaseResponse[];
  allergies: PatientAllergyResponse[];
  createdBy: string;
  createdAt: string;
  updatedAt: string;
}

/** #17. gender bỏ trống được (DB có default), khác #18. */
export interface CreatePatientProfileRequest {
  patientUserId: string;
  gender: Gender | null;
  diseases: PatientDiseaseInput[];
  allergies: PatientAllergyInput[];
}

/** #18 — thay TOÀN BỘ hồ sơ, nên gender bắt buộc. patientUserId không sửa được. */
export interface UpdatePatientProfileRequest {
  gender: Gender;
  diseases: PatientDiseaseInput[];
  allergies: PatientAllergyInput[];
}

export interface PatientDiseaseInput {
  diseaseId: string;
  note: string | null;
}

export interface PatientAllergyInput {
  allergyTypeId: string;
  note: string | null;
}

export interface PatientDiseaseResponse {
  diseaseId: string;
  diseaseName: string;
  isOther: boolean;
  note: string | null;
}

export interface PatientAllergyResponse {
  allergyTypeId: string;
  allergyName: string;
  isOther: boolean;
  note: string | null;
}

export interface MedicalDisease {
  id: string;
  name: string;
  requiresNote: boolean;
  isOther: boolean;
}

export interface MedicalAllergyType {
  id: string;
  name: string;
  isOther: boolean;
}

/** Tài khoản bệnh nhân (BE-4). CÓ dateOfBirth — khác hẳn UserAccountResponse của Module 2. */
export interface PatientAccount {
  userId: string;
  fullName: string;
  phoneNumber: string;
  dateOfBirth: string | null;
  email: string | null;
}

/**
 * Response của #28 POST /patients — DUY NHẤT chỗ có `temporaryPassword`, và chỉ đúng một lần
 * ngay lúc tạo (quyết định ghi đè 06/08/2026, thay một phần BR-05 gốc). Không endpoint nào
 * khác của Module 04 trả trường này.
 */
export interface PatientAccountCreated extends PatientAccount {
  temporaryPassword: string;
}

/** UC-06 AF-01 — chỉ Điều dưỡng. Không có role (luôn PATIENT), không có mật khẩu. */
export interface CreatePatientAccountRequest {
  phoneNumber: string;
  fullName: string;
  dateOfBirth: string | null;
  email: string | null;
}

/** UC-06 AF-02 — đúng 4 trường liên hệ (BR-04). Không role, không status. */
export interface UpdatePatientAccountRequest {
  fullName: string;
  phoneNumber: string;
  dateOfBirth: string | null;
  email: string | null;
}

/** BE-3 — cố ý chỉ có id và họ tên. */
export interface DoctorSummary {
  userId: string;
  fullName: string;
}

/** #21 #22 và nhúng trong #23. */
export interface UltrasoundImage {
  imageId: string;
  caseId: string;
  /** NULL khi Storage ký URL thất bại — gallery phải có ô hỏng, đừng để <img src={null}>. */
  imageUrl: string | null;
  uploadedAt: string;
  note: string | null;
}

/**
 * Nhúng trong #23. Module 04 KHÔNG render hai khối này (quyết định D5) — Module 05 và 07
 * chưa có backend, badge bấm không được cũng là UI chết. Khai type để khớp payload thật và
 * để hai module đó gắn khối của mình vào sau mà không phải sửa lại kiểu.
 */
export interface AiResultSummary {
  aiResultId: string;
  status: string;
  findingCount: number;
}

/** Xem AiResultSummary. */
export interface PrescriptionSummary {
  prescriptionId: string;
  status: string;
}

/**
 * #20 (kết quả tạo) và #23 — bản đầy đủ cho Bác sĩ/Điều dưỡng.
 *
 * Web chỉ có Doctor/Nurse nên chỉ khai kiểu này. Backend còn một kiểu `PatientCaseResponse`
 * rút gọn cho Bệnh nhân, nhưng đó là của ứng dụng Flutter (SCR-13/14) — không thiếu type,
 * là cố ý không khai.
 */
export interface CaseDetail {
  caseId: string;
  patientProfileId: string;
  doctorId: string;
  doctorName: string;
  visitDate: string;
  clinicalInfo: string | null;
  status: CaseStatus;
  /** Spec API v0.1 gộp thành một trường `conclusion`; code thật tách đôi. */
  finalDiagnosis: string | null;
  doctorConclusion: string | null;
  patientProfile: PatientProfile | null;
  ultrasoundImages: UltrasoundImage[];
  symptoms: CaseSymptomDetail[];
  aiResults: AiResultSummary[];
  prescription: PrescriptionSummary | null;
  createdAt: string;
  updatedAt: string;
}

export interface SymptomItem {
  symptomId: string;
  name: string;
  isOther: boolean;
}

export interface SymptomCategory {
  categoryId: string;
  name: string;
  isOther: boolean;
  symptoms: SymptomItem[];
}

export interface CaseSymptomDetail {
  categoryId: string;
  categoryName: string;
  symptomId: string | null;
  symptomName: string | null;
  otherNote: string | null;
}

export interface CreateCaseSymptomInput {
  categoryId: string;
  symptomId: string | null;
  otherNote: string | null;
}

/**
 * #24 — một dòng lịch sử khám (SCR-12), CHỈ dành cho Bác sĩ/Điều dưỡng (Web).
 *
 * createdAt thêm 06/08/2026 — visitDate là ngày thuần (DateOnly, không giờ), nên cần trường
 * riêng để hiện giờ tạo ca. KHÔNG dùng lại type này cho #25 (danh sách của chính bệnh nhân,
 * chỉ tồn tại ở Mobile) — backend cố tình tách hai response khác nhau ở đây.
 */
export interface CaseSummary {
  caseId: string;
  visitDate: string;
  status: CaseStatus;
  doctorId: string;
  createdAt: string;
}

export interface PatientListQuery {
  search?: string;
  visitStatus?: VisitStatusFilter;
  /** undefined = tất cả; false = chỉ tài khoản chưa có hồ sơ nền (dùng cho luồng tạo #17). */
  hasProfile?: boolean;
  page?: number;
  pageSize?: number;
}

export interface CaseListQuery {
  patientProfileId: string;
  status?: CaseStatus;
  sortOrder?: "asc" | "desc";
  page?: number;
  pageSize?: number;
}

export interface CreateCaseInput {
  patientProfileId: string;
  responsibleDoctorId: string;
  clinicalInfo: string | null;
  symptoms: CreateCaseSymptomInput[];
  images: File[];
}

/**
 * Thêm 07/08/2026, sửa lại cùng ngày (tách Lưu/Kết thúc) — Bác sĩ phụ trách nhập/sửa kết luận
 * ngay tại màn chi tiết ca. Cả hai trường bắt buộc (validator backend), không giống
 * ClinicalInfo (#20) vốn tùy chọn. Dùng chung cho cả "Lưu kết luận" (không đổi trạng thái) và
 * "Kết thúc ca khám" (khoá ca) — hai hành động khác nhau, cùng hình dạng dữ liệu gửi lên.
 */
export interface CaseConclusionInput {
  finalDiagnosis: string;
  doctorConclusion: string;
}
