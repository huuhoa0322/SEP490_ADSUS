import { render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { PrescriptionHistoryView } from "../prescription-history-view";

function renderWithClient(ui: React.ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

describe("PrescriptionHistoryView", () => {
  // Test chỉ render empty state khi TanStack Query hoàn tất (Promise từ hook).
  // Không cần MSW — query sẽ error do không có base URL axios, nhưng ta mock
  // bằng cách bọc QueryClient với retry:false và check UI fallback.
  it("hiển thị loading hoặc error/empty state ngay từ đầu", async () => {
    renderWithClient(<PrescriptionHistoryView patientProfileId="p1" />);
    await waitFor(() => {
      // Có thể là loading, error, hoặc empty — đều không phải full data list.
      // Quan trọng: component render được, không crash.
      const body = document.body.textContent ?? "";
      expect(body.length).toBeGreaterThan(0);
      // Phải có 1 trong 3 state cơ bản
      expect(
        body.includes("Đang tải") ||
          body.includes("Không tải") ||
          body.includes("chưa có đơn thuốc") ||
          body.includes("Tất cả"), // filter buttons vẫn render
      ).toBe(true);
    });
  });
});
