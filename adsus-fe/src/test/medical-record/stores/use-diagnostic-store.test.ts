import { afterEach, describe, expect, it } from "vitest";

import { useDiagnosticStore } from "@/features/medical-record/stores/use-diagnostic-store";

function fakeImage(name: string): File {
  return new File([], name, { type: "image/jpeg" });
}

afterEach(() => {
  useDiagnosticStore.getState().clearSession();
});

describe("useDiagnosticStore", () => {
  it("setDiagnosticSession khởi tạo session và reset toàn bộ state cũ", () => {
    useDiagnosticStore.getState().setAiResult(0, { sessionId: "stale", detections: [] });

    useDiagnosticStore.getState().setDiagnosticSession("case-1", [fakeImage("a.jpg")]);

    const state = useDiagnosticStore.getState();
    expect(state.caseId).toBe("case-1");
    expect(state.images).toHaveLength(1);
    expect(state.currentIndex).toBe(0);
    expect(state.aiResults).toEqual({});
  });

  it("nextImage tăng currentIndex, prevImage không lùi quá 0", () => {
    useDiagnosticStore.getState().setDiagnosticSession("case-1", [fakeImage("a.jpg"), fakeImage("b.jpg")]);

    useDiagnosticStore.getState().nextImage();
    expect(useDiagnosticStore.getState().currentIndex).toBe(1);

    useDiagnosticStore.getState().prevImage();
    useDiagnosticStore.getState().prevImage();
    expect(useDiagnosticStore.getState().currentIndex).toBe(0);
  });

  it("goToImage nhảy thẳng tới index chỉ định", () => {
    useDiagnosticStore
      .getState()
      .setDiagnosticSession("case-1", [fakeImage("a.jpg"), fakeImage("b.jpg"), fakeImage("c.jpg")]);

    useDiagnosticStore.getState().goToImage(2);

    expect(useDiagnosticStore.getState().currentIndex).toBe(2);
  });

  it("setAiResult/setIsProcessing/setDraft chỉ cập nhật đúng index, không đụng index khác", () => {
    useDiagnosticStore.getState().setDiagnosticSession("case-1", [fakeImage("a.jpg"), fakeImage("b.jpg")]);

    useDiagnosticStore.getState().setAiResult(0, { sessionId: "sess-1", detections: [] });
    useDiagnosticStore.getState().setIsProcessing(1, true);
    useDiagnosticStore.getState().setDraft(0, { lesions: [], note: "Nghi ngờ" });

    const state = useDiagnosticStore.getState();
    expect(state.aiResults[0]).toEqual({ sessionId: "sess-1", detections: [] });
    expect(state.aiResults[1]).toBeUndefined();
    expect(state.isProcessing[1]).toBe(true);
    expect(state.isProcessing[0]).toBeUndefined();
    expect(state.drafts[0]?.note).toBe("Nghi ngờ");
  });

  it("removeImage xoá đúng ảnh và dồn lại index cho aiResults/isProcessing/drafts phía sau", () => {
    useDiagnosticStore
      .getState()
      .setDiagnosticSession("case-1", [fakeImage("a.jpg"), fakeImage("b.jpg"), fakeImage("c.jpg")]);
    useDiagnosticStore.getState().setAiResult(0, { sessionId: "sess-a", detections: [] });
    useDiagnosticStore.getState().setAiResult(1, { sessionId: "sess-b", detections: [] });
    useDiagnosticStore.getState().setAiResult(2, { sessionId: "sess-c", detections: [] });

    // Xoá ảnh giữa (index 1) — ảnh c (cũ index 2) phải dồn xuống index 1, ảnh a giữ nguyên.
    useDiagnosticStore.getState().removeImage(1);

    const state = useDiagnosticStore.getState();
    expect(state.images).toHaveLength(2);
    expect(state.images[0].name).toBe("a.jpg");
    expect(state.images[1].name).toBe("c.jpg");
    expect(state.aiResults[0]).toEqual({ sessionId: "sess-a", detections: [] });
    expect(state.aiResults[1]).toEqual({ sessionId: "sess-c", detections: [] });
  });

  it("removeImage ảnh cuối cùng đưa currentIndex về 0 thay vì âm/ngoài phạm vi", () => {
    useDiagnosticStore.getState().setDiagnosticSession("case-1", [fakeImage("a.jpg")]);

    useDiagnosticStore.getState().removeImage(0);

    const state = useDiagnosticStore.getState();
    expect(state.images).toHaveLength(0);
    expect(state.currentIndex).toBe(0);
  });

  it("clearSession trả toàn bộ state về ban đầu", () => {
    useDiagnosticStore.getState().setDiagnosticSession("case-1", [fakeImage("a.jpg")]);
    useDiagnosticStore.getState().setAiResult(0, { sessionId: "sess-1", detections: [] });

    useDiagnosticStore.getState().clearSession();

    const state = useDiagnosticStore.getState();
    expect(state.caseId).toBeNull();
    expect(state.images).toEqual([]);
    expect(state.aiResults).toEqual({});
  });
});
