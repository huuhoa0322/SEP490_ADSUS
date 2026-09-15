import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { describe, expect, it, vi, beforeEach } from "vitest";

import type { AddRelativeRequest } from "@/features/appointment-scheduling/types/relatives.types";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";
import { AddRelativeModal } from "@/features/appointment-scheduling/components/add-relative-modal";

const { toastErrorMock, toastSuccessMock } = vi.hoisted(() => ({
  toastErrorMock: vi.fn(),
  toastSuccessMock: vi.fn(),
}));

vi.mock("react-hot-toast", () => ({
  default: {
    error: toastErrorMock,
    success: toastSuccessMock,
  },
}));

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });
  function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  }
  return Wrapper;
}

describe("AddRelativeModal", () => {
  const defaultProps = {
    guardianUserId: "guardian-user-123",
    guardianName: "Nguyễn Thị Mẹ",
    open: true,
    onOpenChange: vi.fn(),
    onSuccess: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("render modal với đầy đủ các trường nhập liệu khi open = true", () => {
    render(<AddRelativeModal {...defaultProps} />, { wrapper: createWrapper() });

    expect(screen.getByText("Thêm người thân cho bệnh nhân")).toBeInTheDocument();
    expect(screen.getByText(/Nguyễn Thị Mẹ/)).toBeInTheDocument();
    expect(screen.getByLabelText(/Họ tên người thân/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Quan hệ với bệnh nhân/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Số điện thoại/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Ngày sinh/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Lưu người thân/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Hủy/i })).toBeInTheDocument();
  });

  it("chọn nhanh mối quan hệ phổ biến (Con, Vợ, Chồng...)", () => {
    render(<AddRelativeModal {...defaultProps} />, { wrapper: createWrapper() });

    const conButton = screen.getByRole("button", { name: "Con" });
    fireEvent.click(conButton);

    const relInput = screen.getByPlaceholderText(/Hoặc tự nhập/i) as HTMLInputElement;
    expect(relInput.value).toBe("Con");
  });

  it("kiểm tra số điện thoại thành công khi SĐT hợp lệ", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/relatives/check-phone`, () => {
        return HttpResponse.json({
          phone: "0912345678",
          isRegistered: false,
        });
      })
    );

    render(<AddRelativeModal {...defaultProps} />, { wrapper: createWrapper() });

    const phoneInput = screen.getByPlaceholderText("0912345678");
    fireEvent.change(phoneInput, { target: { value: "0912345678" } });

    const checkPhoneBtn = screen.getByRole("button", { name: /Kiểm tra SĐT/i });
    expect(checkPhoneBtn).not.toBeDisabled();
    fireEvent.click(checkPhoneBtn);

    await waitFor(() => {
      expect(
        screen.getByText(/Số điện thoại hợp lệ và chưa đăng ký tài khoản/i)
      ).toBeInTheDocument();
    });
  });

  it("submit thành công và gọi onSuccess callback", async () => {
    server.use(
      http.post(
        `${API_BASE_URL}/api/v1/relatives/guardian/guardian-user-123`,
        async ({ request }) => {
          const body = (await request.json()) as AddRelativeRequest;
          return HttpResponse.json({
            success: true,
            data: {
              relationshipId: "rel-new-1",
              patientProfileId: "profile-child-1",
              patientName: body.fullName,
              patientPhone: body.phone ?? null,
              dateOfBirth: body.dateOfBirth ?? null,
              gender: null,
              relationshipName: body.relationshipName ?? null,
              isRegisteredAccount: false,
              createdAt: "2026-09-15T00:00:00Z",
            },
          });
        }
      )
    );

    const onSuccessMock = vi.fn();
    const onOpenChangeMock = vi.fn();

    render(
      <AddRelativeModal
        {...defaultProps}
        onSuccess={onSuccessMock}
        onOpenChange={onOpenChangeMock}
      />,
      { wrapper: createWrapper() }
    );

    fireEvent.change(screen.getByLabelText(/Họ tên người thân/i), {
      target: { value: "Nguyễn Văn Con" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Con" }));

    const submitBtn = screen.getByRole("button", { name: /Lưu người thân/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(toastSuccessMock).toHaveBeenCalledWith(
        expect.stringContaining("Đã thêm người thân \"Nguyễn Văn Con\" thành công.")
      );
      expect(onOpenChangeMock).toHaveBeenCalledWith(false);
      expect(onSuccessMock).toHaveBeenCalledWith(
        expect.objectContaining({
          relationshipId: "rel-new-1",
          patientName: "Nguyễn Văn Con",
        })
      );
    });
  });
});
