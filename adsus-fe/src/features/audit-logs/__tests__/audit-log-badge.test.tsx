import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import {
  AuditLogBadge,
  getActionBadgeConfig,
} from "../components/audit-log-badge";

describe("AuditLogBadge", () => {
  it("renders Emerald tone and icon for CREATE actions", () => {
    const { container } = render(<AuditLogBadge action="CREATE_ACCOUNT" />);
    expect(screen.getByText("Tạo tài khoản")).toBeInTheDocument();
    expect(screen.getByTestId("badge-icon-create")).toBeInTheDocument();
    expect(container.firstChild).toHaveClass("bg-emerald-500/10");
  });

  it("renders Emerald tone and refresh icon for REACTIVATE actions", () => {
    const { container } = render(<AuditLogBadge action="REACTIVATE_ACCOUNT" />);
    expect(screen.getByText("Khôi phục tài khoản")).toBeInTheDocument();
    expect(screen.getByTestId("badge-icon-reactivate")).toBeInTheDocument();
    expect(container.firstChild).toHaveClass("bg-emerald-500/10");
  });

  it("renders Neutral tone and pencil icon for UPDATE actions", () => {
    const { container } = render(<AuditLogBadge action="UPDATE_ACCOUNT" />);
    expect(screen.getByText("Cập nhật tài khoản")).toBeInTheDocument();
    expect(screen.getByTestId("badge-icon-update")).toBeInTheDocument();
    expect(container.firstChild).toHaveClass("bg-muted");
  });

  it("renders Red tone and user-minus icon for DEACTIVATE actions", () => {
    const { container } = render(<AuditLogBadge action="DEACTIVATE_ACCOUNT" />);
    expect(screen.getByText("Vô hiệu hoá tài khoản")).toBeInTheDocument();
    expect(screen.getByTestId("badge-icon-deactivate")).toBeInTheDocument();
    expect(container.firstChild).toHaveClass("bg-destructive/10");
  });

  it("renders Indigo tone and brain icon for AI actions", () => {
    const { container } = render(<AuditLogBadge action="REGISTER_AI_MODEL" />);
    expect(screen.getByText("Đăng ký mô hình AI")).toBeInTheDocument();
    expect(screen.getByTestId("badge-icon-ai")).toBeInTheDocument();
    expect(container.firstChild).toHaveClass("bg-indigo-500/10");
  });

  it("renders Amber tone and key icon for PASSWORD reset actions", () => {
    const { container } = render(<AuditLogBadge action="ADMIN_RESET_PASSWORD" />);
    expect(screen.getByText("Cấp lại mật khẩu")).toBeInTheDocument();
    expect(screen.getByTestId("badge-icon-password")).toBeInTheDocument();
    expect(container.firstChild).toHaveClass("bg-amber-500/10");
  });

  it("renders unknown future action cleanly without error", () => {
    const { container } = render(<AuditLogBadge action="UNKNOWN_FUTURE_ACTION" />);
    expect(screen.getByText("UNKNOWN_FUTURE_ACTION")).toBeInTheDocument();
    expect(screen.getByTestId("badge-icon-fallback")).toBeInTheDocument();
    expect(container.firstChild).toHaveClass("bg-secondary");
  });

  it("correctly identifies action configurations in getActionBadgeConfig", () => {
    const aiConfig = getActionBadgeConfig("ACTIVATE_AI_MODEL");
    expect(aiConfig.className).toContain("indigo");

    const nurseConfig = getActionBadgeConfig("NURSE_CREATE_PATIENT_ACCOUNT");
    expect(nurseConfig.className).toContain("emerald");

    const passwordConfig = getActionBadgeConfig("NURSE_RESET_PATIENT_PASSWORD");
    expect(passwordConfig.className).toContain("amber");
  });
});
