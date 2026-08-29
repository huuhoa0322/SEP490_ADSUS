// Mock ở biên api/cases-diagnosis.api (không phải MSW/HTTP) — giá trị test ở hook này là logic
// hàng đợi (khoá processLock, bỏ qua ảnh đã xong, tự lặp), phần HTTP/multipart thật đã có
// cases-diagnosis.api.test.ts riêng che phủ.
import { renderHook, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { useBackgroundAi } from "@/features/medical-record/hooks/use-background-ai";
import { useDiagnosticStore } from "@/features/medical-record/stores/use-diagnostic-store";
import { analyzeImage } from "@/features/medical-record/api/cases-diagnosis.api";

vi.mock("@/features/medical-record/api/cases-diagnosis.api", () => ({
  analyzeImage: vi.fn(),
}));

function fakeImage(name: string): File {
  return new File([], name, { type: "image/jpeg" });
}

afterEach(() => {
  useDiagnosticStore.getState().clearSession();
  vi.clearAllMocks();
});

describe("useBackgroundAi", () => {
  it("tự động phân tích ảnh chưa xử lý khi có session", async () => {
    vi.mocked(analyzeImage).mockResolvedValue({ sessionId: "sess-1", detections: [] });

    useDiagnosticStore.getState().setDiagnosticSession("case-1", [fakeImage("a.jpg")]);

    renderHook(() => useBackgroundAi());

    await waitFor(() => {
      expect(useDiagnosticStore.getState().aiResults[0]).toEqual({
        sessionId: "sess-1",
        detections: [],
      });
    });

    expect(analyzeImage).toHaveBeenCalledWith("case-1", expect.any(File));
    expect(useDiagnosticStore.getState().isProcessing[0]).toBe(false);
  });

  it("ghi nhận lỗi vào store thay vì ném exception ra ngoài khi analyzeImage throw", async () => {
    vi.mocked(analyzeImage).mockRejectedValue(new Error("network lỗi"));

    useDiagnosticStore.getState().setDiagnosticSession("case-1", [fakeImage("a.jpg")]);

    renderHook(() => useBackgroundAi());

    await waitFor(() => {
      expect(useDiagnosticStore.getState().aiResults[0]?.sessionId).toBe("failed");
    });

    // getApiErrorMessage chỉ đọc được message thật từ AxiosError — lỗi thường (như mock ở
    // trên) rơi về đúng fallback "Lỗi hệ thống" đã truyền vào use-background-ai.ts.
    expect(useDiagnosticStore.getState().aiResults[0]?.error).toBe("Lỗi hệ thống");
  });

  it("bỏ qua ảnh đã có kết quả, chỉ xử lý ảnh chưa phân tích", async () => {
    vi.mocked(analyzeImage).mockResolvedValue({ sessionId: "sess-2", detections: [] });

    useDiagnosticStore
      .getState()
      .setDiagnosticSession("case-1", [fakeImage("a.jpg"), fakeImage("b.jpg")]);
    useDiagnosticStore.getState().setAiResult(0, { sessionId: "sess-1", detections: [] });

    renderHook(() => useBackgroundAi());

    await waitFor(() => {
      expect(useDiagnosticStore.getState().aiResults[1]).toEqual({
        sessionId: "sess-2",
        detections: [],
      });
    });

    expect(analyzeImage).toHaveBeenCalledTimes(1);
  });

  it("không gọi gì khi chưa có session (caseId null)", () => {
    renderHook(() => useBackgroundAi());

    expect(analyzeImage).not.toHaveBeenCalled();
  });
});
