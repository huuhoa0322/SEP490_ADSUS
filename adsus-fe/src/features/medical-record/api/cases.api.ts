import { AxiosError } from "axios";

import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";

import type {
  CaseConclusionInput,
  CaseDetail,
  CaseListQuery,
  CaseSummary,
  CreateCaseInput,
  PagedResult,
  UltrasoundImage,
} from "../types/medical-record.types";

const BASE = "/api/v1/cases";

/** #24 — lịch sử khám của một bệnh nhân (SCR-12). patientProfileId là bắt buộc. */
export async function listCasesByPatient(query: CaseListQuery): Promise<PagedResult<CaseSummary>> {
  const { data } = await apiClient.get<ApiResponse<PagedResult<CaseSummary>>>(BASE, {
    params: {
      patientProfileId: query.patientProfileId,
      status: query.status || undefined,
      sortOrder: query.sortOrder ?? "desc",
      page: query.page ?? 1,
      pageSize: query.pageSize ?? 20,
    },
  });

  if (!data.data) throw new Error(data.message || "Không tải được danh sách lần khám.");

  return data.data;
}

/**
 * #23 — chi tiết một ca khám.
 *
 * Backend trả hai hình dạng khác nhau theo vai trò; ở đây luôn là bản đầy đủ vì Web chỉ có
 * Doctor/Nurse. Bản rút gọn cho Bệnh nhân là của ứng dụng Flutter (SCR-14).
 */
export async function getCaseDetail(caseId: string): Promise<CaseDetail> {
  const { data } = await apiClient.get<ApiResponse<CaseDetail>>(`${BASE}/${caseId}`);

  if (!data.data) throw new Error(data.message || "Không tìm thấy ca khám.");

  return data.data;
}

/** #22 — không phân trang: số ảnh trên một lần khám là tập nhỏ có biên. */
export async function listUltrasoundImages(caseId: string): Promise<UltrasoundImage[]> {
  const { data } = await apiClient.get<ApiResponse<UltrasoundImage[]>>(
    `${BASE}/${caseId}/ultrasound-images`,
  );

  if (!data.data) throw new Error(data.message || "Không tải được ảnh siêu âm.");

  return data.data;
}

/**
 * #20 — tạo ca khám kèm ảnh, trong MỘT request multipart.
 *
 * Không tự đặt header Content-Type: axios cần tự sinh boundary cho FormData, đặt tay sẽ
 * làm hỏng request.
 */
export async function createCase(input: CreateCaseInput): Promise<CaseDetail> {
  const form = new FormData();
  form.append("patientProfileId", input.patientProfileId);
  form.append("responsibleDoctorId", input.responsibleDoctorId);
  if (input.clinicalInfo) form.append("clinicalInfo", input.clinicalInfo);
  if (input.symptoms && input.symptoms.length > 0) {
    form.append("symptomsJson", JSON.stringify(input.symptoms));
  }

  // Backend nhận List<IFormFile> images — append nhiều lần CÙNG khoá "images", không phải
  // "images[]". Sai tên khoá thì server nhận 0 file và trả 422 "phải có ít nhất 1 ảnh".
  for (const file of input.images) {
    form.append("images", file);
  }

  const { data } = await apiClient.post<ApiResponse<CaseDetail>>(BASE, form);

  if (!data.data) throw new Error(data.message || "Tạo ca khám thất bại.");

  return data.data;
}

/**
 * Thêm 07/08/2026 — "Lưu kết luận". Chỉ lưu nội dung, KHÔNG đổi trạng thái ca — sửa lại được
 * nhiều lần cho tới khi bấm "Kết thúc ca khám" (confirmCase). Backend chặn cả vai trò (CHỈ
 * Doctor) lẫn đúng-bác-sĩ-phụ-trách (GB-04) và trạng thái chưa CONFIRMED (P2/GB-01).
 */
export async function saveCaseConclusion(
  caseId: string,
  input: CaseConclusionInput,
): Promise<CaseDetail> {
  const { data } = await apiClient.put<ApiResponse<CaseDetail>>(`${BASE}/${caseId}/conclusion`, {
    finalDiagnosis: input.finalDiagnosis,
    doctorConclusion: input.doctorConclusion,
  });

  if (!data.data) throw new Error(data.message || "Lưu kết luận thất bại.");

  return data.data;
}

/**
 * Thêm 07/08/2026 — "Kết thúc ca khám". Lưu VÀ khoá ca (CONFIRMED) trong cùng một lần gọi,
 * không có đường lùi. Cùng hai điều kiện chặn với saveCaseConclusion (422 nếu vi phạm).
 */
export async function confirmCase(caseId: string, input: CaseConclusionInput): Promise<CaseDetail> {
  const { data } = await apiClient.put<ApiResponse<CaseDetail>>(`${BASE}/${caseId}/confirm`, {
    finalDiagnosis: input.finalDiagnosis,
    doctorConclusion: input.doctorConclusion,
  });

  if (!data.data) throw new Error(data.message || "Kết thúc ca khám thất bại.");

  return data.data;
}

/**
 * Kết thúc ca bệnh trực tiếp (chuyển CONFIRMED sang END) cho bệnh nhân không lấy thuốc.
 */
export async function endCaseWithoutPrescription(caseId: string): Promise<CaseDetail> {
  const { data } = await apiClient.put<ApiResponse<CaseDetail>>(`${BASE}/${caseId}/end`, {});

  if (!data.data) throw new Error(data.message || "Kết thúc ca bệnh thất bại.");

  return data.data;
}

/**
 * #27 — endpoint DUY NHẤT không bọc trong khuôn {code, message, data}: thân phản hồi là byte
 * của file PDF.
 *
 * Riêng nhánh lỗi thì backend vẫn trả JSON — nhưng vì đã ép responseType "blob" cho nhánh
 * thành công, axios cũng gói JSON đó thành Blob. getApiErrorMessage đọc `error.response.data.message`
 * trên một Blob sẽ ra undefined, và người dùng nhận thông báo trống. Phải đọc ngược Blob về
 * text rồi parse.
 */
export async function downloadCaseReport(caseId: string): Promise<Blob> {
  try {
    const { data } = await apiClient.get<Blob>(`${BASE}/${caseId}/report`, {
      responseType: "blob",
    });

    return data;
  } catch (error) {
    if (error instanceof AxiosError && error.response?.data instanceof Blob) {
      const text = await error.response.data.text();

      // Bắt riêng lỗi của JSON.parse (không phải lỗi tự ném ở dưới): nếu bắt chung, không
      // thể phân biệt "JSON.parse thật sự lỗi" với "mình vừa throw message" — cả hai đều
      // rơi vào cùng một catch. Thân phản hồi không phải JSON (proxy chen vào, backend sập
      // giữa chừng...) thì rơi xuống throw error gốc bên dưới thay vì lộ ra SyntaxError thô.
      let message: string | undefined;
      try {
        message = (JSON.parse(text) as ApiResponse<null>).message ?? undefined;
      } catch {
        // không phải JSON — message giữ nguyên undefined, rơi xuống throw error bên dưới.
      }

      if (message) throw new Error(message);
    }

    throw error;
  }
}
