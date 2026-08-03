import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { PrescriptionHistoryView } from "../prescription-history-view";

function renderWithClient(ui: React.ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

describe("PrescriptionHistoryView", () => {
  it("hiển thị empty state khi chưa có đơn thuốc", () => {
    renderWithClient(<PrescriptionHistoryView patientProfileId="p1" />);
    expect(screen.getByText(/chưa có đơn thuốc/i)).toBeInTheDocument();
  });
});
