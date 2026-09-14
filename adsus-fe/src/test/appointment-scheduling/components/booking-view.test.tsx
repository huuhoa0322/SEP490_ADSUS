import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { describe, expect, it, vi, beforeEach, beforeAll } from "vitest";
import { format, addDays, startOfDay } from "date-fns";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";
import { useAuthStore } from "@/store/auth-store";
import { BookingView } from "@/features/appointment-scheduling/components/booking-view";
import type { BookAppointmentRequest } from "@/features/appointment-scheduling/types/booking.types";

const { toastErrorMock, toastSuccessMock, routerPushMock } = vi.hoisted(() => ({
  toastErrorMock: vi.fn(),
  toastSuccessMock: vi.fn(),
  routerPushMock: vi.fn(),
}));

vi.mock("react-hot-toast", () => ({
  default: {
    error: toastErrorMock,
    success: toastSuccessMock,
  },
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({
    push: routerPushMock,
    replace: vi.fn(),
  }),
}));

beforeAll(() => {
  // Polyfill pointer & layout methods for Radix UI in jsdom
  Element.prototype.hasPointerCapture = vi.fn();
  Element.prototype.setPointerCapture = vi.fn();
  Element.prototype.releasePointerCapture = vi.fn();
  Element.prototype.scrollIntoView = vi.fn();
});

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

describe("BookingView", () => {
  const today = startOfDay(new Date());
  const tomorrow = addDays(today, 1);
  const tomorrowStr = format(tomorrow, "yyyy-MM-dd");

  const mockSlots = [
    {
      slotId: "slot-1",
      doctorId: "doc-1",
      doctorName: "Nguyễn Văn Nam",
      doctorStatus: "ACTIVE",
      doctorGender: "MALE",
      slotDate: tomorrowStr,
      startTime: "09:00:00",
      endTime: "09:30:00",
      createdAt: "2026-09-01T00:00:00Z",
    },
    {
      slotId: "slot-2",
      doctorId: "doc-2",
      doctorName: "Trần Thị Nữ",
      doctorStatus: "ACTIVE",
      doctorGender: "FEMALE",
      slotDate: tomorrowStr,
      startTime: "10:00:00",
      endTime: "10:30:00",
      createdAt: "2026-09-01T00:00:00Z",
    },
  ];

  const mockRelatives = [
    {
      relationshipId: "rel-1",
      patientProfileId: "prof-1",
      patientName: "Nguyễn Thị Mẹ",
      patientPhone: "0912345678",
      dateOfBirth: "1960-01-01",
      gender: "FEMALE",
      relationshipName: "Mẹ",
      isRegisteredAccount: false,
      createdAt: "2026-09-01T00:00:00Z",
    },
  ];

  const mockCategories = [
    {
      categoryId: "cat-pain",
      name: "Đau tức",
      isOther: false,
      symptoms: [
        { symptomId: "sym-1", name: "Đau hạ vị", isOther: false },
        { symptomId: "sym-2", name: "Đau lưng", isOther: false },
      ],
    },
    {
      categoryId: "cat-other",
      name: "Khác",
      isOther: true,
      symptoms: [],
    },
  ];

  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.setState({
      accessToken: "patient-token",
      user: {
        userId: "patient-1",
        email: "patient@test.com",
        fullName: "Bệnh Nhân Test",
        role: "PATIENT",
        mustChangePassword: false,
      },
    });

    server.use(
      http.get(`${API_BASE_URL}/api/v1/appointments/slots`, () =>
        HttpResponse.json({
          code: 200,
          message: "OK",
          data: mockSlots,
        })
      ),
      http.get(`${API_BASE_URL}/api/v1/relatives`, () =>
        HttpResponse.json({
          relatives: mockRelatives,
        })
      ),
      http.get(`${API_BASE_URL}/api/v1/symptoms/categories`, () =>
        HttpResponse.json({
          code: 200,
          message: "OK",
          data: mockCategories,
        })
      )
    );
  });

  it("render đầy đủ các thành phần chính của giao diện đặt lịch", async () => {
    render(<BookingView />, { wrapper: createWrapper() });

    expect(screen.getByText("Đặt lịch khám trực tuyến")).toBeInTheDocument();
    expect(screen.getByText(/Giới tính bác sĩ \(tùy chọn\)/i)).toBeInTheDocument();
    expect(screen.getByText(/Bác sĩ phụ trách/i)).toBeInTheDocument();
    expect(screen.getByText(/Chọn tuần khám/i)).toBeInTheDocument();
    expect(screen.getByText(/Khung giờ khả dụng/i)).toBeInTheDocument();
    expect(screen.getByText(/Lý do khám \(tùy chọn\)/i)).toBeInTheDocument();
    expect(screen.getByText(/Triệu chứng \(tùy chọn\)/i)).toBeInTheDocument();
    expect(screen.getByText(/Đặt lịch cho/i)).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: /Xác nhận đặt lịch khám/i })
    ).toBeInTheDocument();

    // Ban đầu chưa chọn slot -> nút submit bị disabled
    expect(
      screen.getByRole("button", { name: /Xác nhận đặt lịch khám/i })
    ).toBeDisabled();
  });

  it("guard banner: hiển thị thông báo yêu cầu chọn bác sĩ khi chưa chọn bác sĩ", async () => {
    render(<BookingView />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(
        screen.getByText("Vui lòng chọn bác sĩ để xem các khung giờ.")
      ).toBeInTheDocument();
    });
  });

  it("guard banner: hiển thị thông báo yêu cầu chọn ngày khi đã chọn bác sĩ nhưng chưa chọn ngày", async () => {
    render(<BookingView />, { wrapper: createWrapper() });

    await waitFor(() => expect(screen.getByText("Tất cả")).toBeInTheDocument());

    const doctorTrigger = screen.getByRole("combobox");
    fireEvent.click(doctorTrigger);
    const docItem = await screen.findByText(/BS\. Trần Thị Nữ/i);
    fireEvent.click(docItem);

    await waitFor(() => {
      expect(
        screen.getByText("Vui lòng chọn ngày khám để xem các khung giờ.")
      ).toBeInTheDocument();
    });
  });

  it("chuyển đổi giữa Tôi và Người thân", async () => {
    render(<BookingView />, { wrapper: createWrapper() });

    const selfBtn = screen.getByRole("button", { name: /Tôi \(Bản thân\)/i });
    const relativeBtn = screen.getByRole("button", { name: /Người thân/i });

    expect(selfBtn).toBeInTheDocument();
    expect(relativeBtn).toBeInTheDocument();

    // Mặc định là Cho Tôi -> Không hiển thị dropdown người thân
    expect(screen.queryByText(/-- Chọn người thân --/i)).not.toBeInTheDocument();

    // Bấm chọn Người thân
    fireEvent.click(relativeBtn);

    // Đợi query người thân hoàn tất và dropdown xuất hiện
    await waitFor(() => {
      expect(screen.getByText("-- Chọn người thân --")).toBeInTheDocument();
    });

    // Bấm lại Cho Tôi -> dropdown biến mất
    fireEvent.click(selfBtn);
    expect(screen.queryByText(/-- Chọn người thân --/i)).not.toBeInTheDocument();
  });

  it("người thân trống: hiển thị banner hướng dẫn và nút Thêm người thân", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/relatives`, () =>
        HttpResponse.json({ relatives: [] })
      )
    );

    const onSwitchMock = vi.fn();
    render(<BookingView onSwitchToAddRelative={onSwitchMock} />, {
      wrapper: createWrapper(),
    });

    const relativeBtn = screen.getByRole("button", { name: /Người thân/i });
    fireEvent.click(relativeBtn);

    await waitFor(() => {
      expect(
        screen.getByText(/Bạn chưa có thông tin người thân nào trong danh sách/i)
      ).toBeInTheDocument();
    });

    const addRelativeBtn = screen.getByRole("button", { name: /Thêm người thân ngay/i });
    fireEvent.click(addRelativeBtn);
    expect(onSwitchMock).toHaveBeenCalledTimes(1);
  });

  it("chặn submit khi chưa chọn người thân (chọn Người thân nhưng không chọn ai)", async () => {
    render(<BookingView />, { wrapper: createWrapper() });

    await waitFor(() => expect(screen.getByText("Tất cả")).toBeInTheDocument());

    // Chọn giới tính Nữ để lọc bác sĩ
    const femaleBtn = screen.getByRole("button", { name: "Nữ" });
    fireEvent.click(femaleBtn);

    // Mở select bác sĩ
    const doctorTrigger = screen.getByRole("combobox");
    fireEvent.click(doctorTrigger);

    const docItem = await screen.findByText(/BS\. Trần Thị Nữ/i);
    fireEvent.click(docItem);

    // Chọn ngày (tomorrow)
    const dayBtn = await screen.findByText(new RegExp(format(tomorrow, "dd/MM")));
    fireEvent.click(dayBtn);

    // Chọn slot
    const slotBtn = await screen.findByText(/10:00 - 10:30/i);
    fireEvent.click(slotBtn);

    // Nút xác nhận đặt lịch đã được enable
    const confirmBtn = screen.getByRole("button", { name: /Xác nhận đặt lịch khám/i });
    expect(confirmBtn).not.toBeDisabled();

    // Chuyển sang đặt cho Người thân
    const relativeRadio = screen.getByRole("button", { name: /Người thân/i });
    fireEvent.click(relativeRadio);

    // Bấm submit mà CHƯA chọn người thân trong dropdown
    fireEvent.click(confirmBtn);

    expect(toastErrorMock).toHaveBeenCalledWith(
      "Vui lòng chọn người thân trước khi xác nhận đặt lịch."
    );
  });

  it("đặt lịch cho bản thân: gửi relationshipId là undefined", async () => {
    let capturedBody: BookAppointmentRequest | null = null;
    server.use(
      http.post(`${API_BASE_URL}/api/v1/appointments`, async ({ request }) => {
        capturedBody = (await request.json()) as BookAppointmentRequest;
        return HttpResponse.json({
          code: 200,
          message: "OK",
          data: {
            appointmentId: "app-1",
            scheduleSlotId: "slot-2",
            slotDate: tomorrowStr,
            startTime: "10:00:00",
            endTime: "10:30:00",
            doctorName: "Trần Thị Nữ",
            status: "BOOKED",
            reason: "Khám định kỳ",
            cancellationReason: null,
            calendarSyncedAt: null,
            createdAt: "2026-09-14T00:00:00Z",
            caseId: null,
          },
        });
      })
    );

    render(<BookingView />, { wrapper: createWrapper() });

    await waitFor(() => expect(screen.getByText("Tất cả")).toBeInTheDocument());

    // Chọn bác sĩ
    const doctorTrigger = screen.getByRole("combobox");
    fireEvent.click(doctorTrigger);
    const docItem = await screen.findByText(/BS\. Trần Thị Nữ/i);
    fireEvent.click(docItem);

    // Chọn ngày
    const dayBtn = await screen.findByText(new RegExp(format(tomorrow, "dd/MM")));
    fireEvent.click(dayBtn);

    // Chọn slot
    const slotBtn = await screen.findByText(/10:00 - 10:30/i);
    fireEvent.click(slotBtn);

    // Điền lý do khám
    const reasonTextarea = screen.getByPlaceholderText(/Đau hạ vị 2 ngày nay/i);
    fireEvent.change(reasonTextarea, { target: { value: "Khám định kỳ" } });

    // Submit
    const confirmBtn = screen.getByRole("button", { name: /Xác nhận đặt lịch khám/i });
    fireEvent.click(confirmBtn);

    await waitFor(() => {
      expect(screen.getByText("Đặt lịch thành công!")).toBeInTheDocument();
    });

    expect(capturedBody).toEqual({
      scheduleSlotId: "slot-2",
      reason: "Khám định kỳ",
      symptoms: undefined,
      relationshipId: undefined,
    });

    // Bấm "Quay về trang chủ"
    const homeBtn = screen.getByRole("button", { name: /Quay về trang chủ/i });
    fireEvent.click(homeBtn);
    expect(routerPushMock).toHaveBeenCalledWith("/");
  });

  it("đặt lịch cho người thân: gửi relationshipId chuẩn và đóng dialog", async () => {
    let capturedBody: BookAppointmentRequest | null = null;
    server.use(
      http.post(`${API_BASE_URL}/api/v1/appointments`, async ({ request }) => {
        capturedBody = (await request.json()) as BookAppointmentRequest;
        return HttpResponse.json({
          code: 200,
          message: "OK",
          data: {
            appointmentId: "app-2",
            scheduleSlotId: "slot-2",
            slotDate: tomorrowStr,
            startTime: "10:00:00",
            endTime: "10:30:00",
            doctorName: "Trần Thị Nữ",
            status: "BOOKED",
            reason: null,
            cancellationReason: null,
            calendarSyncedAt: null,
            createdAt: "2026-09-14T00:00:00Z",
            caseId: null,
          },
        });
      })
    );

    render(<BookingView />, { wrapper: createWrapper() });

    await waitFor(() => expect(screen.getByText("Tất cả")).toBeInTheDocument());

    // Chọn bác sĩ
    const doctorTrigger = screen.getByRole("combobox");
    fireEvent.click(doctorTrigger);
    const docItem = await screen.findByText(/BS\. Trần Thị Nữ/i);
    fireEvent.click(docItem);

    // Chọn ngày
    const dayBtn = await screen.findByText(new RegExp(format(tomorrow, "dd/MM")));
    fireEvent.click(dayBtn);

    // Chọn slot
    const slotBtn = await screen.findByText(/10:00 - 10:30/i);
    fireEvent.click(slotBtn);

    // Chọn Người thân
    const relativeRadio = screen.getByRole("button", { name: /Người thân/i });
    fireEvent.click(relativeRadio);

    // Đợi dropdown xuất hiện
    await waitFor(() => {
      expect(screen.getByText("-- Chọn người thân --")).toBeInTheDocument();
    });

    // Mở dropdown chọn người thân
    const comboboxes = screen.getAllByRole("combobox");
    const relativeTrigger = comboboxes[1];
    fireEvent.click(relativeTrigger);

    const relativeItem = await screen.findByText(/Nguyễn Thị Mẹ/i);
    fireEvent.click(relativeItem);

    // Submit
    const confirmBtn = screen.getByRole("button", { name: /Xác nhận đặt lịch khám/i });
    fireEvent.click(confirmBtn);

    await waitFor(() => {
      expect(screen.getByText("Đặt lịch thành công!")).toBeInTheDocument();
    });

    expect(capturedBody).toEqual({
      scheduleSlotId: "slot-2",
      reason: undefined,
      symptoms: undefined,
      relationshipId: "rel-1",
    });
  });

  it("sanitize triệu chứng: loại bỏ block rỗng và chỉ gửi triệu chứng hợp lệ", async () => {
    let capturedBody: BookAppointmentRequest | null = null;
    server.use(
      http.post(`${API_BASE_URL}/api/v1/appointments`, async ({ request }) => {
        capturedBody = (await request.json()) as BookAppointmentRequest;
        return HttpResponse.json({
          code: 200,
          message: "OK",
          data: {
            appointmentId: "app-3",
            scheduleSlotId: "slot-1",
            slotDate: tomorrowStr,
            startTime: "09:00:00",
            endTime: "09:30:00",
            doctorName: "Nguyễn Văn Nam",
            status: "BOOKED",
            reason: null,
            cancellationReason: null,
            calendarSyncedAt: null,
            createdAt: "2026-09-14T00:00:00Z",
            caseId: null,
          },
        });
      })
    );

    render(<BookingView />, { wrapper: createWrapper() });

    await waitFor(() => expect(screen.getByText("Tất cả")).toBeInTheDocument());

    // Chọn bác sĩ Nam
    const doctorTrigger = screen.getByRole("combobox");
    fireEvent.click(doctorTrigger);
    const docItem = await screen.findByText(/BS\. Nguyễn Văn Nam/i);
    fireEvent.click(docItem);

    // Chọn ngày
    const dayBtn = await screen.findByText(new RegExp(format(tomorrow, "dd/MM")));
    fireEvent.click(dayBtn);

    // Chọn slot
    const slotBtn = await screen.findByText(/09:00 - 09:30/i);
    fireEvent.click(slotBtn);

    // Mở phần Triệu chứng
    const symptomToggle = screen.getByText(/Triệu chứng \(tùy chọn\)/i);
    fireEvent.click(symptomToggle);

    // Thêm nhóm triệu chứng 1: chọn Đau tức, tick Đau hạ vị
    const addBlockBtn = await screen.findByText(/Thêm nhóm triệu chứng/i);
    fireEvent.click(addBlockBtn);

    const categorySelects = screen.getAllByRole("combobox").filter((el) => el.tagName === "SELECT");
    fireEvent.change(categorySelects[0], { target: { value: "cat-pain" } });

    // Tick triệu chứng "Đau hạ vị"
    const symptomCheckbox = await screen.findByLabelText(/Đau hạ vị/i);
    fireEvent.click(symptomCheckbox);

    // Thêm nhóm triệu chứng 2: để rỗng (không chọn category) -> phải bị sanitize bỏ
    fireEvent.click(addBlockBtn);

    // Thêm nhóm triệu chứng 3: chọn Khác, nhập ghi chú
    fireEvent.click(addBlockBtn);
    const updatedSelects = screen.getAllByRole("combobox").filter((el) => el.tagName === "SELECT");
    fireEvent.change(updatedSelects[2], { target: { value: "cat-other" } });

    const otherTextarea = await screen.findByPlaceholderText(/Mô tả triệu chứng khác\.\.\./i);
    fireEvent.change(otherTextarea, { target: { value: "   Sốt nhẹ về chiều   " } });

    // Submit
    const confirmBtn = screen.getByRole("button", { name: /Xác nhận đặt lịch khám/i });
    fireEvent.click(confirmBtn);

    await waitFor(() => {
      expect(screen.getByText("Đặt lịch thành công!")).toBeInTheDocument();
    });

    // Verify sanitized symptoms
    const body = capturedBody as unknown as BookAppointmentRequest;
    expect(body?.symptoms).toBeDefined();
    expect(body?.symptoms).toEqual([
      {
        categoryId: "cat-pain",
        symptomId: "sym-1",
        otherNote: null,
      },
      {
        categoryId: "cat-other",
        symptomId: null,
        otherNote: "Sốt nhẹ về chiều", // Đã được trim khoảng trắng!
      },
    ]);
  });

  it("chặn submit khi role không phải PATIENT", async () => {
    useAuthStore.setState({
      accessToken: "doctor-token",
      user: {
        userId: "doc-1",
        email: "doctor@test.com",
        fullName: "Bác Sĩ",
        role: "DOCTOR",
        mustChangePassword: false,
      },
    });

    render(<BookingView />, { wrapper: createWrapper() });

    await waitFor(() => expect(screen.getByText("Tất cả")).toBeInTheDocument());

    const doctorTrigger = screen.getByRole("combobox");
    fireEvent.click(doctorTrigger);
    const docItem = await screen.findByText(/BS\. Trần Thị Nữ/i);
    fireEvent.click(docItem);

    const dayBtn = await screen.findByText(new RegExp(format(tomorrow, "dd/MM")));
    fireEvent.click(dayBtn);

    const slotBtn = await screen.findByText(/10:00 - 10:30/i);
    fireEvent.click(slotBtn);

    const confirmBtn = screen.getByRole("button", { name: /Xác nhận đặt lịch khám/i });
    fireEvent.click(confirmBtn);

    expect(toastErrorMock).toHaveBeenCalledWith(
      "Bạn cần đăng nhập với tài khoản bệnh nhân để đặt lịch."
    );
  });

  it("gọi onGuestBookAttempt khi requireAuth = true", async () => {
    const onGuestAttemptMock = vi.fn();
    render(
      <BookingView
        requireAuth={true}
        onGuestBookAttempt={onGuestAttemptMock}
      />,
      { wrapper: createWrapper() }
    );

    await waitFor(() => expect(screen.getByText("Tất cả")).toBeInTheDocument());

    const doctorTrigger = screen.getByRole("combobox");
    fireEvent.click(doctorTrigger);
    const docItem = await screen.findByText(/BS\. Trần Thị Nữ/i);
    fireEvent.click(docItem);

    const dayBtn = await screen.findByText(new RegExp(format(tomorrow, "dd/MM")));
    fireEvent.click(dayBtn);

    const slotBtn = await screen.findByText(/10:00 - 10:30/i);
    fireEvent.click(slotBtn);

    const confirmBtn = screen.getByRole("button", { name: /Xác nhận đặt lịch khám/i });
    fireEvent.click(confirmBtn);

    expect(onGuestAttemptMock).toHaveBeenCalledTimes(1);
  });
});
