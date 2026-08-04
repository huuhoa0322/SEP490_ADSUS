import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { UserList } from "./user-list";

const { replaceMock } = vi.hoisted(() => ({
  replaceMock: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: replaceMock }),
}));

vi.mock("../hooks/use-users", () => {
  const mutation = () => ({
    error: null,
    isPending: false,
    mutate: vi.fn(),
  });

  return {
    useUserList: () => ({
      data: {
        items: [],
        page: 1,
        pageSize: 20,
        totalCount: 0,
        totalPages: 0,
      },
      isLoading: false,
      isError: false,
      error: null,
    }),
    useSetUserLocked: mutation,
    useDeactivateUser: mutation,
    useResetUserPassword: mutation,
  };
});

describe("UserList create notice", () => {
  beforeEach(() => {
    replaceMock.mockClear();
  });

  it("hiện cảnh báo API sau khi chuyển trang và dọn nó khỏi URL", () => {
    const notice =
      "Đã tạo tài khoản, nhưng không gửi được email chứa mật khẩu tạm.";
    const { rerender } = render(<UserList initialCreateNotice={notice} />);

    expect(screen.getByRole("status")).toHaveTextContent(notice);
    expect(replaceMock).toHaveBeenCalledWith("/admin/users", { scroll: false });

    // Điều hướng replace chỉ dọn URL; cảnh báo vẫn phải đủ lâu để người dùng đọc.
    rerender(<UserList />);
    expect(screen.getByRole("status")).toHaveTextContent(notice);
  });
});
