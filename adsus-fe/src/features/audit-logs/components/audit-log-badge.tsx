import type { ReactNode } from "react";
import {
  BrainCircuit,
  KeyRound,
  PencilLine,
  RefreshCcw,
  UserMinus,
  UserPlus,
} from "lucide-react";
import { cn } from "@/lib/utils";

interface AuditBadgeConfig {
  label: string;
  icon: ReactNode;
  className: string;
}

const KNOWN_LABELS: Record<string, string> = {
  CREATE_ACCOUNT: "Tạo tài khoản",
  UPDATE_ACCOUNT: "Cập nhật tài khoản",
  DEACTIVATE_ACCOUNT: "Vô hiệu hoá tài khoản",
  REACTIVATE_ACCOUNT: "Khôi phục tài khoản",
  ACCOUNT_LOCK: "Khoá tài khoản",
  ACCOUNT_UNLOCK: "Mở khoá tài khoản",
  ADMIN_RESET_PASSWORD: "Cấp lại mật khẩu",
  SELF_RESET_PASSWORD: "Tự đặt lại mật khẩu",
  REGISTER_AI_MODEL: "Đăng ký mô hình AI",
  UPDATE_AI_MODEL: "Cập nhật mô hình AI",
  ACTIVATE_AI_MODEL: "Kích hoạt mô hình AI",
  NURSE_CREATE_PATIENT_ACCOUNT: "Tạo hồ sơ bệnh nhân",
  NURSE_UPDATE_PATIENT_ACCOUNT: "Cập nhật hồ sơ bệnh nhân",
  NURSE_RESET_PATIENT_PASSWORD: "Cấp lại mật khẩu bệnh nhân",
};

export function getActionBadgeConfig(action: string): AuditBadgeConfig {
  const upper = (action || "").toUpperCase();
  const label = KNOWN_LABELS[upper] || action;

  // 1. AI models -> Indigo with brain icon
  if (upper.includes("AI_MODEL") || upper.includes("_AI") || upper.startsWith("AI_")) {
    return {
      label,
      icon: <BrainCircuit className="size-3.5 shrink-0" data-testid="badge-icon-ai" />,
      className:
        "bg-indigo-500/10 text-indigo-700 dark:text-indigo-400 border border-indigo-500/20",
    };
  }

  // 2. Passwords / Reset -> Amber with key icon
  if (upper.includes("PASSWORD") || upper.includes("RESET")) {
    return {
      label,
      icon: <KeyRound className="size-3.5 shrink-0" data-testid="badge-icon-password" />,
      className:
        "bg-amber-500/10 text-amber-700 dark:text-amber-400 border border-amber-500/20",
    };
  }

  // 3. Deactivation / Lock -> Red with user-minus/lock icon
  if (
    upper.includes("DEACTIVATE") ||
    upper.includes("LOCK") ||
    upper.includes("BAN") ||
    upper.includes("DELETE")
  ) {
    return {
      label,
      icon: <UserMinus className="size-3.5 shrink-0" data-testid="badge-icon-deactivate" />,
      className: "bg-destructive/10 text-destructive border border-destructive/20",
    };
  }

  // 4. Create / Reactivate -> Emerald with user-plus or refresh icon
  if (upper.includes("CREATE") || upper.includes("REACTIVATE") || upper.includes("ADD")) {
    const isReactivate = upper.includes("REACTIVATE");
    return {
      label,
      icon: isReactivate ? (
        <RefreshCcw className="size-3.5 shrink-0" data-testid="badge-icon-reactivate" />
      ) : (
        <UserPlus className="size-3.5 shrink-0" data-testid="badge-icon-create" />
      ),
      className:
        "bg-emerald-500/10 text-emerald-700 dark:text-emerald-400 border border-emerald-500/20",
    };
  }

  // 5. Update / Edit -> Neutral with pencil icon
  if (upper.includes("UPDATE") || upper.includes("EDIT") || upper.includes("MODIFY")) {
    return {
      label,
      icon: <PencilLine className="size-3.5 shrink-0" data-testid="badge-icon-update" />,
      className: "bg-muted text-muted-foreground border border-border",
    };
  }

  // 6. Fallback / Unknown actions -> Neutral fallback with clean presentation
  return {
    label,
    icon: <PencilLine className="size-3.5 shrink-0" data-testid="badge-icon-fallback" />,
    className: "bg-secondary text-secondary-foreground border border-border",
  };
}

export interface AuditLogBadgeProps {
  action: string;
  className?: string;
}

export function AuditLogBadge({ action, className }: AuditLogBadgeProps) {
  const { label, icon, className: toneClass } = getActionBadgeConfig(action);

  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold tracking-wide transition-colors",
        toneClass,
        className
      )}
      title={action}
      data-testid="audit-action-badge"
    >
      {icon}
      <span>{label}</span>
    </span>
  );
}
