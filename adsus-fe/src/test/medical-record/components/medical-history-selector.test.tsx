import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { MedicalHistorySelector } from "@/features/medical-record/components/medical-history-selector";

const { diseasesMock } = vi.hoisted(() => ({ diseasesMock: vi.fn() }));

vi.mock("@/features/medical-record/hooks/use-medical-dictionaries", () => ({
  useDiseases: () => diseasesMock(),
}));

const diseases = [
  { id: "d-1", name: "Ung thư vú", requiresNote: true, isOther: false },
  { id: "d-2", name: "Cao huyết áp", requiresNote: false, isOther: false },
  { id: "d-3", name: "Khác", requiresNote: false, isOther: true },
];

describe("MedicalHistorySelector", () => {
  beforeEach(() => {
    diseasesMock.mockReset();
  });

  it("hiện trạng thái đang tải", () => {
    diseasesMock.mockReturnValue({ data: undefined, isLoading: true });

    render(<MedicalHistorySelector value={[]} onChange={vi.fn()} />);

    expect(screen.getByText(/đang tải danh mục tiền sử bệnh/i)).toBeInTheDocument();
  });

  it("sắp xếp: bệnh thường trước, bệnh cần ghi chú sát trên, 'Khác' luôn cuối cùng", () => {
    diseasesMock.mockReturnValue({ data: diseases, isLoading: false });

    render(<MedicalHistorySelector value={[]} onChange={vi.fn()} />);

    const labels = screen.getAllByRole("checkbox").map((cb) => cb.closest("label")?.textContent);
    expect(labels).toEqual(["Cao huyết áp", "Ung thư vú", "Khác"]);
  });

  it("tick bệnh KHÔNG cần ghi chú thì note = null, không hiện ô nhập", async () => {
    diseasesMock.mockReturnValue({ data: diseases, isLoading: false });
    const onChange = vi.fn();

    render(<MedicalHistorySelector value={[]} onChange={onChange} />);
    await userEvent.click(screen.getByRole("checkbox", { name: /cao huyết áp/i }));

    expect(onChange).toHaveBeenCalledWith([{ diseaseId: "d-2", note: null }]);
    expect(screen.queryByPlaceholderText(/nhập chi tiết bệnh/i)).not.toBeInTheDocument();
  });

  it("tick bệnh CẦN ghi chú thì note = chuỗi rỗng và hiện ô nhập", async () => {
    diseasesMock.mockReturnValue({ data: diseases, isLoading: false });
    const onChange = vi.fn();

    const { rerender } = render(<MedicalHistorySelector value={[]} onChange={onChange} />);
    await userEvent.click(screen.getByRole("checkbox", { name: /ung thư vú/i }));

    expect(onChange).toHaveBeenCalledWith([{ diseaseId: "d-1", note: "" }]);

    rerender(
      <MedicalHistorySelector value={[{ diseaseId: "d-1", note: "" }]} onChange={onChange} />,
    );
    expect(screen.getByPlaceholderText(/nhập chi tiết bệnh/i)).toBeInTheDocument();
  });

  it("bỏ tick loại bệnh khỏi danh sách", async () => {
    diseasesMock.mockReturnValue({ data: diseases, isLoading: false });
    const onChange = vi.fn();

    render(
      <MedicalHistorySelector value={[{ diseaseId: "d-2", note: null }]} onChange={onChange} />,
    );
    await userEvent.click(screen.getByRole("checkbox", { name: /cao huyết áp/i }));

    expect(onChange).toHaveBeenCalledWith([]);
  });
});
