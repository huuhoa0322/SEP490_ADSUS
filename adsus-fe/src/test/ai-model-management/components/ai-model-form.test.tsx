import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, AxiosHeaders } from "axios";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { AiModelFormDialog } from "@/features/ai-model-management/components/ai-model-form";
import type { AiModelVersion } from "@/features/ai-model-management/types/ai-model.types";

const {
  useAiModelDetailMock,
  registerMutateMock,
  updateMutateMock,
  useUpdateAiModelStateMock,
  toastErrorMock,
  toastSuccessMock,
} = vi.hoisted(() => ({
  useAiModelDetailMock: vi.fn(),
  registerMutateMock: vi.fn(),
  updateMutateMock: vi.fn(),
  useUpdateAiModelStateMock: vi.fn(() => ({ isPending: false, error: null as unknown })),
  toastErrorMock: vi.fn(),
  toastSuccessMock: vi.fn(),
}));

vi.mock("@/features/ai-model-management/hooks/use-ai-models", () => ({
  useAiModelDetail: () => useAiModelDetailMock(),
  useRegisterAiModel: () => ({ mutate: registerMutateMock, isPending: false, error: null }),
  useUpdateAiModel: () => ({ mutate: updateMutateMock, ...useUpdateAiModelStateMock() }),
}));

vi.mock("react-hot-toast", () => ({
  default: { error: toastErrorMock, success: toastSuccessMock },
}));

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

function getFileInput(container: HTMLElement): HTMLInputElement {
  const input = container.querySelector('input[type="file"]');
  if (!input) throw new Error("file input not found");
  return input as HTMLInputElement;
}

describe("AiModelFormDialog", () => {
  beforeEach(() => {
    registerMutateMock.mockClear();
    updateMutateMock.mockClear();
    toastErrorMock.mockClear();
    toastSuccessMock.mockClear();
    useAiModelDetailMock.mockReturnValue({ data: undefined, isLoading: false });
    useUpdateAiModelStateMock.mockReturnValue({ isPending: false, error: null });
  });

  it("open=false — không render gì", () => {
    const { container } = render(
      <AiModelFormDialog open={false} onClose={() => {}} onSuccess={() => {}} />,
    );
    expect(container).toBeEmptyDOMElement();
  });

  it("chế độ đăng ký: tải file JSON tự điền các field rồi submit đúng payload", async () => {
    const onSuccess = vi.fn();
    const { container } = render(
      <AiModelFormDialog open={true} onClose={() => {}} onSuccess={onSuccess} />,
    );

    const file = new File(
      [
        JSON.stringify({
          versionCode: "YOLO26_v2",
          description: "Bản nâng cấp",
          hfRepoId: "org/repo2",
          hfFilename: "model2.pt",
          metricsPrecision: 92,
          metricsMap50: 87,
          metricsRecall: 0.91,
        }),
      ],
      "config.json",
      { type: "application/json" },
    );

    await userEvent.upload(getFileInput(container), file);

    await waitFor(() => expect(screen.getByLabelText(/Mã phiên bản/)).toHaveValue("YOLO26_v2"));
    expect(screen.getByLabelText("HuggingFace Repo ID *")).toHaveValue("org/repo2");
    expect(screen.getByLabelText("HuggingFace Filename *")).toHaveValue("model2.pt");

    registerMutateMock.mockImplementation((_payload, options) => {
      options?.onSuccess?.();
    });

    fireEvent.click(screen.getByRole("button", { name: /Đăng ký mô hình/ }));

    expect(registerMutateMock).toHaveBeenCalledWith(
      {
        versionCode: "YOLO26_v2",
        description: "Bản nâng cấp",
        hfRepoId: "org/repo2",
        hfFilename: "model2.pt",
        metricsPrecision: 92,
        metricsMap50: 87,
        metricsRecall: 0.91,
      },
      expect.any(Object),
    );
    expect(toastSuccessMock).toHaveBeenCalledWith("Đăng ký mô hình AI thành công!");
    expect(onSuccess).toHaveBeenCalled();
  });

  it("chế độ đăng ký: tải file Key=Value tự điền các field (chuẩn hoá tên field PascalCase -> camelCase)", async () => {
    const { container } = render(
      <AiModelFormDialog open={true} onClose={() => {}} onSuccess={() => {}} />,
    );

    const file = new File(
      ["VersionCode=YOLO26_v3\nHfRepoId=org/repo3\nHfFilename=model3.pt\n# ghi chú bị bỏ qua"],
      "config.txt",
      { type: "text/plain" },
    );

    await userEvent.upload(getFileInput(container), file);

    await waitFor(() => expect(screen.getByLabelText(/Mã phiên bản/)).toHaveValue("YOLO26_v3"));
    expect(screen.getByLabelText("HuggingFace Repo ID *")).toHaveValue("org/repo3");
    expect(screen.getByLabelText("HuggingFace Filename *")).toHaveValue("model3.pt");
  });

  it("file JSON không hợp lệ — báo toast lỗi, không đổi field", async () => {
    // Component tự console.error() trong nhánh catch — mock đi để output test không lẫn log
    // của một nhánh lỗi ĐÃ ĐƯỢC XỬ LÝ đúng (không phải lỗi test thật).
    const consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});

    const { container } = render(
      <AiModelFormDialog open={true} onClose={() => {}} onSuccess={() => {}} />,
    );

    const file = new File(["{ invalid json"], "bad.json", { type: "application/json" });

    await userEvent.upload(getFileInput(container), file);

    await waitFor(() =>
      expect(toastErrorMock).toHaveBeenCalledWith(
        "File không đúng định dạng. Vui lòng dùng JSON hoặc chuẩn Key=Value",
      ),
    );
    expect(screen.getByLabelText(/Mã phiên bản/)).toHaveValue("");
    expect(consoleErrorSpy).toHaveBeenCalled();
    consoleErrorSpy.mockRestore();
  });

  it("chế độ sửa: tải sẵn dữ liệu từ detail, không cho sửa Mã phiên bản, submit gọi update", () => {
    useAiModelDetailMock.mockReturnValue({ data: buildModel({}), isLoading: false });
    const onSuccess = vi.fn();

    render(<AiModelFormDialog id="model-1" open={true} onClose={() => {}} onSuccess={onSuccess} />);

    expect(screen.getByLabelText(/Mã phiên bản/)).toHaveValue("YOLO26_v1");
    expect(screen.getByText("Không thể thay đổi mã phiên bản sau khi tạo.")).toBeInTheDocument();

    updateMutateMock.mockImplementation((_args, options) => {
      options?.onSuccess?.();
    });

    fireEvent.click(screen.getByRole("button", { name: /Lưu thay đổi/ }));

    expect(updateMutateMock).toHaveBeenCalledWith(
      {
        id: "model-1",
        payload: {
          description: "Mô hình chính thức",
          hfRepoId: "org/repo",
          hfFilename: "model.pt",
          metricsPrecision: 91.5,
          metricsMap50: 88.2,
          metricsRecall: 0.93,
        },
      },
      expect.any(Object),
    );
    expect(toastSuccessMock).toHaveBeenCalledWith("Cập nhật mô hình AI thành công!");
    expect(onSuccess).toHaveBeenCalled();
  });

  it("chế độ sửa phiên bản đang Active — chặn submit, hiện cảnh báo, ẩn nút tải file", () => {
    useAiModelDetailMock.mockReturnValue({ data: buildModel({ status: "Active" }), isLoading: false });

    render(<AiModelFormDialog id="model-1" open={true} onClose={() => {}} onSuccess={() => {}} />);

    expect(screen.getByText(/đang được/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Lưu thay đổi/ })).toBeDisabled();
    expect(screen.queryByText("Tải cấu hình từ File")).not.toBeInTheDocument();
  });

  it("submit thất bại — hiện đúng message lỗi từ backend", () => {
    useAiModelDetailMock.mockReturnValue({ data: buildModel({}), isLoading: false });
    // apiError trong component đọc từ updateError (kết quả của hook, không phải tham số
    // callback onError) — phải mock cả 2 khớp nhau để tái hiện đúng luồng thật.
    useUpdateAiModelStateMock.mockReturnValue({
      isPending: false,
      error: loiHttp(422, "Mã HuggingFace không hợp lệ."),
    });

    render(<AiModelFormDialog id="model-1" open={true} onClose={() => {}} onSuccess={() => {}} />);

    updateMutateMock.mockImplementation((_args, options) => {
      options?.onError?.();
    });

    fireEvent.click(screen.getByRole("button", { name: /Lưu thay đổi/ }));

    expect(screen.getByRole("alert")).toHaveTextContent("Mã HuggingFace không hợp lệ.");
  });
});
