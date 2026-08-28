import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { UserList } from "@/features/user-role-management/components/user-list";
import type { UserAccount } from "@/features/user-role-management/types/user.types";

const {
  useUserListMock,
  deactivateMutateMock,
  reactivateMutateMock,
  resetPasswordMutateMock,
} = vi.hoisted(() => ({
  useUserListMock: vi.fn(),
  deactivateMutateMock: vi.fn(),
  reactivateMutateMock: vi.fn(),
  resetPasswordMutateMock: vi.fn(),
}));

vi.mock("@/features/user-role-management/hooks/use-users", () => ({
  useUserList: () => useUserListMock(),
  useDeactivateUser: () => ({ mutate: deactivateMutateMock, isPending: false, error: null }),
  useReactivateUser: () => ({ mutate: reactivateMutateMock, isPending: false, error: null }),
  useResetUserPassword: () => ({
    mutate: resetPasswordMutateMock,
    isPending: false,
    error: null,
  }),
}));

function buildUser(overrides: Partial<UserAccount>): UserAccount {
  return {
    userId: "u1",
    phoneNumber: "0900000001",
    fullName: "Nguyễn Văn A",
    email: null,
    role: "DOCTOR",
    status: "ACTIVE",
    dateOfBirth: null,
    mustChangePassword: false,
    createdAt: "2026-01-01T00:00:00Z",
    isCurrentUser: false,
    ...overrides,
  };
}

function mockList(items: UserAccount[]) {
  useUserListMock.mockReturnValue({
    data: { items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1 },
    isLoading: false,
    isError: false,
    error: null,
  });
}

describe("UserList — thao tác theo dòng (UC-04 SCR-06)", () => {
  beforeEach(() => {
    deactivateMutateMock.mockClear();
    reactivateMutateMock.mockClear();
    resetPasswordMutateMock.mockClear();
  });

  it("ẩn nút vô hiệu hoá và cấp lại mật khẩu trên dòng của chính Admin đang đăng nhập", () => {
    mockList([buildUser({ isCurrentUser: true })]);

    render(<UserList />);

    expect(screen.getByText("Bạn")).toBeInTheDocument();
    expect(screen.queryByTitle("Vô hiệu hoá vĩnh viễn")).not.toBeInTheDocument();
    expect(screen.queryByTitle("Cấp lại mật khẩu")).not.toBeInTheDocument();
    // Vẫn được sửa tên/email của chính mình.
    expect(screen.getByTitle("Sửa thông tin và phân quyền")).toBeInTheDocument();
  });

  it("hiện nút khôi phục thay vì vô hiệu hoá/cấp lại mật khẩu khi tài khoản đã bị vô hiệu hoá", () => {
    mockList([buildUser({ status: "DEACTIVATED" })]);

    render(<UserList />);

    expect(screen.getByTitle("Khôi phục tài khoản")).toBeInTheDocument();
    expect(screen.queryByTitle("Vô hiệu hoá vĩnh viễn")).not.toBeInTheDocument();
    expect(screen.queryByTitle("Cấp lại mật khẩu")).not.toBeInTheDocument();
  });

  it("hiện mật khẩu tạm đúng một lần trên màn hình sau khi cấp lại thành công, không qua email", () => {
    resetPasswordMutateMock.mockImplementation((_userId, options) => {
      options?.onSuccess?.("Aa1b2C3d4E5f");
      options?.onSettled?.();
    });
    mockList([buildUser({ fullName: "Trần Thị B" })]);

    render(<UserList />);

    fireEvent.click(screen.getByTitle("Cấp lại mật khẩu"));
    fireEvent.click(screen.getByRole("button", { name: "Cấp lại" }));

    expect(screen.getByText("Đã cấp lại mật khẩu cho Trần Thị B")).toBeInTheDocument();
    expect(screen.getByText("Aa1b2C3d4E5f")).toBeInTheDocument();
  });
});
