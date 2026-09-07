import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { SymptomSelector } from "@/features/medical-record/components/symptom-selector";
import type { CreateCaseSymptomInput } from "@/features/medical-record/types/medical-record.types";

const { categoriesMock } = vi.hoisted(() => ({ categoriesMock: vi.fn() }));

vi.mock("@/features/medical-record/hooks/use-symptoms", () => ({
  useSymptomCategories: () => categoriesMock(),
}));

const categories = [
  {
    categoryId: "cat-pain",
    name: "Đau vú",
    isOther: false,
    symptoms: [
      { symptomId: "sym-touch", name: "Đau khi chạm", isOther: false },
      { symptomId: "sym-rest", name: "Đau khi nghỉ", isOther: false },
    ],
  },
  {
    categoryId: "cat-lump",
    name: "Khối u",
    isOther: false,
    symptoms: [{ symptomId: "sym-hard", name: "Cứng", isOther: false }],
  },
  {
    categoryId: "cat-other",
    name: "Khác",
    isOther: true,
    symptoms: [],
  },
];

function renderSelector(value: CreateCaseSymptomInput[], onChange = vi.fn()) {
  render(<SymptomSelector value={value} onChange={onChange} />);
  return onChange;
}

describe("SymptomSelector", () => {
  beforeEach(() => {
    categoriesMock.mockReset();
    categoriesMock.mockReturnValue({ data: categories, isLoading: false });
  });

  it("hiện trạng thái đang tải", () => {
    categoriesMock.mockReturnValue({ data: undefined, isLoading: true });

    render(<SymptomSelector value={[]} onChange={vi.fn()} />);

    expect(screen.getByText(/đang tải danh mục triệu chứng/i)).toBeInTheDocument();
  });

  it("chưa có block nào thì chỉ hiện nút Thêm nhóm triệu chứng", () => {
    renderSelector([]);

    expect(screen.getByRole("button", { name: /thêm nhóm triệu chứng/i })).toBeInTheDocument();
    expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
  });

  it("bấm Thêm nhóm triệu chứng thì thêm 1 block rỗng", async () => {
    const onChange = renderSelector([]);

    await userEvent.click(screen.getByRole("button", { name: /thêm nhóm triệu chứng/i }));

    expect(onChange).toHaveBeenCalledWith([{ categoryId: "", symptomId: null, otherNote: null }]);
  });

  it("chọn nhóm 'Khác' (isOther) thì hiện textarea thay vì danh sách checkbox", async () => {
    const onChange = renderSelector([{ categoryId: "", symptomId: null, otherNote: null }]);

    await userEvent.selectOptions(screen.getByRole("combobox"), "cat-other");

    expect(onChange).toHaveBeenCalledWith([
      { categoryId: "cat-other", symptomId: null, otherNote: null },
    ]);
  });

  it("chọn nhóm thường thì hiện checkbox từng triệu chứng, tick một triệu chứng gán luôn vào block trống", async () => {
    const onChange = renderSelector([{ categoryId: "cat-pain", symptomId: null, otherNote: null }]);

    expect(screen.getByRole("checkbox", { name: /đau khi chạm/i })).toBeInTheDocument();

    await userEvent.click(screen.getByRole("checkbox", { name: /đau khi chạm/i }));

    expect(onChange).toHaveBeenCalledWith([
      { categoryId: "cat-pain", symptomId: "sym-touch", otherNote: null },
    ]);
  });

  it("tick thêm triệu chứng thứ 2 cùng nhóm thì chèn thêm 1 item mới, không ghi đè item đầu", async () => {
    const onChange = renderSelector([
      { categoryId: "cat-pain", symptomId: "sym-touch", otherNote: null },
    ]);

    await userEvent.click(screen.getByRole("checkbox", { name: /đau khi nghỉ/i }));

    expect(onChange).toHaveBeenCalledWith([
      { categoryId: "cat-pain", symptomId: "sym-touch", otherNote: null },
      { categoryId: "cat-pain", symptomId: "sym-rest", otherNote: null },
    ]);
  });

  it("bỏ tick triệu chứng đã chọn thì loại đúng item, giữ block trống để hiện lại select", async () => {
    const onChange = renderSelector([
      { categoryId: "cat-pain", symptomId: "sym-touch", otherNote: null },
    ]);

    await userEvent.click(screen.getByRole("checkbox", { name: /đau khi chạm/i }));

    expect(onChange).toHaveBeenCalledWith([
      { categoryId: "cat-pain", symptomId: null, otherNote: null },
    ]);
  });

  it("nhóm đã được chọn ở block khác thì bị ẩn khỏi option của block còn lại", () => {
    renderSelector([
      { categoryId: "cat-pain", symptomId: "sym-touch", otherNote: null },
      { categoryId: "", symptomId: null, otherNote: null },
    ]);

    const selects = screen.getAllByRole("combobox");
    const secondBlockOptions = Array.from(selects[1].querySelectorAll("option")).map(
      (o) => o.textContent,
    );

    expect(secondBlockOptions).not.toContain("Đau vú");
    expect(secondBlockOptions).toContain("Khối u");
  });

  it("nhóm không có sẵn triệu chứng 'Khác' trong DB thì hiện checkbox fallback 'Khác...'", async () => {
    const onChange = renderSelector([{ categoryId: "cat-lump", symptomId: null, otherNote: null }]);

    expect(screen.getByRole("checkbox", { name: /khác\.\.\./i })).toBeInTheDocument();

    await userEvent.click(screen.getByRole("checkbox", { name: /khác\.\.\./i }));

    expect(onChange).toHaveBeenCalledWith([
      { categoryId: "cat-lump", symptomId: null, otherNote: "" },
    ]);
  });

  it("bấm nút xoá (thùng rác) thì loại bỏ toàn bộ item của block đó", async () => {
    const onChange = renderSelector([
      { categoryId: "cat-pain", symptomId: "sym-touch", otherNote: null },
      { categoryId: "cat-pain", symptomId: "sym-rest", otherNote: null },
      { categoryId: "cat-lump", symptomId: "sym-hard", otherNote: null },
    ]);

    // Block 1 (cat-pain) là block đầu tiên hiển thị.
    await userEvent.click(screen.getAllByTitle(/xoá nhóm này/i)[0]);

    expect(onChange).toHaveBeenCalledWith([
      { categoryId: "cat-lump", symptomId: "sym-hard", otherNote: null },
    ]);
  });
});
