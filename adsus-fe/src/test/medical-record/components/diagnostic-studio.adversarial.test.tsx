import { Suspense } from "react";
import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import DiagnosticPage from "@/app/(protected)/cases/[caseId]/diagnostic/page";
import { DiagnosticCanvas } from "@/features/medical-record/components/diagnostic-canvas";
import { useDiagnosticStore } from "@/features/medical-record/stores/use-diagnostic-store";
import { analyzeImage, confirmAnalysis } from "@/features/medical-record/api/cases-diagnosis.api";
import { generateBurntImage, checkIntersection } from "@/features/medical-record/utils/canvas-utils";

// Mocks
const pushMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({
    push: pushMock,
  }),
}));

vi.mock("@/features/medical-record/api/cases-diagnosis.api", () => ({
  analyzeImage: vi.fn(),
  confirmAnalysis: vi.fn(),
}));

vi.mock("@/features/medical-record/utils/canvas-utils", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/features/medical-record/utils/canvas-utils")>();
  return {
    ...actual,
    generateBurntImage: vi.fn(),
  };
});

function fakeFile(name = "test.jpg"): File {
  return new File([new Uint8Array([1, 2, 3])], name, { type: "image/jpeg" });
}

function createMockImage(width = 800, height = 600) {
  const img = document.createElement("img");
  Object.defineProperty(img, "width", { value: width, writable: true });
  Object.defineProperty(img, "height", { value: height, writable: true });
  Object.defineProperty(img, "naturalWidth", { value: width, writable: true });
  Object.defineProperty(img, "naturalHeight", { value: height, writable: true });

  let srcVal = "";
  Object.defineProperty(img, "src", {
    get() {
      return srcVal;
    },
    set(val: string) {
      srcVal = val;
      setTimeout(() => {
        if (typeof img.onload === "function") {
          img.onload(new Event("load") as unknown as Event);
        }
      }, 0);
    },
  });

  return img;
}

const MockImageConstructor = function (w?: number, h?: number) {
  return createMockImage(w || 800, h || 600);
} as unknown as typeof Image;

class NeverLoadingImage {
  width = 0;
  height = 0;
  onload: (() => void) | null = null;
  onerror: (() => void) | null = null;
  src = "";
}

function renderWithQueryClient(ui: React.ReactElement) {
  const client = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  });
  return render(
    <QueryClientProvider client={client}>
      <Suspense fallback={<div>Loading Suspense...</div>}>
        {ui}
      </Suspense>
    </QueryClientProvider>
  );
}

describe("Diagnostic Studio & Buttons — Adversarial Stress Testing", () => {
  beforeEach(() => {
    URL.createObjectURL = vi.fn(() => "blob:fake-url");
    URL.revokeObjectURL = vi.fn();

    Element.prototype.setPointerCapture = vi.fn();
    Element.prototype.releasePointerCapture = vi.fn();
    Element.prototype.hasPointerCapture = vi.fn();

    // Mock getBoundingClientRect for SVG coordinate transforms
    Element.prototype.getBoundingClientRect = vi.fn(() => ({
      width: 800,
      height: 600,
      top: 0,
      left: 0,
      bottom: 600,
      right: 800,
      x: 0,
      y: 0,
      toJSON: () => {},
    }));

    vi.stubGlobal("Image", MockImageConstructor);
  });

  afterEach(() => {
    act(() => {
      useDiagnosticStore.getState().clearSession();
    });
    vi.clearAllMocks();
    vi.unstubAllGlobals();
  });

  describe("1. DiagnosticPage Header & Navigation Controls Stress Tests", () => {
    it("renders page with multi-image session and verifies 'Quay lại ca khám' link", async () => {
      useDiagnosticStore.getState().setDiagnosticSession("case-nav", [
        fakeFile("img1.jpg"),
        fakeFile("img2.jpg"),
        fakeFile("img3.jpg"),
      ]);

      const params = Promise.resolve({ caseId: "case-nav" });
      await act(async () => {
        renderWithQueryClient(<DiagnosticPage params={params} />);
      });

      await waitFor(() => {
        expect(screen.getByText("AI Ultrasound Diagnostic Studio")).toBeInTheDocument();
      });

      const backLink = screen.getByRole("link", { name: /quay lại ca khám/i });
      expect(backLink).toBeInTheDocument();
      expect(backLink).toHaveAttribute("href", "/cases/case-nav");
    });

    it("prev button is disabled on first image (index 0) and does not decrement below 0 on click", async () => {
      useDiagnosticStore.getState().setDiagnosticSession("case-nav", [
        fakeFile("img1.jpg"),
        fakeFile("img2.jpg"),
      ]);

      const params = Promise.resolve({ caseId: "case-nav" });
      await act(async () => {
        renderWithQueryClient(<DiagnosticPage params={params} />);
      });

      await waitFor(() => {
        expect(screen.getByText("Ảnh 1 / 2")).toBeInTheDocument();
      });

      const prevBtn = screen.getByRole("button", { name: /ảnh trước/i });
      expect(prevBtn).toBeDisabled();

      // Stress click prev button 10 times
      for (let i = 0; i < 10; i++) {
        fireEvent.click(prevBtn);
      }

      expect(useDiagnosticStore.getState().currentIndex).toBe(0);
      expect(screen.getByText("Ảnh 1 / 2")).toBeInTheDocument();
    });

    it("next button is disabled on last image and does not increment beyond bounds", async () => {
      useDiagnosticStore.getState().setDiagnosticSession("case-nav", [
        fakeFile("img1.jpg"),
        fakeFile("img2.jpg"),
      ]);
      useDiagnosticStore.getState().setCurrentIndex(1);

      const params = Promise.resolve({ caseId: "case-nav" });
      await act(async () => {
        renderWithQueryClient(<DiagnosticPage params={params} />);
      });

      await waitFor(() => {
        expect(screen.getByText("Ảnh 2 / 2")).toBeInTheDocument();
      });

      const nextBtn = screen.getByRole("button", { name: /ảnh kế tiếp/i });
      expect(nextBtn).toBeDisabled();

      // Stress click next button 10 times
      for (let i = 0; i < 10; i++) {
        fireEvent.click(nextBtn);
      }

      expect(useDiagnosticStore.getState().currentIndex).toBe(1);
      expect(screen.getByText("Ảnh 2 / 2")).toBeInTheDocument();
    });

    it("cycles correctly between next and prev buttons through full image set", async () => {
      useDiagnosticStore.getState().setDiagnosticSession("case-nav", [
        fakeFile("img1.jpg"),
        fakeFile("img2.jpg"),
        fakeFile("img3.jpg"),
      ]);

      const params = Promise.resolve({ caseId: "case-nav" });
      await act(async () => {
        renderWithQueryClient(<DiagnosticPage params={params} />);
      });

      await waitFor(() => {
        expect(screen.getByText("Ảnh 1 / 3")).toBeInTheDocument();
      });

      const nextBtn = screen.getByRole("button", { name: /ảnh kế tiếp/i });
      const prevBtn = screen.getByRole("button", { name: /ảnh trước/i });

      // Click Next -> Index 1
      fireEvent.click(nextBtn);
      await waitFor(() => expect(screen.getByText("Ảnh 2 / 3")).toBeInTheDocument());

      // Click Next -> Index 2 (last)
      fireEvent.click(nextBtn);
      await waitFor(() => expect(screen.getByText("Ảnh 3 / 3")).toBeInTheDocument());
      expect(nextBtn).toBeDisabled();

      // Click Prev -> Index 1
      fireEvent.click(prevBtn);
      await waitFor(() => expect(screen.getByText("Ảnh 2 / 3")).toBeInTheDocument());

      // Click Prev -> Index 0
      fireEvent.click(prevBtn);
      await waitFor(() => expect(screen.getByText("Ảnh 1 / 3")).toBeInTheDocument());
      expect(prevBtn).toBeDisabled();
    });

    it("redirects and renders null when session has 0 images without runtime crash", async () => {
      useDiagnosticStore.getState().setDiagnosticSession("case-empty", []);

      const params = Promise.resolve({ caseId: "case-empty" });
      let renderedContainer: HTMLElement;
      await act(async () => {
        const { container } = renderWithQueryClient(<DiagnosticPage params={params} />);
        renderedContainer = container;
      });

      await waitFor(() => {
        expect(pushMock).toHaveBeenCalledWith("/cases/case-empty");
      });
      expect(renderedContainer!.textContent).toBe("");
    });

    it("locks both prev and next buttons when session has exactly 1 image", async () => {
      useDiagnosticStore.getState().setDiagnosticSession("case-single", [fakeFile("single.jpg")]);

      const params = Promise.resolve({ caseId: "case-single" });
      await act(async () => {
        renderWithQueryClient(<DiagnosticPage params={params} />);
      });

      await waitFor(() => {
        expect(screen.getByText("Ảnh 1 / 1")).toBeInTheDocument();
      });

      const prevBtn = screen.getByRole("button", { name: /ảnh trước/i });
      const nextBtn = screen.getByRole("button", { name: /ảnh kế tiếp/i });

      expect(prevBtn).toBeDisabled();
      expect(nextBtn).toBeDisabled();
    });
  });

  describe("2. Zoom & Pan Controls Stress Tests", () => {
    it("handles Zoom In (+), Zoom Out (−), and Fit (↺ Fit) clicks in both AI and Edit panels without error", async () => {
      const { container } = render(
        <DiagnosticCanvas caseId="case-zoom" file={fakeFile()} onConfirm={vi.fn()} />,
      );

      // Wait for mock image to trigger onload and pan-zoom controller initialization
      await waitFor(() => {
        const textElements = within(container).queryAllByText(/Fit/i);
        expect(textElements.length).toBeGreaterThan(0);
      });

      const plusButtons = within(container).getAllByText("+", { exact: true });
      const minusButtons = within(container).getAllByText("−", { exact: true });
      const fitButtons = within(container).getAllByText(/Fit/i);

      expect(plusButtons.length).toBe(2); // One per panel
      expect(minusButtons.length).toBe(2);
      expect(fitButtons.length).toBeGreaterThanOrEqual(2);

      // Rapidly click zoom in 20 times on each panel
      for (const btn of plusButtons) {
        for (let i = 0; i < 20; i++) {
          fireEvent.click(btn);
        }
      }

      // Rapidly click zoom out 20 times on each panel
      for (const btn of minusButtons) {
        for (let i = 0; i < 20; i++) {
          fireEvent.click(btn);
        }
      }

      // Click Fit on each panel
      for (const btn of fitButtons) {
        fireEvent.click(btn);
      }

      // No crash, canvas remains healthy
      expect(container).toBeInTheDocument();
    });

    it("safe no-op when zoom buttons are clicked while image is still loading (uninitialized controller)", async () => {
      vi.stubGlobal("Image", NeverLoadingImage);

      const { container } = render(
        <DiagnosticCanvas caseId="case-zoom" file={fakeFile()} onConfirm={vi.fn()} />,
      );

      expect(screen.getAllByText("Đang tải ảnh...").length).toBe(2);

      const plusButtons = within(container).getAllByText("+", { exact: true });
      const minusButtons = within(container).getAllByText("−", { exact: true });
      const fitButtons = within(container).getAllByText(/Fit/i);

      for (let i = 0; i < 10; i++) {
        plusButtons.forEach(b => fireEvent.click(b));
        minusButtons.forEach(b => fireEvent.click(b));
        fitButtons.forEach(b => fireEvent.click(b));
      }

      expect(container).toBeInTheDocument();
    });

    it("survives pan gestures (pointerdown, pointermove, pointerup) and wheel events on panels", async () => {
      const { container } = render(
        <DiagnosticCanvas caseId="case-pan" file={fakeFile()} onConfirm={vi.fn()} />,
      );

      await waitFor(() => {
        expect(container.querySelector(".canvas-inner")).toBeInTheDocument();
      });

      const panels = container.querySelectorAll(".cursor-grab");
      expect(panels.length).toBeGreaterThan(0);

      for (const panel of panels) {
        // Pointer down
        fireEvent.pointerDown(panel, { clientX: 100, clientY: 100, pointerId: 1 });
        // Pointer move
        fireEvent.pointerMove(panel, { clientX: 150, clientY: 160, pointerId: 1 });
        // Pointer up
        fireEvent.pointerUp(panel, { clientX: 150, clientY: 160, pointerId: 1 });

        // Wheel event
        fireEvent.wheel(panel, { clientX: 100, clientY: 100, deltaY: -120 });
        fireEvent.wheel(panel, { clientX: 100, clientY: 100, deltaY: 120 });
      }

      expect(container).toBeInTheDocument();
    });
  });

  describe("3. Caliper Addition, Dragging, Rejection & Math Calculations", () => {
    it("toggles '+ Thêm caliper' mode and resets adding clicks correctly", async () => {
      render(<DiagnosticCanvas caseId="case-caliper" file={fakeFile()} onConfirm={vi.fn()} />);

      const toggleBtn = screen.getByRole("button", { name: /\+ thêm caliper/i });
      expect(toggleBtn).toBeInTheDocument();

      // Click to enable adding mode
      fireEvent.click(toggleBtn);
      expect(document.body.getAttribute("data-adding")).toBe("true");
      expect(screen.getByText(/Click 4 điểm: 0\/4/i)).toBeInTheDocument();

      // Click again to cancel
      fireEvent.click(toggleBtn);
      expect(document.body.getAttribute("data-adding")).toBe("false");

      // Rapid toggle 10 times
      for (let i = 0; i < 10; i++) {
        fireEvent.click(toggleBtn);
      }
    });

    it("creates a doctor-added caliper by clicking 4 coordinates on the edit canvas", async () => {
      const { container } = render(
        <DiagnosticCanvas caseId="case-caliper" file={fakeFile()} onConfirm={vi.fn()} />,
      );

      await waitFor(() => {
        expect(container.querySelector("#editSVG")).toBeInTheDocument();
      });

      // Enable adding mode
      const toggleBtn = screen.getByRole("button", { name: /\+ thêm caliper/i });
      fireEvent.click(toggleBtn);

      const editCanvas = container.querySelector('[role="button"][tabIndex="0"]') as HTMLElement;
      expect(editCanvas).toBeInTheDocument();

      // Click 4 points that create intersecting caliper lines
      // Pair A: (10, 10) to (50, 50)
      // Pair B: (10, 50) to (50, 10)
      fireEvent.click(editCanvas, { clientX: 10, clientY: 10 });
      expect(screen.getByText(/Click 4 điểm: 1\/4/i)).toBeInTheDocument();

      fireEvent.click(editCanvas, { clientX: 50, clientY: 50 });
      expect(screen.getByText(/Click 4 điểm: 2\/4/i)).toBeInTheDocument();

      fireEvent.click(editCanvas, { clientX: 10, clientY: 50 });
      expect(screen.getByText(/Click 4 điểm: 3\/4/i)).toBeInTheDocument();

      fireEvent.click(editCanvas, { clientX: 50, clientY: 10 });

      // After 4 clicks, addingMode resets to false
      expect(document.body.getAttribute("data-adding")).toBe("false");

      // Draft in store contains the newly added caliper
      const draft = useDiagnosticStore.getState().drafts[0];
      expect(draft?.lesions).toHaveLength(1);
      expect(draft?.lesions[0].source).toBe("doctor_added");
      expect(draft?.lesions[0].rejected).toBe(false);
    });

    it("caliper reject button ('×') marks the lesion as rejected", async () => {
      act(() => {
        useDiagnosticStore.setState({
          drafts: {
            0: {
              note: "",
              lesions: [
                {
                  pair_a: [{ x: 10, y: 10 }, { x: 50, y: 50 }],
                  pair_b: [{ x: 10, y: 50 }, { x: 50, y: 10 }],
                  source: "doctor_added",
                  rejected: false,
                  isValid: true,
                },
              ],
            },
          },
        });
      });

      const { container } = render(
        <DiagnosticCanvas caseId="case-caliper" file={fakeFile()} onConfirm={vi.fn()} />,
      );

      await waitFor(() => {
        expect(container.querySelector('[data-reject-for="0"]')).toBeInTheDocument();
      });

      const rejectBtn = container.querySelector('[data-reject-for="0"]') as SVGElement;
      fireEvent.click(rejectBtn);

      expect(useDiagnosticStore.getState().drafts[0]?.lesions[0].rejected).toBe(true);
    });

    it("verifies mathematical intersection logic under degenerate and edge conditions", () => {
      // Intersecting
      const crossA = [{ x: 0, y: 0 }, { x: 10, y: 10 }];
      const crossB = [{ x: 0, y: 10 }, { x: 10, y: 0 }];
      expect(checkIntersection(crossA, crossB)).toBe(true);

      // Parallel lines (never intersect)
      const parallelA = [{ x: 0, y: 0 }, { x: 10, y: 0 }];
      const parallelB = [{ x: 0, y: 10 }, { x: 10, y: 10 }];
      expect(checkIntersection(parallelA, parallelB)).toBe(false);

      // Collinear disjoint lines
      const colA = [{ x: 0, y: 0 }, { x: 10, y: 0 }];
      const colB = [{ x: 20, y: 0 }, { x: 30, y: 0 }];
      expect(checkIntersection(colA, colB)).toBe(false);

      // Degenerate zero-length segments (points)
      const pointA = [{ x: 5, y: 5 }, { x: 5, y: 5 }];
      const pointB = [{ x: 5, y: 5 }, { x: 5, y: 5 }];
      expect(checkIntersection(pointA, pointB)).toBe(false);

      // Extreme coordinates (large numbers)
      const hugeA = [{ x: 0, y: 0 }, { x: 1e6, y: 1e6 }];
      const hugeB = [{ x: 0, y: 1e6 }, { x: 1e6, y: 0 }];
      expect(checkIntersection(hugeA, hugeB)).toBe(true);
    });
  });

  describe("4. Save/Confirm Button Handler & Modal Notifications", () => {
    it("blocks submission and shows modal toast when caliper does not intersect", async () => {
      act(() => {
        useDiagnosticStore.setState({
          drafts: {
            0: {
              note: "",
              lesions: [
                {
                  pair_a: [{ x: 0, y: 0 }, { x: 10, y: 0 }],
                  pair_b: [{ x: 0, y: 20 }, { x: 10, y: 20 }],
                  source: "doctor_added",
                  rejected: false,
                  isValid: true,
                },
              ],
            },
          },
        });
      });

      render(<DiagnosticCanvas caseId="case-save" file={fakeFile()} onConfirm={vi.fn()} />);

      const saveBtn = screen.getByRole("button", { name: /lưu xác nhận/i });
      fireEvent.click(saveBtn);

      await waitFor(() => {
        expect(screen.getByText(/chưa cắt nhau/i)).toBeInTheDocument();
      });

      expect(confirmAnalysis).not.toHaveBeenCalled();

      // Dismiss modal via 'Đã hiểu' button
      const dismissBtn = screen.getByRole("button", { name: /đã hiểu/i });
      fireEvent.click(dismissBtn);

      await waitFor(() => {
        expect(screen.queryByText(/chưa cắt nhau/i)).not.toBeInTheDocument();
      });
    });

    it("catches confirmAnalysis network error gracefully without crashing", async () => {
      act(() => {
        useDiagnosticStore.setState({
          drafts: {
            0: {
              note: "Note",
              lesions: [
                {
                  pair_a: [{ x: 0, y: 0 }, { x: 10, y: 10 }],
                  pair_b: [{ x: 0, y: 10 }, { x: 10, y: 0 }],
                  source: "doctor_added",
                  rejected: false,
                  isValid: true,
                },
              ],
            },
          },
        });
      });

      vi.mocked(generateBurntImage).mockResolvedValue(fakeFile("burnt.jpg"));
      vi.mocked(confirmAnalysis).mockRejectedValue(new Error("500 Internal Server Error"));

      render(<DiagnosticCanvas caseId="case-save" file={fakeFile()} onConfirm={vi.fn()} />);

      const saveBtn = screen.getByRole("button", { name: /lưu xác nhận/i });
      fireEvent.click(saveBtn);

      await waitFor(() => {
        expect(screen.getByText(/Lỗi lưu ảnh: 500 Internal Server Error/i)).toBeInTheDocument();
      });

      // Button is re-enabled and not stuck in isConfirming
      expect(saveBtn).not.toBeDisabled();
    });

    it("handles note input with extreme 5000-character text and emoji without crash", async () => {
      render(<DiagnosticCanvas caseId="case-note" file={fakeFile()} onConfirm={vi.fn()} />);

      const input = screen.getByPlaceholderText(/ghi chú cho ảnh này/i);
      const longText = "A".repeat(5000) + " 🩺🏥 Ultra sound note test";

      fireEvent.change(input, { target: { value: longText } });

      expect(useDiagnosticStore.getState().drafts[0]?.note).toBe(longText);
    });

    it("handles 'Chạy AI' button failure gracefully without unhandled rejection", async () => {
      vi.mocked(analyzeImage).mockRejectedValue(new Error("AI Gateway Offline"));

      render(<DiagnosticCanvas caseId="case-ai" file={fakeFile()} onConfirm={vi.fn()} />);

      const aiBtn = screen.getByRole("button", { name: /chạy ai/i });
      fireEvent.click(aiBtn);

      await waitFor(() => {
        expect(screen.getAllByText("Kết nối tới model AI thất bại").length).toBeGreaterThan(0);
      });

      // Status in AI panel shows failure message in red
      expect(useDiagnosticStore.getState().aiResults[0]?.sessionId).toBe("failed");
      expect(aiBtn).not.toBeDisabled();
    });
  });
});
