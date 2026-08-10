import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";

import type {
  PagedResult,
  PatientListQuery,
  PatientSummary,
} from "../types/medical-record.types";

/** UC-09 — góc nhìn lâm sàng, tách khỏi /admin/users của Admin. Doctor và Nurse đều gọi được. */
const BASE = "/api/v1/patients";

export async function searchPatients(query: PatientListQuery): Promise<PagedResult<PatientSummary>> {
  const { data } = await apiClient.get<ApiResponse<PagedResult<PatientSummary>>>(BASE, {
    // Bỏ hẳn tham số rỗng thay vì gửi chuỗi rỗng, để backend hiểu là "không lọc".
    params: {
      search: query.search || undefined,
      // "All" là mặc định phía backend rồi, gửi lên chỉ thêm nhiễu vào URL.
      visitStatus: query.visitStatus && query.visitStatus !== "All" ? query.visitStatus : undefined,
      // Phải so sánh với undefined chứ không dùng ||: hasProfile=false là giá trị CÓ nghĩa.
      hasProfile: query.hasProfile === undefined ? undefined : query.hasProfile,
      page: query.page ?? 1,
      pageSize: query.pageSize ?? 20,
    },
  });

  if (!data.data) throw new Error(data.message || "Không tải được danh sách bệnh nhân.");

  return data.data;
}
