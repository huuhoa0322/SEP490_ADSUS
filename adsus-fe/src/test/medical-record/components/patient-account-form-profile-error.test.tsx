import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { PatientAccountForm } from "@/features/medical-record/components/patient-account-form";
import type { PatientAccountCreated } from "@/features/medical-record/types/medical-record.types";

// File tách riêng (xem lưu ý trong brief) — module hồ sơ nền được mock TĨNH với isError: true
// ngay từ đầu, để tránh bẫy cache ESM của vi.doMock + import() động trong cùng file với
// vi.mock toàn cục (isError: false).
const { pushMock, accountMutate, profileMutate } = vi.hoisted(() => ({
  pushMock: vi.fn(),
  accountMutate: vi.fn(),
  profileMutate: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: pushMock }),
}));

vi.mock("@/features/medical-record/hooks/use-medical-dictionaries", () => ({
  useDiseases: () => ({ data: [], isLoading: false }),
  useAllergyTypes: () => ({ data: [], isLoading: false }),
}));

vi.mock("@/features/medical-record/hooks/use-patient-account", () => ({
  useCreatePatientAccount: () => ({
    mutate: accountMutate,
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
  }),
}));

// Mô phỏng lỗi tạo hồ sơ nền: isError: true ngay từ đầu file, KHÔNG dùng vi.doMock linh hoạt
// (xem lưu ý trong brief — vi.doMock + await import() động không đáng tin vì
// patient-account-form đã được import tĩnh và cache theo specifier).
vi.mock("@/features/medical-record/hooks/use-patient-profile", () => ({
  useCreatePatientProfile: () => ({
    mutate: profileMutate,
    isPending: false,
    isSuccess: false,
    isError: true,
    error: new Error("Network error"),
  }),
}));

const createdAccount: PatientAccountCreated = {
  userId: "user-9",
  fullName: "Lê Thị Hoa",
  phoneNumber: "0981234567",
  dateOfBirth: null,
  email: null,
  temporaryPassword: "Ab3xyz9pqr",
};

async function fillAndSubmit(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText(/họ và tên/i), "Lê Thị Hoa");
  await user.type(screen.getByLabelText(/số điện thoại/i), "0981234567");
  await user.click(screen.getByRole("button", { name: /tạo bệnh nhân mới/i }));
}

describe("PatientAccountForm — tạo hồ sơ nền thất bại sau khi tài khoản đã tạo xong", () => {
  beforeEach(() => {
    pushMock.mockReset();
    accountMutate.mockReset();
    profileMutate.mockReset();
  });

  it("vẫn hiện mật khẩu tạm kèm cảnh báo nếu tạo hồ sơ nền thất bại, Tiếp tục vẫn dùng được", async () => {
    accountMutate.mockImplementation((_payload, options) => {
      options?.onSuccess?.(createdAccount);
    });
    // profileMutate KHÔNG gọi onSuccess — mô phỏng lỗi bằng hook mock isError=true ở trên,
    // đúng với thực tế: mutate() được gọi nhưng không bao giờ thành công.

    const user = userEvent.setup();
    render(<PatientAccountForm />);

    await fillAndSubmit(user);

    // Tài khoản đã tạo xong (mật khẩu vẫn hiện — dữ liệu quý, chỉ hiện một lần), kèm cảnh
    // báo riêng cho phần hồ sơ nền, và Tiếp tục vẫn bấm được (không bị disabled vĩnh viễn).
    expect(screen.getByText("Ab3xyz9pqr")).toBeInTheDocument();
    expect(screen.getByRole("alert")).toHaveTextContent(/chưa tạo được hồ sơ nền/i);

    await user.click(screen.getByRole("button", { name: /tiếp tục/i }));
    expect(pushMock).toHaveBeenCalledWith("/patients");
  });
});
