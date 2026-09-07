import { fireEvent, render, screen } from "@testing-library/react";
import { AxiosError, AxiosHeaders } from "axios";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { AiModelList } from "@/features/ai-model-management/components/ai-model-list";
import type { AiModelVersion } from "@/features/ai-model-management/types/ai-model.types";

function loiHttp(status: number, message: string): AxiosError {
  const error = new AxiosError("Request failed");
  error.response = {
    status,
    statusText: "",
    data: { code: status, message, data: null },
    headers: {},
    config: { headers: new AxiosHeaders() },
  };
  return error;
}

const {
  useAiModelListMock,
  activateMutateMock,
  resetActivateMock,
  calculateMutateMock,
  toastLoadingMock,
  toastSuccessMock,
  toastErrorMock,
} = vi.hoisted(() => ({
  useAiModelListMock: vi.fn(),
  activateMutateMock: vi.fn(),
  resetActivateMock: vi.fn(),
  calculateMutateMock: vi.fn(),
  toastLoadingMock: vi.fn(() => "toast-id"),
  toastSuccessMock: vi.fn(),
  toastErrorMock: vi.fn(),
}));

vi.mock("@/features/ai-model-management/hooks/use-ai-models", () => ({
  useAiModelList: () => useAiModelListMock(),
  useActivateAiModel: () => ({
    mutate: activateMutateMock,
    isPending: false,
    error: null,
    reset: resetActivateMock,
  }),
  useCalculateMap50: () => ({ mutate: calculateMutateMock, isPending: false }),
  // AiModelFormDialog is always mounted (just returns null while closed) — its hooks still run.
  useAiModelDetail: () => ({ data: undefined, isLoading: false }),
  useRegisterAiModel: () => ({ mutate: vi.fn(), isPending: false, error: null }),
  useUpdateAiModel: () => ({ mutate: vi.fn(), isPending: false, error: null }),
}));

vi.mock("react-hot-toast", () => ({
  default: {
    loading: toastLoadingMock,
    success: toastSuccessMock,
    error: toastErrorMock,
  },
}));

function buildModel(overrides: Partial<AiModelVersion>): AiModelVersion {
  return {
    modelVersionId: "model-1",
    versionCode: "YOLO26_v1",
    description: "Mô hình chính thức",
    metricsPrecision: 91.5,
    metricsMap50: 88.2,
    metricsRecall: 0.93,
    hfRepoId: "org/repo",
    hfFilename: "model.pt",
    status: "Inactive",
    registeredAt: "2026-08-01T00:00:00Z",
    ...overrides,
  };
}

interface MockListState {
  data?: { items: AiModelVersion[]; page: number; pageSize: number; totalItems: number; totalPages: number };
  isLoading?: boolean;
  isError?: boolean;
  error?: unknown;
}

function mockList(overrides: MockListState) {
  useAiModelListMock.mockReturnValue({
    data: undefined,
    isLoading: false,
    isError: false,
    error: null,
    ...overrides,
  });
}

describe("AiModelList — trạng thái tải (UC-20)", () => {
  beforeEach(() => {
    activateMutateMock.mockClear();
    resetActivateMock.mockClear();
    calculateMutateMock.mockClear();
    toastLoadingMock.mockClear();
    toastSuccessMock.mockClear();
    toastErrorMock.mockClear();
  });

  it("isLoading=true — chưa hiện thông báo rỗng, chưa hiện dòng dữ liệu", () => {
    mockList({ isLoading: true });

    render(<AiModelList />);

    expect(screen.queryByText(/Chưa có phiên bản AI nào/)).not.toBeInTheDocument();
    expect(screen.queryByText("YOLO26_v1")).not.toBeInTheDocument();
  });

  it("danh sách rỗng — hiện thông báo chưa có phiên bản nào", () => {
    mockList({
      data: { items: [], page: 1, pageSize: 10, totalItems: 0, totalPages: 1 },
    });

    render(<AiModelList />);

    expect(screen.getByText(/Chưa có phiên bản AI nào được đăng ký/)).toBeInTheDocument();
  });

  it("isError=true — hiện alert với message lỗi", () => {
    mockList({ isError: true, error: loiHttp(500, "Lỗi tải danh sách") });

    render(<AiModelList />);

    expect(screen.getByRole("alert")).toHaveTextContent("Lỗi tải danh sách");
  });
});

describe("AiModelList — hiện/ẩn thao tác theo trạng thái model", () => {
  beforeEach(() => {
    activateMutateMock.mockClear();
    resetActivateMock.mockClear();
    calculateMutateMock.mockClear();
  });

  it("model Active — hiện nhãn 'Đang chạy', KHÔNG hiện nút Sửa/Kích hoạt", () => {
    mockList({
      data: {
        items: [buildModel({ status: "Active" })],
        page: 1,
        pageSize: 10,
        totalItems: 1,
        totalPages: 1,
      },
    });

    render(<AiModelList />);

    expect(screen.getByText("Đang chạy")).toBeInTheDocument();
    expect(screen.queryByTitle("Sửa thông tin")).not.toBeInTheDocument();
    expect(screen.queryByTitle("Kích hoạt mô hình này")).not.toBeInTheDocument();
  });

  it("model Inactive — hiện nhãn 'Inactive' VÀ nút Sửa/Kích hoạt", () => {
    mockList({
      data: {
        items: [buildModel({ status: "Inactive" })],
        page: 1,
        pageSize: 10,
        totalItems: 1,
        totalPages: 1,
      },
    });

    render(<AiModelList />);

    expect(screen.getByText("Inactive")).toBeInTheDocument();
    expect(screen.getByTitle("Sửa thông tin")).toBeInTheDocument();
    expect(screen.getByTitle("Kích hoạt mô hình này")).toBeInTheDocument();
  });
});

describe("AiModelList — luồng kích hoạt phiên bản (BR-02)", () => {
  beforeEach(() => {
    activateMutateMock.mockClear();
    resetActivateMock.mockClear();
    mockList({
      data: {
        items: [buildModel({ modelVersionId: "model-7", versionCode: "v7", status: "Inactive" })],
        page: 1,
        pageSize: 10,
        totalItems: 1,
        totalPages: 1,
      },
    });
  });

  it("bấm icon Kích hoạt mở ConfirmDialog đúng tên phiên bản, xác nhận gọi mutate đúng id", () => {
    render(<AiModelList />);

    fireEvent.click(screen.getByTitle("Kích hoạt mô hình này"));

    expect(screen.getByText(/kích hoạt phiên bản v7/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Kích hoạt" }));

    expect(activateMutateMock).toHaveBeenCalledWith("model-7", expect.any(Object));
  });

  it("bấm Huỷ đóng ConfirmDialog và reset lỗi activate", () => {
    render(<AiModelList />);

    fireEvent.click(screen.getByTitle("Kích hoạt mô hình này"));
    fireEvent.click(screen.getByRole("button", { name: "Huỷ" }));

    expect(resetActivateMock).toHaveBeenCalled();
    expect(screen.queryByText(/kích hoạt phiên bản v7/)).not.toBeInTheDocument();
  });
});

describe("AiModelList — tính lại mAP50", () => {
  beforeEach(() => {
    calculateMutateMock.mockClear();
    toastLoadingMock.mockClear();
    toastSuccessMock.mockClear();
    toastErrorMock.mockClear();
    mockList({
      data: {
        items: [buildModel({ modelVersionId: "model-9" })],
        page: 1,
        pageSize: 10,
        totalItems: 1,
        totalPages: 1,
      },
    });
  });

  it("bấm 'Tính lại mAP50' gọi mutate đúng id và báo toast khi thành công", () => {
    calculateMutateMock.mockImplementation((_id, options) => {
      options?.onSuccess?.();
    });

    render(<AiModelList />);

    fireEvent.click(screen.getByTitle("Tính lại mAP50"));

    expect(calculateMutateMock).toHaveBeenCalledWith("model-9", expect.any(Object));
    expect(toastLoadingMock).toHaveBeenCalled();
    expect(toastSuccessMock).toHaveBeenCalledWith(
      "Đã quét Database và tính toán xong mAP50!",
      expect.objectContaining({ id: "toast-id" }),
    );
  });

  it("báo toast lỗi khi tính toán thất bại", () => {
    calculateMutateMock.mockImplementation((_id, options) => {
      options?.onError?.(loiHttp(500, "Lỗi tính toán"));
    });

    render(<AiModelList />);

    fireEvent.click(screen.getByTitle("Tính lại mAP50"));

    expect(toastErrorMock).toHaveBeenCalledWith("Lỗi tính toán", expect.objectContaining({ id: "toast-id" }));
  });
});
