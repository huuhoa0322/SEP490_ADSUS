import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { UserForm } from "./user-form";

const { createMutateMock } = vi.hoisted(() => ({
  createMutateMock: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock("../hooks/use-users", () => ({
  useUserDetail: () => ({
    data: undefined,
    isLoading: false,
    isError: false,
    error: null,
  }),
  useCreateUser: () => ({
    mutate: createMutateMock,
    isPending: false,
    error: null,
  }),
  useUpdateUser: () => ({
    mutate: vi.fn(),
    isPending: false,
    error: null,
  }),
}));

describe("UserForm date of birth", () => {
  beforeEach(() => {
    createMutateMock.mockClear();
  });

  it("không gửi yêu cầu tạo tài khoản khi ngày sinh là hôm nay", () => {
    render(<UserForm />);

    fireEvent.change(screen.getByLabelText("Số điện thoại"), {
      target: { value: "0900000001" },
    });
    fireEvent.change(screen.getByLabelText("Họ và tên"), {
      target: { value: "Nguyễn Văn A" },
    });
    fireEvent.change(screen.getByLabelText(/Ngày sinh/), {
      target: { value: localIsoDate(new Date()) },
    });
    fireEvent.click(screen.getByRole("button", { name: "Tạo tài khoản" }));

    expect(screen.getByRole("alert")).toHaveTextContent("Người dùng phải đủ 18 tuổi.");
    expect(createMutateMock).not.toHaveBeenCalled();
  });
});

function localIsoDate(date: Date): string {
  const month = `${date.getMonth() + 1}`.padStart(2, "0");
  const day = `${date.getDate()}`.padStart(2, "0");
  return `${date.getFullYear()}-${month}-${day}`;
}
