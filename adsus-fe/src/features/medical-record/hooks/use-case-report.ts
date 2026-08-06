"use client";

import { useMutation } from "@tanstack/react-query";

import { downloadCaseReport } from "../api/cases.api";

/**
 * UC-12 — xuất báo cáo PDF của một lần khám (#27).
 *
 * Dùng useMutation chứ không useQuery: đây là hành động do người dùng bấm, không phải dữ
 * liệu cần cache. useQuery sẽ tự chạy lại khi cửa sổ lấy lại focus và tải file thêm lần nữa.
 *
 * Nút gọi hook này chỉ được bật khi ca ở trạng thái CONFIRMED (BR-01) — backend cũng chặn
 * bằng 422, nhưng bày ra một nút chắc chắn báo lỗi thì chỉ tổ làm người dùng bối rối.
 */
export function useExportCaseReport(caseId: string) {
  const mutation = useMutation({
    mutationFn: () => downloadCaseReport(caseId),
    onSuccess: (blob) => {
      // Không có API tải file nào của trình duyệt nhận thẳng Blob, nên phải dựng URL tạm,
      // bấm hộ một thẻ <a download>, rồi thu hồi URL để không rò bộ nhớ.
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `visit-report-${caseId}.pdf`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      URL.revokeObjectURL(url);
    },
  });

  return {
    exportReport: () => mutation.mutate(),
    isPending: mutation.isPending,
    error: mutation.error,
  };
}
