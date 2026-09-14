import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { describe, expect, it, vi, beforeEach, beforeAll } from "vitest";
import { format, addDays, startOfDay } from "date-fns";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";
import { useAuthStore } from "@/store/auth-store";
import { BookingPageWrapper } from "@/features/appointment-scheduling/components/booking-page-wrapper";

const { routerReplaceMock, routerPushMock } = vi.hoisted(() => ({
  routerReplaceMock: vi.fn(),
  routerPushMock: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({
    push: routerPushMock,
    replace: routerReplaceMock,
  }),
}));

vi.mock("react-hot-toast", () => ({
  default: {
    error: vi.fn(),
    success: vi.fn(),
  },
}));

beforeAll(() => {
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

describe("BookingPageWrapper", () => {
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
          relatives: [],
        })
      )
    );
  });

  it("redirects về login khi người dùng chưa đăng nhập", async () => {
    useAuthStore.setState({
      accessToken: null,
      user: null,
    });

    render(<BookingPageWrapper />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(routerReplaceMock).toHaveBeenCalledWith("/login?redirect=/dat-lich");
    });
  });

  it("redirects về landing khi role không phải PATIENT", async () => {
    useAuthStore.setState({
      accessToken: "staff-token",
      user: {
        userId: "staff-1",
        email: "staff@test.com",
        fullName: "Nhân Viên",
        role: "STAFF",
        mustChangePassword: false,
      },
    });

    render(<BookingPageWrapper />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(routerReplaceMock).toHaveBeenCalledWith("/");
    });
  });

  it("bảo toàn state form khi chuyển Tab qua lại (forceMount)", async () => {
    render(<BookingPageWrapper />, { wrapper: createWrapper() });

    // Đợi BookingView tải xong
    await waitFor(() => {
      expect(screen.getByText("Đặt lịch khám trực tuyến")).toBeInTheDocument();
    });

    // 1. Nhập lý do khám ở Tab 1
    const reasonInput = screen.getByPlaceholderText(/Đau hạ vị 2 ngày nay/i);
    fireEvent.change(reasonInput, { target: { value: "Khám phụ khoa định kỳ" } });
    expect(reasonInput).toHaveValue("Khám phụ khoa định kỳ");

    // 2. Chuyển sang Tab 2 "Thêm người thân"
    const addRelativeTabTrigger = screen.getByRole("tab", { name: /Thêm người thân/i });
    fireEvent.click(addRelativeTabTrigger);

    // Tab 2 hiển thị
    await waitFor(() => {
      expect(screen.getByText("Thêm người thân mới")).toBeInTheDocument();
    });

    // Nhập thử thông tin trên Tab 2
    const relativeNameInput = screen.getByLabelText(/Họ tên người thân/i);
    fireEvent.change(relativeNameInput, { target: { value: "Nguyễn Thị Mẹ" } });
    expect(relativeNameInput).toHaveValue("Nguyễn Thị Mẹ");

    // 3. Chuyển ngược lại về Tab 1 "Đặt lịch khám"
    const bookingTabTrigger = screen.getByRole("tab", { name: /Đặt lịch khám/i });
    fireEvent.click(bookingTabTrigger);

    // 4. Kiểm tra dữ liệu Tab 1 vẫn nguyên vẹn nhờ forceMount
    const preservedReasonInput = screen.getByPlaceholderText(/Đau hạ vị 2 ngày nay/i);
    expect(preservedReasonInput).toHaveValue("Khám phụ khoa định kỳ");

    // Kiểm tra dữ liệu Tab 2 cũng vẫn còn trong DOM
    expect(screen.getByLabelText(/Họ tên người thân/i)).toHaveValue("Nguyễn Thị Mẹ");
  });
});
