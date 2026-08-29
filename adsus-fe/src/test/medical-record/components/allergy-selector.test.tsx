import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { AllergySelector } from "@/features/medical-record/components/allergy-selector";

const { allergyTypesMock } = vi.hoisted(() => ({ allergyTypesMock: vi.fn() }));

vi.mock("@/features/medical-record/hooks/use-medical-dictionaries", () => ({
  useAllergyTypes: () => allergyTypesMock(),
}));

const allergyTypes = [
  { id: "a-1", name: "Dị ứng thuốc kháng sinh", isOther: false },
  { id: "a-2", name: "Khác", isOther: true },
];

describe("AllergySelector", () => {
  beforeEach(() => {
    allergyTypesMock.mockReset();
  });

  it("hiện trạng thái đang tải", () => {
    allergyTypesMock.mockReturnValue({ data: undefined, isLoading: true });

    render(<AllergySelector value={[]} onChange={vi.fn()} />);

    expect(screen.getByText(/đang tải danh mục dị ứng/i)).toBeInTheDocument();
  });

  it("sắp xếp mục 'Khác' (isOther) xuống cuối danh sách", () => {
    allergyTypesMock.mockReturnValue({ data: allergyTypes, isLoading: false });

    render(<AllergySelector value={[]} onChange={vi.fn()} />);

    const labels = screen.getAllByRole("checkbox").map((cb) => cb.closest("label")?.textContent);
    expect(labels).toEqual(["Dị ứng thuốc kháng sinh", "Khác"]);
  });

  it("tick chọn một dị ứng thì gọi onChange kèm note rỗng, và hiện ô nhập ghi chú", async () => {
    allergyTypesMock.mockReturnValue({ data: allergyTypes, isLoading: false });
    const onChange = vi.fn();

    const { rerender } = render(<AllergySelector value={[]} onChange={onChange} />);
    await userEvent.click(screen.getByRole("checkbox", { name: /dị ứng thuốc kháng sinh/i }));

    expect(onChange).toHaveBeenCalledWith([{ allergyTypeId: "a-1", note: "" }]);

    // Giả lập parent cập nhật lại value theo onChange vừa gọi, để kiểm ô ghi chú xuất hiện.
    rerender(
      <AllergySelector value={[{ allergyTypeId: "a-1", note: "" }]} onChange={onChange} />,
    );
    expect(screen.getByPlaceholderText(/nhập chi tiết dị ứng/i)).toBeInTheDocument();
  });

  it("bỏ tick một dị ứng đã chọn thì loại nó khỏi danh sách", async () => {
    allergyTypesMock.mockReturnValue({ data: allergyTypes, isLoading: false });
    const onChange = vi.fn();

    render(
      <AllergySelector value={[{ allergyTypeId: "a-1", note: "Nổi mẩn" }]} onChange={onChange} />,
    );
    await userEvent.click(screen.getByRole("checkbox", { name: /dị ứng thuốc kháng sinh/i }));

    expect(onChange).toHaveBeenCalledWith([]);
  });

  it("sửa ghi chú chỉ cập nhật đúng dị ứng đang gõ, giữ nguyên dị ứng khác", async () => {
    allergyTypesMock.mockReturnValue({ data: allergyTypes, isLoading: false });
    const onChange = vi.fn();

    render(
      <AllergySelector
        value={[
          { allergyTypeId: "a-1", note: "Không đổi" },
          { allergyTypeId: "a-2", note: "" },
        ]}
        onChange={onChange}
      />,
    );

    // a-1 xếp trước a-2 (isOther xuống cuối) nên ô ghi chú thứ 2 thuộc về a-2.
    const noteBoxes = screen.getAllByPlaceholderText(/nhập chi tiết dị ứng/i);
    await userEvent.type(noteBoxes[1], "X");

    // userEvent.type gõ từng ký tự, mỗi ký tự gọi 1 lần onChange — kiểm lần gọi cuối để tránh
    // phụ thuộc số ký tự gõ.
    const lastCall = onChange.mock.calls.at(-1)?.[0];
    expect(lastCall).toEqual(
      expect.arrayContaining([expect.objectContaining({ allergyTypeId: "a-1", note: "Không đổi" })]),
    );
    expect(lastCall.find((v: { allergyTypeId: string }) => v.allergyTypeId === "a-2").note).toBe(
      "X",
    );
  });
});
