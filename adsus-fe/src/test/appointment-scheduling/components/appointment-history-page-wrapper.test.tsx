import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { describe, expect, it, vi, beforeEach } from "vitest";
import "@testing-library/jest-dom/vitest";

import { AppointmentHistoryPageWrapper } from "@/features/appointment-scheduling/components/appointment-history-page-wrapper";
import { useAuthStore } from "@/store/auth-store";

const { routerReplaceMock } = vi.hoisted(() => ({
  routerReplaceMock: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({
    push: vi.fn(),
    replace: routerReplaceMock,
  }),
}));

vi.mock("react-hot-toast", () => ({
  default: { error: vi.fn(), success: vi.fn() },
}));

// Mock useHasHydrated to control hydration state
const _mockUseHasHydrated = vi.fn();
vi.mock("@/store/auth-store", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/store/auth-store")>();
  return {
    ...actual,
    useHasHydrated: () => _mockUseHasHydrated(),
  };
});

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  }
  return Wrapper;
}

describe("AppointmentHistoryPageWrapper", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    _mockUseHasHydrated.mockReturnValue(true);
    useAuthStore.setState({
      accessToken: null,
      user: null,
    });
  });

  // ---------------------------------------------------------------------------
  // 1. Guest → redirect to login
  // ---------------------------------------------------------------------------
  it("redirects to /login when not authenticated", async () => {
    useAuthStore.setState({ accessToken: null, user: null });

    render(<AppointmentHistoryPageWrapper />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(routerReplaceMock).toHaveBeenCalledWith("/login?redirect=/lich-hen-cua-toi");
    });
  });

  // ---------------------------------------------------------------------------
  // 2. Doctor/Admin → redirect to /
  // ---------------------------------------------------------------------------
  it("redirects to / when role is DOCTOR", async () => {
    useAuthStore.setState({
      accessToken: "doctor-token",
      user: {
        userId: "doc-1",
        email: "doctor@test.com",
        fullName: "BS. Nguyễn Văn A",
        role: "DOCTOR",
        mustChangePassword: false,
      },
    });

    render(<AppointmentHistoryPageWrapper />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(routerReplaceMock).toHaveBeenCalledWith("/");
    });
  });

  it("redirects to / when role is ADMIN", async () => {
    useAuthStore.setState({
      accessToken: "admin-token",
      user: {
        userId: "admin-1",
        email: "admin@test.com",
        fullName: "Admin User",
        role: "ADMIN",
        mustChangePassword: false,
      },
    });

    render(<AppointmentHistoryPageWrapper />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(routerReplaceMock).toHaveBeenCalledWith("/");
    });
  });

  // ---------------------------------------------------------------------------
  // 3. Patient → render AppointmentHistoryView
  // ---------------------------------------------------------------------------
  it("renders AppointmentHistoryView when role is PATIENT", async () => {
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

    render(<AppointmentHistoryPageWrapper />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getByText("Lịch hẹn của tôi")).toBeInTheDocument();
    });
    expect(screen.getByRole("tab", { name: /Lịch của tôi/ })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: /Lịch người thân/ })).toBeInTheDocument();
  });

  // ---------------------------------------------------------------------------
  // 4. Non-hydrated → return null (no redirect)
  // ---------------------------------------------------------------------------
  it("returns null without redirect when not hydrated", async () => {
    _mockUseHasHydrated.mockReturnValue(false);

    render(<AppointmentHistoryPageWrapper />, { wrapper: createWrapper() });

    // Should not redirect while not hydrated
    expect(routerReplaceMock).not.toHaveBeenCalled();
    // Should not Render content
    expect(screen.queryByText("Lịch hẹn của tôi")).not.toBeInTheDocument();
  });
});
