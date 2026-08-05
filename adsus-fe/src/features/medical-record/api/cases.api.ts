import { AxiosError } from "axios";

import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";

import type {
  AddUltrasoundImagesInput,
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

  // Backend nhận List<IFormFile> images — append nhiều lần CÙNG khoá "images", không phải
  // "images[]". Sai tên khoá thì server nhận 0 file và trả 422 "phải có ít nhất 1 ảnh".
  for (const file of input.images) {
    form.append("images", file);
  }

  const { data } = await apiClient.post<ApiResponse<CaseDetail>>(BASE, form);

  if (!data.data) throw new Error(data.message || "Tạo ca khám thất bại.");

  return data.data;
}

/** #21 — bổ sung ảnh vào ca CHƯA chốt. Ca đã CONFIRMED sẽ bị backend từ chối (GB-01). */
export async function addUltrasoundImages(
  input: AddUltrasoundImagesInput,
): Promise<UltrasoundImage[]> {
  const form = new FormData();
  for (const file of input.images) {
    form.append("images", file);
  }
  if (input.note) form.append("note", input.note);

  const { data } = await apiClient.post<ApiResponse<UltrasoundImage[]>>(
    `${BASE}/${input.caseId}/ultrasound-images`,
    form,
  );

  if (!data.data) throw new Error(data.message || "Tải ảnh bổ sung thất bại.");

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

      try {
        const body = JSON.parse(text) as ApiResponse<null>;
        if (body.message) throw new Error(body.message);
      } catch (parseError) {
        // Thân phản hồi không phải JSON (proxy chen vào, backend sập giữa chừng...).
        // Ném lại lỗi đã dựng ở trên nếu có, còn không thì rơi xuống câu chung bên dưới.
        if (parseError instanceof Error && parseError.message !== text) throw parseError;
      }
    }

    throw error;
  }
}
