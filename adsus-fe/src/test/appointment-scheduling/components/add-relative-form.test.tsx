import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { describe, expect, it, vi, beforeEach } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";
import { AddRelativeForm } from "@/features/appointment-scheduling/components/add-relative-form";

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

describe("AddRelativeForm", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("render giao diện với đầy đủ các trường nhập liệu", () => {
    render(<AddRelativeForm />, { wrapper: createWrapper() });

    expect(screen.getByText("Thêm người thân mới")).toBeInTheDocument();
    expect(screen.getByLabelText(/Họ tên người thân/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Số điện thoại/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Ngày sinh/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Nhãn quan hệ/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Lưu người thân/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Kiểm tra SĐT/i })).toBeDisabled();
  });

  it("validation: báo lỗi khi submit với họ tên rỗng", async () => {
    render(<AddRelativeForm />, { wrapper: createWrapper() });

    const submitBtn = screen.getByRole("button", { name: /Lưu người thân/i });
    expect(submitBtn).toBeDisabled();

    // Nhập toàn khoảng trắng
    const nameInput = screen.getByLabelText(/Họ tên người thân/i);
    fireEvent.change(nameInput, { target: { value: "   " } });

    // Submit form trực tiếp
    const form = nameInput.closest("form")!;
    fireEvent.submit(form);

    expect(toastErrorMock).toHaveBeenCalledWith("Vui lòng nhập họ và tên người thân.");
  });

  it("validation: báo lỗi khi SĐT sai định dạng", async () => {
    render(<AddRelativeForm />, { wrapper: createWrapper() });

    const nameInput = screen.getByLabelText(/Họ tên người thân/i);
    const phoneInput = screen.getByLabelText(/Số điện thoại/i);
    fireEvent.change(nameInput, { target: { value: "Nguyễn Văn A" } });
    fireEvent.change(phoneInput, { target: { value: "12345" } });

    const form = nameInput.closest("form")!;
    fireEvent.submit(form);

    expect(toastErrorMock).toHaveBeenCalledWith(
      "Số điện thoại không hợp lệ (phải bắt đầu bằng 0 và có 10 chữ số)."
    );
  });

  it("checkPhoneRegistered: kiểm tra SĐT không hợp lệ khi bấm nút kiểm tra SĐT", async () => {
    render(<AddRelativeForm />, { wrapper: createWrapper() });

    const phoneInput = screen.getByLabelText(/Số điện thoại/i);
    fireEvent.change(phoneInput, { target: { value: "9999999999" } });

    const checkBtn = screen.getByRole("button", { name: /Kiểm tra SĐT/i });
    expect(checkBtn).not.toBeDisabled();
    fireEvent.click(checkBtn);

    expect(toastErrorMock).toHaveBeenCalledWith(
      "Số điện thoại không hợp lệ (phải bắt đầu bằng 0 và có 10 chữ số)."
    );
  });

  it("checkPhoneRegistered: hiển thị cảnh báo đỏ khi SĐT đã đăng ký tài khoản", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/relatives/check-phone`, () =>
        HttpResponse.json({ phone: "0912345678", isRegistered: true })
      )
    );

    render(<AddRelativeForm />, { wrapper: createWrapper() });

    const phoneInput = screen.getByLabelText(/Số điện thoại/i);
    fireEvent.change(phoneInput, { target: { value: "0912345678" } });

    const checkBtn = screen.getByRole("button", { name: /Kiểm tra SĐT/i });
    fireEvent.click(checkBtn);

    await waitFor(() => {
      expect(
        screen.getByText(
          /Số điện thoại này đã được đăng ký tài khoản\. Người đó có thể tự đăng nhập và đặt lịch khám\./i
        )
      ).toBeInTheDocument();
    });
    expect(toastErrorMock).toHaveBeenCalledWith("Số điện thoại này đã được đăng ký tài khoản.");
  });

  it("checkPhoneRegistered: hiển thị banner xanh khi SĐT chưa đăng ký tài khoản", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/relatives/check-phone`, () =>
        HttpResponse.json({ phone: "0912345678", isRegistered: false })
      )
    );

    render(<AddRelativeForm />, { wrapper: createWrapper() });

    const phoneInput = screen.getByLabelText(/Số điện thoại/i);
    fireEvent.change(phoneInput, { target: { value: "0912345678" } });

    const checkBtn = screen.getByRole("button", { name: /Kiểm tra SĐT/i });
    fireEvent.click(checkBtn);

    await waitFor(() => {
      expect(
        screen.getByText(/Số điện thoại hợp lệ và chưa đăng ký tài khoản trong hệ thống\./i)
      ).toBeInTheDocument();
    });
    expect(toastSuccessMock).toHaveBeenCalledWith(
      "Số điện thoại hợp lệ (chưa đăng ký tài khoản)."
    );

    // Thay đổi SĐT -> banner biến mất
    fireEvent.change(phoneInput, { target: { value: "0987654321" } });
    expect(
      screen.queryByText(/Số điện thoại hợp lệ và chưa đăng ký tài khoản trong hệ thống\./i)
    ).not.toBeInTheDocument();
  });

  it("submit tạo người thân thành công: gửi payload chuẩn, toast success và gọi onSuccess callback", async () => {
    let capturedBody: unknown = null;
    server.use(
      http.post(`${API_BASE_URL}/api/v1/relatives`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json(
          {
            relationshipId: "rel-new-1",
            patientProfileId: "prof-new-1",
            patientName: "Nguyễn Thị Mẹ",
            patientPhone: "0912345678",
            dateOfBirth: "1965-05-15",
            gender: null,
            relationshipName: "Mẹ",
            isRegisteredAccount: false,
            createdAt: "2026-09-14T00:00:00Z",
          },
          { status: 201 }
        );
      })
    );

    const onSuccessMock = vi.fn();
    render(<AddRelativeForm onSuccess={onSuccessMock} />, { wrapper: createWrapper() });

    const nameInput = screen.getByLabelText(/Họ tên người thân/i);
    const phoneInput = screen.getByLabelText(/Số điện thoại/i);
    const dobInput = screen.getByLabelText(/Ngày sinh/i);
    const relationInput = screen.getByLabelText(/Nhãn quan hệ/i);

    fireEvent.change(nameInput, { target: { value: "Nguyễn Thị Mẹ" } });
    fireEvent.change(phoneInput, { target: { value: "0912345678" } });
    fireEvent.change(dobInput, { target: { value: "1965-05-15" } });
    fireEvent.change(relationInput, { target: { value: "Mẹ" } });

    const submitBtn = screen.getByRole("button", { name: /Lưu người thân/i });
    expect(submitBtn).not.toBeDisabled();
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(toastSuccessMock).toHaveBeenCalledWith("Đã thêm người thân thành công.");
    });

    expect(capturedBody).toEqual({
      fullName: "Nguyễn Thị Mẹ",
      phone: "0912345678",
      dateOfBirth: "1965-05-15",
      relationshipName: "Mẹ",
    });

    expect(onSuccessMock).toHaveBeenCalledTimes(1);

    // Form đã reset
    expect(nameInput).toHaveValue("");
    expect(phoneInput).toHaveValue("");
    expect(dobInput).toHaveValue("");
    expect(relationInput).toHaveValue("");
  });

  it("thông báo lỗi khi API lưu người thân trả lỗi", async () => {
    server.use(
      http.post(`${API_BASE_URL}/api/v1/relatives`, () =>
        HttpResponse.json({ message: "Người thân này đã tồn tại trong danh sách." }, { status: 400 })
      )
    );

    const onSuccessMock = vi.fn();
    render(<AddRelativeForm onSuccess={onSuccessMock} />, { wrapper: createWrapper() });

    const nameInput = screen.getByLabelText(/Họ tên người thân/i);
    fireEvent.change(nameInput, { target: { value: "Nguyễn Thị Mẹ" } });

    const submitBtn = screen.getByRole("button", { name: /Lưu người thân/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(toastErrorMock).toHaveBeenCalledWith("Người thân này đã tồn tại trong danh sách.");
    });
    expect(onSuccessMock).not.toHaveBeenCalled();
  });
});
