// Phạm vi CÓ CHỦ ĐÍCH (xem trao đổi P_FE8, 29/08/2026): chỉ test luồng gọi API + cập nhật
// state + validate — KHÔNG test phần vẽ tay/pan-zoom thật (kéo thả điểm caliper, tính toán
// SVG, `generateBurntImage` render Canvas 2D pixel thật). Lý do: `getBoundingClientRect()`
// luôn trả 0 trong jsdom (không có layout thật) nên toạ độ pan-zoom vô nghĩa để assert; jsdom
// cũng không hỗ trợ Canvas 2D thật. `imgDims` cố ý được để nguyên ở {w:0, h:0} suốt các test
// (ảnh "chưa tải xong") — nút "Chạy AI"/"Lưu xác nhận"/ô ghi chú đều không bị khoá bởi
// `imgDims`, nên vẫn test được đầy đủ logic nghiệp vụ mà không cần mô phỏng Image.onload.
import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { DiagnosticCanvas } from "@/features/medical-record/components/diagnostic-canvas";
import { useDiagnosticStore } from "@/features/medical-record/stores/use-diagnostic-store";
import { analyzeImage, confirmAnalysis } from "@/features/medical-record/api/cases-diagnosis.api";
import { generateBurntImage } from "@/features/medical-record/utils/canvas-utils";

vi.mock("@/features/medical-record/api/cases-diagnosis.api", () => ({
  analyzeImage: vi.fn(),
  confirmAnalysis: vi.fn(),
}));

vi.mock("@/features/medical-record/utils/canvas-utils", async (importOriginal) => {
  const actual =
    await importOriginal<typeof import("@/features/medical-record/utils/canvas-utils")>();
  return {
    ...actual,
    // checkIntersection giữ nguyên bản thật (hàm hình học thuần, rẻ) — chỉ mock
    // generateBurntImage vì nó cần Canvas 2D thật mà jsdom không có.
    generateBurntImage: vi.fn(),
  };
});

function fakeFile(name = "a.jpg"): File {
  return new File([new Uint8Array([1, 2, 3])], name, { type: "image/jpeg" });
}

/**
 * Bản `Image` giả không bao giờ tự gọi `onload` — khớp đúng chủ đích của các test ở đây: ảnh
 * luôn ở trạng thái "chưa tải xong" (`imgDims.w = 0`), nút bấm/ô nhập không phụ thuộc giá trị
 * đó nên vẫn test được đầy đủ logic nghiệp vụ mà không cần mô phỏng việc load ảnh thật.
 */
class NeverLoadingImage {
  width = 0;
  height = 0;
  onload: (() => void) | null = null;
  onerror: (() => void) | null = null;
  src = "";
}

beforeEach(() => {
  URL.createObjectURL = vi.fn(() => "blob:fake");
  URL.revokeObjectURL = vi.fn();
  vi.stubGlobal("Image", NeverLoadingImage);
});

afterEach(() => {
  // Component vẫn có thể còn mounted (RTL chưa kịp tự unmount) khi afterEach này chạy —
  // clearSession() đổi state của Zustand store thật mà component đang subscribe
  // (useSyncExternalStore), nên phải bọc act() để không rơi ra ngoài — đây chính là nguồn gây
  // warning "not wrapped in act(...)" xuất hiện ở MỌI test kể cả test không tương tác gì
  // (Image giả ở trên không phải nguyên nhân, đã loại trừ qua thử nghiệm thực tế).
  act(() => {
    useDiagnosticStore.getState().clearSession();
  });
  vi.clearAllMocks();
  vi.unstubAllGlobals();
});

describe("DiagnosticCanvas", () => {
  it("render không crash, hiện đúng trạng thái ban đầu 'Chưa phân tích'", () => {
    render(<DiagnosticCanvas caseId="case-1" file={fakeFile()} onConfirm={vi.fn()} />);

    expect(screen.getByText("Chưa phân tích")).toBeInTheDocument();
  });

  it("bấm 'Chạy AI' thành công thì cập nhật số vùng abnormal, gọi đúng caseId/file", async () => {
    vi.mocked(analyzeImage).mockResolvedValue({
      sessionId: "sess-1",
      detections: [
        {
          confidence: 0.9,
          bbox: { xmin: 0.1, ymin: 0.1, xmax: 0.3, ymax: 0.3 },
          suggested_calipers: {
            pair_a: [
              [10, 10],
              [30, 30],
            ],
            pair_b: [
              [10, 30],
              [30, 10],
            ],
          },
        },
      ],
    });
    const file = fakeFile();

    render(<DiagnosticCanvas caseId="case-1" file={file} onConfirm={vi.fn()} />);
    await userEvent.click(screen.getByRole("button", { name: /chạy ai/i }));

    await waitFor(() => expect(screen.getByText(/có 1 vùng abnormal/i)).toBeInTheDocument());
    expect(analyzeImage).toHaveBeenCalledWith("case-1", file);
  });

  it("bấm 'Chạy AI' thất bại thì hiện toast lỗi, không throw ra ngoài", async () => {
    vi.mocked(analyzeImage).mockRejectedValue(new Error("network lỗi"));

    render(<DiagnosticCanvas caseId="case-1" file={fakeFile()} onConfirm={vi.fn()} />);
    await userEvent.click(screen.getByRole("button", { name: /chạy ai/i }));

    await waitFor(() =>
      expect(screen.getAllByText("Kết nối tới model AI thất bại").length).toBeGreaterThan(0),
    );
  });

  it("lưu xác nhận khi caliper chưa cắt nhau thì hiện toast lỗi, KHÔNG gọi confirmAnalysis", async () => {
    useDiagnosticStore.setState({
      drafts: {
        0: {
          note: "",
          lesions: [
            {
              // 2 đoạn song song, không cắt nhau.
              pair_a: [
                { x: 0, y: 0 },
                { x: 10, y: 0 },
              ],
              pair_b: [
                { x: 0, y: 50 },
                { x: 10, y: 50 },
              ],
              source: "doctor_added",
              rejected: false,
              isValid: true,
            },
          ],
        },
      },
    });

    render(<DiagnosticCanvas caseId="case-1" file={fakeFile()} onConfirm={vi.fn()} />);
    await userEvent.click(screen.getByRole("button", { name: /lưu xác nhận/i }));

    await waitFor(() => expect(screen.getByText(/chưa cắt nhau/i)).toBeInTheDocument());
    expect(confirmAnalysis).not.toHaveBeenCalled();
  });

  it("lưu xác nhận thành công thì gọi confirmAnalysis đúng file, hiện toast thành công, gọi onConfirm", async () => {
    useDiagnosticStore.setState({
      drafts: {
        0: {
          note: "Nghi ngờ u lành",
          lesions: [
            {
              // 2 đoạn cắt nhau tại (5,5) — hợp lệ.
              pair_a: [
                { x: 0, y: 0 },
                { x: 10, y: 10 },
              ],
              pair_b: [
                { x: 0, y: 10 },
                { x: 10, y: 0 },
              ],
              source: "doctor_added",
              rejected: false,
              isValid: true,
            },
          ],
        },
      },
    });
    const burntFile = fakeFile("burnt.jpg");
    vi.mocked(confirmAnalysis).mockResolvedValue(undefined);
    vi.mocked(generateBurntImage).mockResolvedValue(burntFile);

    const onConfirm = vi.fn();
    const file = fakeFile();
    render(<DiagnosticCanvas caseId="case-1" file={file} onConfirm={onConfirm} />);

    await userEvent.click(screen.getByRole("button", { name: /lưu xác nhận/i }));

    await waitFor(() => expect(screen.getByText(/đã chốt ảnh thành công/i)).toBeInTheDocument());
    expect(confirmAnalysis).toHaveBeenCalledTimes(1);
    const [calledCaseId, calledInput] = vi.mocked(confirmAnalysis).mock.calls[0];
    expect(calledCaseId).toBe("case-1");
    expect(calledInput.originalImage).toBe(file);
    expect(calledInput.burntImage).toBe(burntFile);
    expect(calledInput.note).toBe("Nghi ngờ u lành");
    expect(calledInput.doctorAnnotations).toHaveLength(1);
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it("gõ ghi chú thì lưu vào draft.note của ảnh hiện tại trong store", async () => {
    render(<DiagnosticCanvas caseId="case-1" file={fakeFile()} onConfirm={vi.fn()} />);

    await userEvent.type(
      screen.getByPlaceholderText(/ghi chú cho ảnh này/i),
      "Nghi ngờ u lành",
    );

    expect(useDiagnosticStore.getState().drafts[0]?.note).toBe("Nghi ngờ u lành");
  });

  it("bấm nút zoom khi ảnh chưa tải xong không crash (pan-zoom controller còn null)", async () => {
    const { container } = render(
      <DiagnosticCanvas caseId="case-1" file={fakeFile()} onConfirm={vi.fn()} />,
    );

    // imgDims.w = 0 suốt test này ("Đang tải ảnh...") nên aiPzRef/editPzRef chưa từng được
    // gán — các nút zoom gọi qua optional chaining (`aiPzRef.current?.zoomIn()`), phải là
    // no-op an toàn, không ném lỗi.
    const zoomButtons = within(container).getAllByText("+", { exact: true });
    expect(zoomButtons.length).toBeGreaterThan(0);

    for (const button of zoomButtons) {
      await userEvent.click(button);
    }
  });
});
