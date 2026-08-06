import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { PatientAccountForm } from "@/features/medical-record/components/patient-account-form";
import type { PatientAccountCreated } from "@/features/medical-record/types/medical-record.types";

const { pushMock, accountMutate, profileMutate } = vi.hoisted(() => ({
  pushMock: vi.fn(),
  accountMutate: vi.fn(),
  profileMutate: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: pushMock }),
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

vi.mock("@/features/medical-record/hooks/use-patient-profile", () => ({
  useCreatePatientProfile: () => ({
    mutate: profileMutate,
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
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

const createdProfile = {
  patientProfileId: "profile-9",
  patientUserId: "user-9",
  fullName: "Lê Thị Hoa",
  phone: "0981234567",
  dateOfBirth: null,
  gender: "FEMALE" as const,
  medicalHistory: null,
  allergies: null,
  createdBy: "nurse-1",
  createdAt: "2026-08-06T09:00:00Z",
  updatedAt: "2026-08-06T09:00:00Z",
};

async function fillAndSubmit(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText(/họ và tên/i), "Lê Thị Hoa");
  await user.type(screen.getByLabelText(/số điện thoại/i), "0981234567");
  await user.click(screen.getByRole("button", { name: /tạo bệnh nhân mới/i }));
}

describe("PatientAccountForm", () => {
  beforeEach(() => {
    pushMock.mockReset();
    accountMutate.mockReset();
    profileMutate.mockReset();
  });

  it("chặn gửi khi số điện thoại sai định dạng", async () => {
    const user = userEvent.setup();
    render(<PatientAccountForm />);

    await user.type(screen.getByLabelText(/họ và tên/i), "Lê Thị Hoa");
    await user.type(screen.getByLabelText(/số điện thoại/i), "123");
    await user.click(screen.getByRole("button", { name: /tạo bệnh nhân mới/i }));

    // Validate phía client trước để không đi một vòng lên server chỉ để nhận 400.
    expect(accountMutate).not.toHaveBeenCalled();
    expect(screen.getByRole("alert")).toHaveTextContent(/số điện thoại/i);
  });

  it("gửi null thay vì chuỗi rỗng cho email và ngày sinh bỏ trống", async () => {
    const user = userEvent.setup();
    render(<PatientAccountForm />);

    await fillAndSubmit(user);

    // Chuỗi rỗng sẽ bị validator của backend coi là email sai định dạng.
    expect(accountMutate).toHaveBeenCalledWith(
      { phoneNumber: "0981234567", fullName: "Lê Thị Hoa", dateOfBirth: null, email: null },
      expect.anything(),
    );
  });

  it("sau khi tạo tài khoản thành công thì tạo luôn hồ sơ nền với gender bỏ trống thành null", async () => {
    // Mô phỏng accountMutate thành công bằng cách tự gọi onSuccess được truyền vào.
    accountMutate.mockImplementation((_payload, options) => {
      options?.onSuccess?.(createdAccount);
    });

    const user = userEvent.setup();
    render(<PatientAccountForm />);

    await fillAndSubmit(user);

    // #17 — gender optional, khác #18. Không nhập gì ở Giới tính thì phải gửi null.
    expect(profileMutate).toHaveBeenCalledWith(
      { patientUserId: "user-9", gender: null, medicalHistory: null, allergies: null },
      expect.anything(),
    );
  });

  it("hiện mật khẩu tạm, chờ hồ sơ nền tạo xong rồi Tiếp tục mới điều hướng đúng nơi", async () => {
    accountMutate.mockImplementation((_payload, options) => {
      options?.onSuccess?.(createdAccount);
    });
    profileMutate.mockImplementation((_payload, options) => {
      options?.onSuccess?.(createdProfile);
    });

    const user = userEvent.setup();
    render(<PatientAccountForm />);

    await fillAndSubmit(user);

    // Mật khẩu hiện dưới dạng text, không phải ô nhập — "chỉ sinh không được sửa".
    expect(screen.getByText("Ab3xyz9pqr")).toBeInTheDocument();
    expect(screen.queryByRole("textbox", { name: /mật khẩu/i })).not.toBeInTheDocument();

    expect(pushMock).not.toHaveBeenCalled();
    await user.click(screen.getByRole("button", { name: /tiếp tục/i }));
    expect(pushMock).toHaveBeenCalledWith("/patients/profile-9");
  });
});
