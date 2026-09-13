import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { AiModelDetailDialog } from "@/features/ai-model-management/components/ai-model-detail-dialog";
import type { AiModelVersion } from "@/features/ai-model-management/types/ai-model.types";

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

describe("AiModelDetailDialog", () => {
  it("không render gì khi open=false", () => {
    const { container } = render(
      <AiModelDetailDialog open={false} model={buildModel({})} onClose={() => {}} />,
    );
    expect(container).toBeEmptyDOMElement();
  });

  it("không render gì khi model=null dù open=true", () => {
    const { container } = render(<AiModelDetailDialog open={true} model={null} onClose={() => {}} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("hiện đủ thông tin và nhãn 'Đang chạy' khi Active", () => {
    render(
      <AiModelDetailDialog
        open={true}
        model={buildModel({ status: "Active" })}
        onClose={() => {}}
      />,
    );

    expect(screen.getByText("Chi tiết phiên bản: YOLO26_v1")).toBeInTheDocument();
    expect(screen.getByText("org/repo")).toBeInTheDocument();
    expect(screen.getByText("model.pt")).toBeInTheDocument();
    expect(screen.getByText("Đang chạy")).toBeInTheDocument();
  });

  it("hiện nhãn 'Ngưng hoạt động' và '—' cho metrics rỗng khi Inactive/null", () => {
    render(
      <AiModelDetailDialog
        open={true}
        model={buildModel({
          status: "Inactive",
          description: undefined,
          metricsPrecision: undefined,
          metricsMap50: undefined,
          metricsRecall: undefined,
        })}
        onClose={() => {}}
      />,
    );

    expect(screen.getByText("Ngưng hoạt động")).toBeInTheDocument();
    expect(screen.getByText("Không có mô tả")).toBeInTheDocument();
    expect(screen.getAllByText("—")).toHaveLength(3);
  });

  it("hiển thị mục Đánh giá trực tiếp (Live Evaluation) với đầy đủ live metrics và counters", () => {
    render(
      <AiModelDetailDialog
        open={true}
        model={buildModel({
          livePrecision: 0.885,
          liveRecall: 0.912,
          liveMap50: 89.4,
          liveTp: 45,
          liveFp: 6,
          liveFn: 4,
          lastEvaluatedAt: "2026-09-01T12:00:00Z",
        })}
        onClose={() => {}}
      />,
    );

    expect(screen.getByText("Đánh giá trực tiếp (Live Evaluation)")).toBeInTheDocument();
    expect(screen.getByText("88.5%")).toBeInTheDocument(); // Live Precision
    expect(screen.getByText("91.2%")).toBeInTheDocument(); // Live Recall
    expect(screen.getByText("89.4%")).toBeInTheDocument(); // Live mAP50
    expect(screen.getByText("45")).toBeInTheDocument(); // Live TP
    expect(screen.getByText("6")).toBeInTheDocument();  // Live FP
    expect(screen.getByText("4")).toBeInTheDocument();  // Live FN
    expect(screen.getByText(/Đánh giá lần cuối:/)).toBeInTheDocument();
  });

  it("xử lý an toàn khi Live metrics chưa có dữ liệu (null/undefined)", () => {
    render(
      <AiModelDetailDialog
        open={true}
        model={buildModel({
          livePrecision: undefined,
          liveRecall: undefined,
          liveMap50: undefined,
          liveTp: undefined,
          liveFp: undefined,
          liveFn: undefined,
          lastEvaluatedAt: undefined,
        })}
        onClose={() => {}}
      />,
    );

    expect(screen.getByText("Đánh giá trực tiếp (Live Evaluation)")).toBeInTheDocument();
    expect(screen.getAllByText("Chưa có dữ liệu").length).toBeGreaterThanOrEqual(3);
    expect(screen.getAllByText("0")).toHaveLength(3); // Live TP, FP, FN default to 0
  });
});

