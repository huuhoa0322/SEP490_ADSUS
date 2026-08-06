import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { PatientProfileForm } from "@/features/medical-record/components/patient-profile-form";

// Chế độ sửa gắn thêm <PatientAccountActions> (Task C11) — component đó dùng useMutation
// nên cần QueryClientProvider, dù các test ở đây không đụng tới khối tài khoản.
function renderWithClient(ui: ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
}));

const { profileMock, updateMutate, createMutate } = vi.hoisted(() => ({
  profileMock: vi.fn(),
  updateMutate: vi.fn(),
  createMutate: vi.fn(),
}));

vi.mock("@/features/medical-record/hooks/use-patient-profile", () => ({
  usePatientProfile: () => profileMock(),
  useUpdatePatientProfile: () => ({
    mutate: updateMutate,
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
  }),
  useCreatePatientProfile: () => ({
    mutate: createMutate,
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
  }),
}));

const profile = {
  patientProfileId: "profile-1",
  patientUserId: "user-1",
  fullName: "Lê Thị Hoa",
  phone: "0978123456",
  dateOfBirth: "1984-03-12",
  gender: "FEMALE" as const,
  medicalHistory: "Đã từng có u lành tính",
  allergies: "Penicillin",
  createdBy: "nurse-1",
  createdAt: "2026-08-04T09:00:00Z",
  updatedAt: "2026-08-04T09:00:00Z",
};

describe("PatientProfileForm — chế độ sửa", () => {
  beforeEach(() => {
    updateMutate.mockReset();
    createMutate.mockReset();
    profileMock.mockReturnValue({ data: profile, isLoading: false, isError: false, error: null });
  });

  it("hiện họ tên, ngày sinh, SĐT dưới dạng chỉ đọc chứ không phải ô nhập", () => {
    renderWithClient(<PatientProfileForm mode="edit" profileId="profile-1" />);

    // UC-06 bước 2 — ba trường này lấy từ bảng users, #18 không nhận chúng. Cho sửa ở đây
    // là hứa một điều hệ thống không làm được.
    expect(screen.getByText("Lê Thị Hoa")).toBeInTheDocument();
    expect(screen.queryByLabelText(/họ và tên/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/số điện thoại/i)).not.toBeInTheDocument();
  });

  it("gửi đúng ba trường của #18 khi lưu", async () => {
    const user = userEvent.setup();
    renderWithClient(<PatientProfileForm mode="edit" profileId="profile-1" />);

    await user.click(screen.getByRole("button", { name: /lưu/i }));

    // mutate() nhận đối số thứ hai { onSuccess } để điều hướng sau khi lưu — dùng
    // expect.anything() cho đối số đó, test này chỉ quan tâm payload gửi lên đúng chưa.
    expect(updateMutate).toHaveBeenCalledWith(
      {
        gender: "FEMALE",
        medicalHistory: "Đã từng có u lành tính",
        allergies: "Penicillin",
      },
      expect.anything(),
    );
  });

  it("chặn lưu khi bỏ trống giới tính", async () => {
    const user = userEvent.setup();
    renderWithClient(<PatientProfileForm mode="edit" profileId="profile-1" />);

    // #18 là thay TOÀN BỘ hồ sơ nên gender bắt buộc — khác #17 vốn cho bỏ trống.
    await user.selectOptions(screen.getByLabelText(/giới tính/i), "");
    await user.click(screen.getByRole("button", { name: /lưu/i }));

    expect(updateMutate).not.toHaveBeenCalled();
    expect(screen.getByRole("alert")).toHaveTextContent(/giới tính/i);
  });
});

describe("PatientProfileForm — chế độ tạo", () => {
  beforeEach(() => {
    createMutate.mockReset();
    profileMock.mockReturnValue({ data: undefined, isLoading: false, isError: false, error: null });
  });

  it("cho phép lưu khi bỏ trống giới tính", async () => {
    const user = userEvent.setup();
    render(
      <PatientProfileForm
        mode="create"
        patientUserId="user-9"
        identity={{ fullName: "Phạm Hồng Hạnh", phone: "0912345678", dateOfBirth: null }}
      />,
    );

    await user.click(screen.getByRole("button", { name: /tạo hồ sơ nền/i }));

    // #17 cho bỏ trống gender (DB có default) — dùng chung validator với #18 là sai.
    // mutate() cũng nhận đối số thứ hai { onSuccess } — xem chú thích ở test "chế độ sửa".
    expect(createMutate).toHaveBeenCalledWith(
      {
        patientUserId: "user-9",
        gender: null,
        medicalHistory: null,
        allergies: null,
      },
      expect.anything(),
    );
  });
});
