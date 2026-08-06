import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthStore } from "@/store/auth-store";

import { PatientAccountActions } from "@/features/medical-record/components/patient-account-actions";

const { updateMutate, resetMutate } = vi.hoisted(() => ({
  updateMutate: vi.fn(),
  resetMutate: vi.fn(),
}));

vi.mock("@/features/medical-record/hooks/use-patient-account", () => ({
  useUpdatePatientAccountContact: () => ({
    mutate: updateMutate,
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
  }),
  useResetPatientAccountPassword: () => ({
    mutate: resetMutate,
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
  }),
}));

const props = {
  userId: "user-1",
  fullName: "Lê Thị Hoa",
  phone: "0978123456",
  dateOfBirth: "1984-03-12",
  email: "hoa@example.com",
};

function signInAs(role: "DOCTOR" | "NURSE") {
  useAuthStore.getState().signIn("token", {
    userId: "me",
    fullName: "Người dùng",
    email: null,
    role,
    mustChangePassword: false,
  });
}

describe("PatientAccountActions", () => {
  beforeEach(() => {
    updateMutate.mockReset();
    resetMutate.mockReset();
    useAuthStore.getState().signOut();
  });

  it("không render gì với Bác sĩ", () => {
    // UC-06 BR-03 — đây là ngoại lệ đầu tiên Điều dưỡng có quyền mà Bác sĩ không có. Ẩn hẳn,
    // không phải disable: bày ra nút chắc chắn trả 403 chỉ làm người dùng bối rối.
    signInAs("DOCTOR");

    const { container } = render(<PatientAccountActions {...props} />);

    expect(container).toBeEmptyDOMElement();
  });

  it("hiện hai nút với Điều dưỡng", () => {
    signInAs("NURSE");

    render(<PatientAccountActions {...props} />);

    expect(screen.getByRole("button", { name: /sửa thông tin tài khoản/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /cấp lại mật khẩu/i })).toBeInTheDocument();
  });

  it("hỏi xác nhận trước khi cấp lại mật khẩu", async () => {
    signInAs("NURSE");
    const user = userEvent.setup();

    render(<PatientAccountActions {...props} />);
    await user.click(screen.getByRole("button", { name: /cấp lại mật khẩu/i }));

    // Cấp lại mật khẩu làm mật khẩu cũ chết ngay. Bấm nhầm mà không hỏi lại là khoá bệnh
    // nhân ra ngoài ứng dụng cho tới khi họ đọc được email.
    expect(resetMutate).not.toHaveBeenCalled();
    expect(screen.getByText(/mật khẩu hiện tại sẽ không dùng được nữa/i)).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: /^xác nhận$/i }));
    expect(resetMutate).toHaveBeenCalled();
  });

  it("hiện thông báo đã gửi email khi tài khoản có email (backend trả null)", async () => {
    resetMutate.mockImplementation((_payload, options) => {
      options?.onSuccess?.(null);
    });

    signInAs("NURSE");
    const user = userEvent.setup();

    render(<PatientAccountActions {...props} />);
    await user.click(screen.getByRole("button", { name: /cấp lại mật khẩu/i }));
    await user.click(screen.getByRole("button", { name: /^xác nhận$/i }));

    expect(screen.getByText(/đã gửi mật khẩu tạm tới email/i)).toBeInTheDocument();
  });

  it("hiện mật khẩu tạm trực tiếp khi tài khoản không có email (backend trả chuỗi)", async () => {
    // Quyết định ghi đè 06/08/2026 — tài khoản không có email không còn báo lỗi chặn nữa.
    resetMutate.mockImplementation((_payload, options) => {
      options?.onSuccess?.("Xk4mnpq8rt2Z");
    });

    signInAs("NURSE");
    const user = userEvent.setup();

    render(<PatientAccountActions {...props} />);
    await user.click(screen.getByRole("button", { name: /cấp lại mật khẩu/i }));
    await user.click(screen.getByRole("button", { name: /^xác nhận$/i }));

    expect(screen.getByText("Xk4mnpq8rt2Z")).toBeInTheDocument();
    expect(screen.queryByText(/đã gửi mật khẩu tạm tới email/i)).not.toBeInTheDocument();
  });

  it("gửi đủ bốn trường liên hệ khi lưu", async () => {
    signInAs("NURSE");
    const user = userEvent.setup();

    render(<PatientAccountActions {...props} />);
    await user.click(screen.getByRole("button", { name: /sửa thông tin tài khoản/i }));
    await user.click(screen.getByRole("button", { name: /^lưu$/i }));

    // BR-04 — đúng 4 trường, không role, không status.
    expect(updateMutate).toHaveBeenCalledWith(
      {
        fullName: "Lê Thị Hoa",
        phoneNumber: "0978123456",
        dateOfBirth: "1984-03-12",
        email: "hoa@example.com",
      },
      expect.anything(),
    );
  });
});
