import { fireEvent, render, screen, act } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { AdminFeedbackView } from "./admin-feedback-view";
import type { AdminFeedbackItem } from "../types/feedback.types";

const { useAdminFeedbacksMock } = vi.hoisted(() => ({
  useAdminFeedbacksMock: vi.fn(),
}));

vi.mock("../hooks/use-feedback", () => ({
  useAdminFeedbacks: (query: unknown) => useAdminFeedbacksMock(query),
}));

const sampleFeedbacks: AdminFeedbackItem[] = [
  {
    id: "fb-101",
    rating: 5,
    content: "Dịch vụ phòng khám rất chuyên nghiệp và chu đáo.",
    submittedAt: "2026-09-10T08:30:00Z",
    caseId: "case-alpha-1234",
    doctorName: "BS. Hoàng Văn Phúc",
    doctorId: "doc-99",
    patientProfileId: "pat-55",
    patientName: "Nguyễn Văn Đạt",
    patientPhone: "0901234567",
  },
  {
    id: "fb-102",
    rating: 2,
    content: "Thời gian chờ hơi lâu.",
    submittedAt: "2026-09-09T14:15:00Z",
    caseId: "00000000-0000-0000-0000-000000000000",
    doctorName: "",
    doctorId: "",
    patientProfileId: "",
    patientName: "Trần Thị Mai",
    patientPhone: null,
  },
  {
    id: "fb-103",
    rating: 4,
    content: null,
    submittedAt: "2026-09-08T16:00:00Z",
    caseId: "",
    doctorName: "BS. Lê Hoàng Nam",
    doctorId: "doc-88",
    patientProfileId: "pat-77",
    patientName: "Phạm Thảo Vy",
    patientPhone: "",
  },
];

describe("AdminFeedbackView Component", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders page header, title, and description", () => {
    useAdminFeedbacksMock.mockReturnValue({
      data: { items: [], page: 1, pageSize: 15, totalItems: 0, totalPages: 0 },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    });

    render(<AdminFeedbackView />);

    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent(
      "Quản lý phản hồi dịch vụ"
    );
    expect(
      screen.getByText(/Theo dõi, đánh giá chất lượng phục vụ và ý kiến đóng góp từ bệnh nhân/i)
    ).toBeInTheDocument();
  });

  it("renders search input with placeholder and rating filter dropdown", () => {
    useAdminFeedbacksMock.mockReturnValue({
      data: { items: [], page: 1, pageSize: 15, totalItems: 0, totalPages: 0 },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    });

    render(<AdminFeedbackView />);

    const searchInput = screen.getByPlaceholderText(
      "Tìm theo tên bệnh nhân, bác sĩ hoặc mã ca khám..."
    );
    expect(searchInput).toBeInTheDocument();

    const ratingSelect = screen.getByLabelText("Lọc theo đánh giá");
    expect(ratingSelect).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Tất cả đánh giá" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "5 sao" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "1 sao" })).toBeInTheDocument();
  });

  it("renders loading spinner when data is loading", () => {
    useAdminFeedbacksMock.mockReturnValue({
      data: null,
      isLoading: true,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    });

    render(<AdminFeedbackView />);

    expect(screen.getByText("Đang tải dữ liệu phản hồi...")).toBeInTheDocument();
  });

  it("renders empty state when no items returned", () => {
    useAdminFeedbacksMock.mockReturnValue({
      data: { items: [], page: 1, pageSize: 15, totalItems: 0, totalPages: 0 },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    });

    render(<AdminFeedbackView />);

    expect(
      screen.getByText("Không tìm thấy phản hồi nào phù hợp.")
    ).toBeInTheDocument();
  });

  it("renders error alert with retry button when isError is true", () => {
    const refetchMock = vi.fn();
    useAdminFeedbacksMock.mockReturnValue({
      data: null,
      isLoading: false,
      isError: true,
      error: new Error("Lỗi kết nối máy chủ"),
      refetch: refetchMock,
      isFetching: false,
    });

    render(<AdminFeedbackView />);

    const alert = screen.getByRole("alert");
    expect(alert).toBeInTheDocument();
    expect(screen.getByText(/Lỗi kết nối máy chủ/i)).toBeInTheDocument();

    const retryBtn = screen.getByRole("button", { name: /Thử lại/i });
    fireEvent.click(retryBtn);
    expect(refetchMock).toHaveBeenCalledTimes(1);
  });

  it("renders table with correct headers and container styling", () => {
    useAdminFeedbacksMock.mockReturnValue({
      data: { items: sampleFeedbacks, page: 1, pageSize: 15, totalItems: 3, totalPages: 1 },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    });

    const { container } = render(<AdminFeedbackView />);

    expect(screen.getByText("Mã ca khám / Chi tiết")).toBeInTheDocument();
    expect(screen.getByText("Bác sĩ phụ trách")).toBeInTheDocument();
    expect(screen.getByText("Bệnh nhân")).toBeInTheDocument();
    expect(screen.getByText("Đánh giá")).toBeInTheDocument();
    expect(screen.getByText("Nội dung")).toBeInTheDocument();
    expect(screen.getByText("Thời gian gửi")).toBeInTheDocument();

    const tableWrapper = container.querySelector(".rounded-3xl.border.border-border.bg-background");
    expect(tableWrapper).toBeInTheDocument();
  });

  it("renders entity links to doctor, patient, and case", () => {
    useAdminFeedbacksMock.mockReturnValue({
      data: { items: [sampleFeedbacks[0]], page: 1, pageSize: 15, totalItems: 1, totalPages: 1 },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    });

    render(<AdminFeedbackView />);

    const caseLink = screen.getByTitle("Xem chi tiết ca khám");
    expect(caseLink).toHaveAttribute("href", "/cases/case-alpha-1234");
    expect(caseLink).toHaveTextContent("case-alp...");

    const docLink = screen.getByTitle("Xem thông tin tài khoản bác sĩ");
    expect(docLink).toHaveAttribute("href", "/admin/users/doc-99");
    expect(docLink).toHaveTextContent("BS. Hoàng Văn Phúc");

    const patLink = screen.getByTitle("Xem hồ sơ bệnh nhân");
    expect(patLink).toHaveAttribute("href", "/patients/pat-55");
    expect(patLink).toHaveTextContent("Nguyễn Văn Đạt");
    expect(screen.getByText("0901234567")).toBeInTheDocument();
  });

  it("renders fallbacks for missing case, doctor, and patient links", () => {
    useAdminFeedbacksMock.mockReturnValue({
      data: { items: [sampleFeedbacks[1]], page: 1, pageSize: 15, totalItems: 1, totalPages: 1 },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    });

    render(<AdminFeedbackView />);

    expect(screen.getByText("Phản hồi chung")).toBeInTheDocument();
    expect(screen.getByText("Không xác định")).toBeInTheDocument();
    expect(screen.getByText("Trần Thị Mai")).toBeInTheDocument();
    expect(screen.getByText("Chưa có SĐT")).toBeInTheDocument();
  });

  it("renders fallback text for null feedback content", () => {
    useAdminFeedbacksMock.mockReturnValue({
      data: { items: [sampleFeedbacks[2]], page: 1, pageSize: 15, totalItems: 1, totalPages: 1 },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    });

    render(<AdminFeedbackView />);

    expect(screen.getByText("Không có nhận xét")).toBeInTheDocument();
  });

  it("renders visual 5-star rating with (X/5) badge text", () => {
    useAdminFeedbacksMock.mockReturnValue({
      data: { items: sampleFeedbacks, page: 1, pageSize: 15, totalItems: 3, totalPages: 1 },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    });

    render(<AdminFeedbackView />);

    expect(screen.getByText("(5/5)")).toBeInTheDocument();
    expect(screen.getByText("(2/5)")).toBeInTheDocument();
    expect(screen.getByText("(4/5)")).toBeInTheDocument();
  });

  it("renders pagination and result counter when multiple pages exist", () => {
    useAdminFeedbacksMock.mockReturnValue({
      data: { items: sampleFeedbacks, page: 1, pageSize: 15, totalItems: 45, totalPages: 3 },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    });

    render(<AdminFeedbackView />);

    expect(screen.getByText("Đang xem 3 / 45 kết quả")).toBeInTheDocument();
    expect(screen.getByText("Sau")).toBeInTheDocument();
  });

  it("updates rating filter and triggers query", () => {
    useAdminFeedbacksMock.mockReturnValue({
      data: { items: [], page: 1, pageSize: 15, totalItems: 0, totalPages: 0 },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    });

    render(<AdminFeedbackView />);

    const select = screen.getByLabelText("Lọc theo đánh giá");
    fireEvent.change(select, { target: { value: "5" } });

    expect(useAdminFeedbacksMock).toHaveBeenLastCalledWith(
      expect.objectContaining({ minRating: 5, page: 1 })
    );
  });

  it("debounces realtime search input typing (300ms)", async () => {
    vi.useFakeTimers();

    useAdminFeedbacksMock.mockReturnValue({
      data: { items: [], page: 1, pageSize: 15, totalItems: 0, totalPages: 0 },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    });

    render(<AdminFeedbackView />);

    const searchInput = screen.getByPlaceholderText(
      "Tìm theo tên bệnh nhân, bác sĩ hoặc mã ca khám..."
    );

    act(() => {
      fireEvent.change(searchInput, { target: { value: "BS. Phúc" } });
    });

    // Before 300ms, debounced query is still empty
    expect(useAdminFeedbacksMock).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: "" })
    );

    // Advance timer past 300ms
    act(() => {
      vi.advanceTimersByTime(300);
    });

    expect(useAdminFeedbacksMock).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: "BS. Phúc", page: 1 })
    );

    vi.useRealTimers();
  });
});
