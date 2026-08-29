import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { UltrasoundUploadField } from "@/features/medical-record/components/ultrasound-upload-field";

function makeFile(name: string, type: string, sizeBytes: number): File {
  return new File([new Uint8Array(sizeBytes)], name, { type });
}

beforeEach(() => {
  URL.createObjectURL = vi.fn(() => "blob:fake");
});

describe("UltrasoundUploadField", () => {
  it("chọn ảnh JPEG/PNG hợp lệ thì thêm vào danh sách files hiện có", async () => {
    const onChange = vi.fn();
    render(<UltrasoundUploadField files={[]} onChange={onChange} />);

    const input = screen.getByLabelText(/chọn ảnh siêu âm/i);
    await userEvent.upload(input, makeFile("a.jpg", "image/jpeg", 1024));

    expect(onChange).toHaveBeenCalledWith([expect.objectContaining({ name: "a.jpg" })]);
  });

  it("từ chối file sai định dạng, không đưa vào onChange, hiện thông báo lỗi", async () => {
    const onChange = vi.fn();
    render(<UltrasoundUploadField files={[]} onChange={onChange} />);

    // applyAccept: false — mặc định userEvent.upload tự lọc theo thuộc tính `accept` của input
    // TRƯỚC khi bắn sự kiện change, nên file sai định dạng không bao giờ tới được handleSelect
    // để test được nhánh validate JS thật (accept trên trình duyệt thật chỉ là gợi ý UI, người
    // dùng vẫn chọn được file khác qua "All files" — validate JS này là hàng phòng thủ thật).
    const user = userEvent.setup({ applyAccept: false });
    const input = screen.getByLabelText(/chọn ảnh siêu âm/i);
    await user.upload(input, makeFile("scan.dcm", "application/dicom", 1024));

    expect(onChange).toHaveBeenCalledWith([]);
    expect(screen.getByRole("alert")).toHaveTextContent(/chỉ nhận ảnh jpeg hoặc png/i);
  });

  it("từ chối file vượt quá 20MB", async () => {
    const onChange = vi.fn();
    render(<UltrasoundUploadField files={[]} onChange={onChange} />);

    const input = screen.getByLabelText(/chọn ảnh siêu âm/i);
    const tooBig = makeFile("big.jpg", "image/jpeg", 21 * 1024 * 1024);
    await userEvent.upload(input, tooBig);

    expect(onChange).toHaveBeenCalledWith([]);
    expect(screen.getByRole("alert")).toHaveTextContent(/vượt quá 20mb/i);
  });

  it("render preview + nút xoá cho từng file đã chọn, bấm xoá gọi onChange loại đúng file", async () => {
    const onChange = vi.fn();
    const existing = [makeFile("a.jpg", "image/jpeg", 1024), makeFile("b.jpg", "image/jpeg", 1024)];
    render(<UltrasoundUploadField files={existing} onChange={onChange} />);

    expect(screen.getAllByRole("img")).toHaveLength(2);

    await userEvent.click(screen.getByRole("button", { name: /bỏ ảnh a\.jpg/i }));
    expect(onChange).toHaveBeenCalledWith([existing[1]]);
  });

  it("disabled=true thì input và nút xoá đều bị vô hiệu hoá", () => {
    const existing = [makeFile("a.jpg", "image/jpeg", 1024)];
    render(<UltrasoundUploadField files={existing} onChange={vi.fn()} disabled />);

    expect(screen.getByLabelText(/chọn ảnh siêu âm/i)).toBeDisabled();
    expect(screen.getByRole("button", { name: /bỏ ảnh a\.jpg/i })).toBeDisabled();
  });
});
