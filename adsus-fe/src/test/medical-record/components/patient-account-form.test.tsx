import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { PatientAccountForm } from "@/features/medical-record/components/patient-account-form";

const { createMutate } = vi.hoisted(() => ({ createMutate: vi.fn() }));

vi.mock("@/features/medical-record/hooks/use-patient-account", () => ({
  useCreatePatientAccount: () => ({
    mutate: createMutate,
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
  }),
}));

describe("PatientAccountForm", () => {
  beforeEach(() => createMutate.mockReset());

  it("chặn gửi khi số điện thoại sai định dạng", async () => {
    const user = userEvent.setup();
    render(<PatientAccountForm onCreated={vi.fn()} />);

    await user.type(screen.getByLabelText(/họ và tên/i), "Lê Thị Hoa");
    await user.type(screen.getByLabelText(/số điện thoại/i), "123");
    await user.click(screen.getByRole("button", { name: /tạo tài khoản/i }));

    // Validate phía client trước để không đi một vòng lên server chỉ để nhận 400.
    expect(createMutate).not.toHaveBeenCalled();
    expect(screen.getByRole("alert")).toHaveTextContent(/số điện thoại/i);
  });

  it("gửi null thay vì chuỗi rỗng cho email và ngày sinh bỏ trống", async () => {
    const user = userEvent.setup();
    render(<PatientAccountForm onCreated={vi.fn()} />);

    await user.type(screen.getByLabelText(/họ và tên/i), "Lê Thị Hoa");
    await user.type(screen.getByLabelText(/số điện thoại/i), "0981234567");
    await user.click(screen.getByRole("button", { name: /tạo tài khoản/i }));

    // Chuỗi rỗng sẽ bị validator của backend coi là email sai định dạng.
    expect(createMutate).toHaveBeenCalledWith(
      { phoneNumber: "0981234567", fullName: "Lê Thị Hoa", dateOfBirth: null, email: null },
      expect.anything(),
    );
  });

  it("nhắc rằng mật khẩu tạm chỉ gửi qua email", () => {
    render(<PatientAccountForm onCreated={vi.fn()} />);

    // UC-06 BR-05 — Điều dưỡng không bao giờ thấy mật khẩu. Nói rõ ngay trên form để họ
    // không đi tìm nó sau khi bấm tạo.
    expect(screen.getByText(/mật khẩu tạm/i)).toBeInTheDocument();
  });
});
