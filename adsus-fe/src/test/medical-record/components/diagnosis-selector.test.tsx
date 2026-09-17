import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { DiagnosisSelector } from "@/features/medical-record/components/diagnosis-selector";

const { diagnosisItemsMock } = vi.hoisted(() => ({ diagnosisItemsMock: vi.fn() }));

vi.mock("@/features/medical-record/hooks/use-diagnosis", () => ({
  useDiagnosisItems: () => diagnosisItemsMock(),
}));

const mockItems = [
  { id: "diag-1", name: "U xơ tử cung", requiresNote: false, isOther: false, displayOrder: 1 },
  { id: "diag-2", name: "Viêm âm đạo", requiresNote: true, isOther: false, displayOrder: 2 },
  { id: "diag-3", name: "Khác", requiresNote: true, isOther: true, displayOrder: 3 },
];

describe("DiagnosisSelector", () => {
  beforeEach(() => {
    diagnosisItemsMock.mockReset();
  });

  it("hiện trạng thái đang tải", () => {
    diagnosisItemsMock.mockReturnValue({ data: undefined, isLoading: true });

    render(<DiagnosisSelector value={[]} onChange={vi.fn()} />);

    expect(screen.getByText(/đang tải danh mục chẩn đoán/i)).toBeInTheDocument();
  });

  it("renders all diagnosis items from API theo thứ tự displayOrder", () => {
    diagnosisItemsMock.mockReturnValue({ data: mockItems, isLoading: false });

    render(<DiagnosisSelector value={[]} onChange={vi.fn()} />);

    const labels = screen.getAllByRole("checkbox").map((cb) => cb.closest("label")?.textContent);
    expect(labels).toEqual(["U xơ tử cung", "Viêm âm đạo", "Khác"]);
  });

  it("tick bệnh không cần ghi chú thì note = null, không hiện ô nhập", async () => {
    diagnosisItemsMock.mockReturnValue({ data: mockItems, isLoading: false });
    const onChange = vi.fn();

    render(<DiagnosisSelector value={[]} onChange={onChange} />);
    await userEvent.click(screen.getByRole("checkbox", { name: /u xơ tử cung/i }));

    expect(onChange).toHaveBeenCalledWith([{ diagnosisItemId: "diag-1", note: null }]);
    expect(screen.queryByPlaceholderText(/nhập chi tiết chẩn đoán/i)).not.toBeInTheDocument();
    expect(screen.queryByPlaceholderText(/nhập chẩn đoán khác/i)).not.toBeInTheDocument();
  });

  it("bỏ tick loại chẩn đoán khỏi danh sách", async () => {
    diagnosisItemsMock.mockReturnValue({ data: mockItems, isLoading: false });
    const onChange = vi.fn();

    render(
      <DiagnosisSelector
        value={[{ diagnosisItemId: "diag-1", note: null }]}
        onChange={onChange}
      />,
    );
    await userEvent.click(screen.getByRole("checkbox", { name: /u xơ tử cung/i }));

    expect(onChange).toHaveBeenCalledWith([]);
  });

  it("shows note textarea for requiresNote items when selected", async () => {
    diagnosisItemsMock.mockReturnValue({ data: mockItems, isLoading: false });
    const onChange = vi.fn();

    const { rerender } = render(<DiagnosisSelector value={[]} onChange={onChange} />);
    await userEvent.click(screen.getByRole("checkbox", { name: /viêm âm đạo/i }));

    expect(onChange).toHaveBeenCalledWith([{ diagnosisItemId: "diag-2", note: "" }]);

    rerender(
      <DiagnosisSelector
        value={[{ diagnosisItemId: "diag-2", note: "Do nấm Candida" }]}
        onChange={onChange}
      />,
    );

    const textarea = screen.getByPlaceholderText(/nhập chi tiết chẩn đoán/i);
    expect(textarea).toBeInTheDocument();
    expect(textarea).toHaveValue("Do nấm Candida");
  });

  it("'Khác' (isOther) shows text input for custom diagnosis", async () => {
    diagnosisItemsMock.mockReturnValue({ data: mockItems, isLoading: false });
    const onChange = vi.fn();

    const { rerender } = render(<DiagnosisSelector value={[]} onChange={onChange} />);
    await userEvent.click(screen.getByRole("checkbox", { name: /khác/i }));

    expect(onChange).toHaveBeenCalledWith([{ diagnosisItemId: "diag-3", note: "" }]);

    rerender(
      <DiagnosisSelector
        value={[{ diagnosisItemId: "diag-3", note: "Bệnh lý phụ khoa hiếm gặp" }]}
        onChange={onChange}
      />,
    );

    const textInput = screen.getByPlaceholderText(/nhập chẩn đoán khác/i);
    expect(textInput).toBeInTheDocument();
    expect(textInput).toHaveValue("Bệnh lý phụ khoa hiếm gặp");
  });
});
