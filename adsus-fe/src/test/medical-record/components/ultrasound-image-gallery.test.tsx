import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";

import { UltrasoundImageGallery } from "@/features/medical-record/components/ultrasound-image-gallery";

vi.mock("next/image", () => {
  return {
    default: ({ fill, ...props }: any) => {
      // eslint-disable-next-line @next/next/no-img-element, jsx-a11y/alt-text
      return <img {...props} />;
    },
  };
});

const okImage = {
  imageId: "img-1",
  caseId: "case-1",
  imageUrl: "https://storage.example.com/img-1.jpg",
  uploadedAt: "2026-08-01T10:00:00Z",
  note: "Ảnh mặt cắt ngang",
};

const brokenImage = {
  imageId: "img-2",
  caseId: "case-1",
  imageUrl: null,
  uploadedAt: "2026-08-01T10:05:00Z",
  note: null,
};

describe("UltrasoundImageGallery", () => {
  it("hiện trạng thái rỗng khi chưa có ảnh nào", () => {
    render(<UltrasoundImageGallery images={[]} />);

    expect(screen.getByText(/chưa có ảnh siêu âm nào/i)).toBeInTheDocument();
  });

  it("render ảnh có imageUrl bằng thẻ img thật", () => {
    render(<UltrasoundImageGallery images={[okImage]} />);

    const img = screen.getByRole("img", { name: /ảnh siêu âm tải lên/i });
    expect(img).toHaveAttribute("src", okImage.imageUrl);
    expect(screen.getByText("Ảnh mặt cắt ngang")).toBeInTheDocument();
  });

  it("ảnh imageUrl null hiện ô báo lỗi thay vì <img src={null}>", () => {
    render(<UltrasoundImageGallery images={[brokenImage]} />);

    expect(screen.getByText(/không tải được ảnh/i)).toBeInTheDocument();
    expect(screen.getByText(/không có ghi chú/i)).toBeInTheDocument();
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("bấm vào ảnh mở lightbox phóng to, bấm nút X đóng lại", async () => {
    render(<UltrasoundImageGallery images={[okImage]} />);

    await userEvent.click(screen.getByRole("img", { name: /ảnh siêu âm tải lên/i }));
    expect(screen.getByRole("img", { name: /ảnh phóng to/i })).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button"));
    expect(screen.queryByRole("img", { name: /ảnh phóng to/i })).not.toBeInTheDocument();
  });
});
