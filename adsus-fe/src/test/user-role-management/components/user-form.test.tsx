import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { UserForm } from "@/features/user-role-management/components/user-form";

const { createMutateMock, updateMutateMock } = vi.hoisted(() => ({
  createMutateMock: vi.fn(),
  updateMutateMock: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock("@/features/user-role-management/hooks/use-users", () => ({
  useUserDetail: (userId?: string) => ({
    data: userId
      ? {
          userId,
          phoneNumber: "0936904406",
          fullName: "STC001 Patient",
          role: "PATIENT",
          email: "patient@example.com",
          status: "ACTIVE",
          dateOfBirth: null,
          mustChangePassword: false,
          createdAt: "2026-09-01T00:00:00Z",
          isCurrentUser: false,
        }
      : undefined,
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
    mutate: updateMutateMock,
    isPending: false,
    error: null,
  }),
}));

describe("UserForm date of birth", () => {
  beforeEach(() => {
    createMutateMock.mockClear();
    updateMutateMock.mockClear();
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
      target: { value: localDateFormat(new Date()) },
    });
    fireEvent.click(screen.getByRole("button", { name: "Tạo tài khoản" }));

    expect(screen.getByRole("alert")).toHaveTextContent("Người dùng phải đủ 18 tuổi.");
    expect(createMutateMock).not.toHaveBeenCalled();
  });

  it("cho phép chọn vai trò Dược sĩ (PHARMACIST) và gửi yêu cầu tạo tài khoản thành công", () => {
    render(<UserForm />);

    fireEvent.change(screen.getByLabelText("Số điện thoại"), {
      target: { value: "0900000088" },
    });
    fireEvent.change(screen.getByLabelText("Họ và tên"), {
      target: { value: "Dược Sĩ Minh" },
    });
    fireEvent.change(screen.getByLabelText("Vai trò"), {
      target: { value: "PHARMACIST" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Tạo tài khoản" }));

    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(createMutateMock).toHaveBeenCalledWith(
      expect.objectContaining({
        phoneNumber: "0900000088",
        fullName: "Dược Sĩ Minh",
        role: "PHARMACIST",
      }),
      expect.anything(),
    );
  });

  it("loại bỏ ô vai trò ở chế độ sửa và giữ nguyên vai trò ban đầu khi submit", () => {
    render(<UserForm userId="user-123" />);

    // Kiểm tra ô Vai trò đã được loại bỏ hoàn toàn khỏi form sửa
    expect(screen.queryByLabelText("Vai trò")).toBeNull();
    expect(screen.queryByRole("combobox", { name: "Vai trò" })).toBeNull();

    fireEvent.change(screen.getByLabelText("Họ và tên"), {
      target: { value: "STC001 Patient Đã Sửa" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Lưu thay đổi" }));

    expect(updateMutateMock).toHaveBeenCalledWith(
      expect.objectContaining({
        fullName: "STC001 Patient Đã Sửa",
        role: "PATIENT",
      }),
      expect.anything(),
    );
  });
});

function localDateFormat(date: Date): string {
  const month = `${date.getMonth() + 1}`.padStart(2, "0");
  const day = `${date.getDate()}`.padStart(2, "0");
  return `${day}/${month}/${date.getFullYear()}`;
}
