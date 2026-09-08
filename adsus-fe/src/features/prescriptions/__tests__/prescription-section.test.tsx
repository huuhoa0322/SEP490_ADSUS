import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import "@testing-library/jest-dom/vitest";

import { PrescriptionSection } from "../components/prescription-section";
import type { PrescriptionWithComplianceResponse } from "../types/prescriptions.types";

const { useCasePrescriptionMock } = vi.hoisted(() => ({
  useCasePrescriptionMock: vi.fn(),
}));

vi.mock("../hooks/use-prescriptions", () => ({
  useCasePrescriptionWithCompliance: (caseId: string) => useCasePrescriptionMock(caseId),
}));

const mockPrescriptionData: PrescriptionWithComplianceResponse[] = [
  {
    prescriptionId: "rx-101",
    caseId: "case-01",
    doctorId: "user-doctor-1",
    prescribedDate: "2026-09-08",
    createdAt: "2026-09-08T08:00:00Z",
    updatedAt: "2026-09-08T08:00:00Z",
    adherencePercent: 85,
    generalNote: "Uống thuốc sau bữa ăn 30 phút, kiêng đồ chua cay.",
    items: [
      {
        prescriptionItemId: "item-1",
        medicineId: "med-1",
        medicineName: "Amoxicillin 500mg",
        dosage: "1 viên/lần",
        scheduleSlots: ["Morning", "Evening"],
        startDate: "2026-09-08",
        durationDays: 7,
        instructions: "Uống nhiều nước",
        adherencePercent: 90,
      },
      {
        prescriptionItemId: "item-2",
        medicineId: "med-2",
        medicineName: "Paracetamol 500mg",
        dosage: "1 viên",
        scheduleSlots: ["Noon"],
        startDate: "2026-09-08",
        durationDays: 3,
        instructions: "Chỉ uống khi sốt trên 38.5 độ",
        adherencePercent: 80,
      },
    ],
  },
];

describe("PrescriptionSection", () => {
  beforeEach(() => {
    useCasePrescriptionMock.mockReset();
  });

  it("hiển thị trạng thái đang tải với bounding box viền đậm và chữ đậm tối màu", () => {
    useCasePrescriptionMock.mockReturnValue({
      data: undefined,
      isLoading: true,
    });

    const { container } = render(<PrescriptionSection caseId="case-01" />);

    const loadingText = screen.getByText("Đang tải đơn thuốc...");
    expect(loadingText).toBeInTheDocument();
    expect(loadingText).toHaveClass("font-bold", "text-foreground");

    const loadingCard = container.querySelector("div");
    expect(loadingCard?.className).toMatch(/border-gray-300/);
  });

  it("trả về null khi không có đơn thuốc", () => {
    useCasePrescriptionMock.mockReturnValue({
      data: [],
      isLoading: false,
    });

    const { container } = render(<PrescriptionSection caseId="case-01" />);
    expect(container.firstChild).toBeNull();
  });

  it("hiển thị đơn thuốc với bounding boxes và typography chuẩn Milestone 3 (không có màu xám nhạt)", () => {
    useCasePrescriptionMock.mockReturnValue({
      data: mockPrescriptionData,
      isLoading: false,
    });

    const { container } = render(<PrescriptionSection caseId="case-01" />);

    // 1. Tiêu đề Đơn thuốc
    const heading = screen.getByRole("heading", { name: /đơn thuốc/i, level: 2 });
    expect(heading).toBeInTheDocument();
    expect(heading).toHaveClass("font-heading", "font-bold", "text-foreground");
    expect(heading).not.toHaveClass("text-muted-foreground");

    // 2. Ngày kê
    expect(screen.getByText(/ngày kê:/i)).toHaveClass("font-bold", "text-foreground");
    expect(screen.getByText("08/09/2026")).toHaveClass("font-bold", "text-foreground");

    // 3. Section Bounding Box (border-black)
    const section = container.querySelector("section");
    expect(section).toBeInTheDocument();
    expect(section?.className).toMatch(/border-gray-300/);

    // 4. Bảng và Table Header: font-bold text-foreground, viền border-black
    const tableWrapper = container.querySelector("div.overflow-hidden");
    expect(tableWrapper?.className).toMatch(/border-gray-300/);

    const headers = screen.getAllByRole("columnheader");
    expect(headers.length).toBe(6);
    headers.forEach((th) => {
      expect(th).toHaveClass("font-bold", "text-foreground");
      expect(th).not.toHaveClass("text-muted-foreground");
    });

    // 5. Thuốc và Liều dùng
    expect(screen.getByText("Amoxicillin 500mg")).toBeInTheDocument();
    expect(screen.getByText("1 viên/lần")).toBeInTheDocument();
    expect(screen.getByText("Sáng, Tối")).toBeInTheDocument();
    expect(screen.getByText("Uống nhiều nước")).toBeInTheDocument();

    // 6. Ghi chú có viền rõ và font-bold
    expect(screen.getByText(/ghi chú:/i)).toHaveClass("font-bold", "text-foreground");
    expect(screen.getByText(/uống thuốc sau bữa ăn 30 phút/i)).toBeInTheDocument();
    const noteCard = screen.getByText(/ghi chú:/i).closest("div");
    expect(noteCard?.className).toMatch(/border-gray-300/);
  });
});
