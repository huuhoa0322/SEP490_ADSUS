import type { Role } from "@/types/api.types";
import { cn } from "@/lib/utils";

export const ROLE_LABEL: Record<Role, string> = {
  ADMIN: "Quản trị viên",
  DOCTOR: "Bác sĩ",
  STAFF: "Nhân viên",
  PATIENT: "Bệnh nhân",
  PHARMACIST: "Dược sĩ",
};

// Quiet department color-code (dot only, not a filled pill) — the same idea as the
// colour on a ward badge or scrub cap: identifies role at a glance without shouting.
// Reuses existing tokens only, no new colours: --primary / --accent / --chart-3 are
// already the app's navy / teal / blue; --status-warning is the existing amber used
// for warning states elsewhere (dashboard, inventory alerts).
const ROLE_DOT_CLASS: Record<Role, string> = {
  ADMIN: "bg-primary",
  DOCTOR: "bg-[var(--success)]",
  STAFF: "bg-chart-3",
  PHARMACIST: "bg-[var(--status-warning)]",
  PATIENT: "bg-muted-foreground",
};

export function RoleBadge({
  role,
  className,
}: {
  role: Role;
  className?: string;
}) {
  return (
    <span className={cn("inline-flex items-center gap-1.5 text-sm font-medium text-muted-foreground", className)}>
      <span aria-hidden className={cn("size-1.5 shrink-0 rounded-full", ROLE_DOT_CLASS[role])} />
      {ROLE_LABEL[role]}
    </span>
  );
}

export function roleDotClassName(role: Role): string {
  return ROLE_DOT_CLASS[role];
}

// Same four tokens as ROLE_DOT_CLASS, expressed as `var(...)` so callers can set them
// as a CSS custom property (e.g. `style={{ "--role-accent": roleAccentVar(role) }}`)
// and have every `var(--role-accent)`-based utility in that subtree pick up the
// signed-in user's role colour — used to tint the active nav item per role.
const ROLE_ACCENT_VAR: Record<Role, string> = {
  ADMIN: "var(--primary)",
  DOCTOR: "var(--success)",
  STAFF: "var(--chart-3)",
  PHARMACIST: "var(--status-warning)",
  PATIENT: "var(--muted-foreground)",
};

export function roleAccentVar(role: Role): string {
  return ROLE_ACCENT_VAR[role];
}
