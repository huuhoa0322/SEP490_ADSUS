import {
  AlertTriangle,
  BrainCircuit,
  CalendarClock,
  ClipboardCheck,
  ClipboardList,
  FileText,
  KeyRound,
  LayoutDashboard,
  Pill,
  Receipt,
  Truck,
  Users,
  type LucideIcon,
  PackagePlus,
} from "lucide-react";

import type { Role } from "@/types/api.types";

export interface NavLeaf {
  title: string;
  href: string;
  icon: LucideIcon;
  roles: Role[];
  /** Default active-match is `pathname.startsWith(href)`. Override for items whose
   *  href is a prefix of a sibling item's href (e.g. "/schedule" vs "/schedule/patients"). */
  isActive?: (pathname: string) => boolean;
}

export interface NavGroup {
  label: string;
  items: NavLeaf[];
}

/** Single source of truth for role-based navigation — consumed by AppSidebar,
 *  MobileNavDrawer, and nothing else, so the two never drift out of sync. */
export const NAV_GROUPS: NavGroup[] = [
  {
    label: "Tổng quan",
    items: [
      { title: "Bảng điều khiển", href: "/dashboard", icon: LayoutDashboard, roles: ["ADMIN"] },
    ],
  },
  {
    label: "Lâm sàng",
    items: [
      { title: "Danh sách bệnh nhân", href: "/patients", icon: ClipboardList, roles: ["DOCTOR", "NURSE"] },
      { title: "Tiếp đón / Check-in", href: "/checkin", icon: ClipboardCheck, roles: ["NURSE"] },
    ],
  },
  {
    label: "Lịch làm việc",
    items: [
      {
        title: "Quản lý lịch",
        href: "/schedule",
        icon: CalendarClock,
        roles: ["DOCTOR"],
      },
      { title: "Theo dõi & nhắc uống thuốc", href: "/medication-tracking", icon: Pill, roles: ["DOCTOR"] },
      { title: "Duyệt nghỉ phép bác sĩ", href: "/admin/shift-requests", icon: CalendarClock, roles: ["ADMIN"] },
    ],
  },
  {
    label: "Dược & kho vận",
    items: [
      { 
        title: "Danh mục thuốc", 
        href: "/medicines", 
        icon: Pill, 
        roles: ["ADMIN", "PHARMACIST", "DOCTOR"],
        isActive: (p) => p.startsWith("/medicines") && !p.startsWith("/medicines/inventory-alerts"),
      },
      { title: "Nhà cung cấp", href: "/suppliers", icon: Truck, roles: ["ADMIN", "PHARMACIST"] },
      { title: "Nhập kho", href: "/inventory/import", icon: PackagePlus, roles: ["ADMIN", "PHARMACIST"] },
      {
        title: "Lịch sử kho",
        href: "/inventory",
        icon: ClipboardList,
        roles: ["ADMIN", "PHARMACIST"],
        isActive: (p) => p === "/inventory",
      },
      {
        title: "Cảnh báo kho",
        href: "/medicines/inventory-alerts",
        icon: AlertTriangle,
        roles: ["ADMIN", "PHARMACIST"],
      },
    ],
  },
  {
    label: "Tài chính",
    items: [{ title: "Quản lý hóa đơn", href: "/invoices", icon: Receipt, roles: ["NURSE"] }],
  },
  {
    label: "Hệ thống",
    items: [
      { title: "Tài khoản nhân sự", href: "/admin/users", icon: Users, roles: ["ADMIN"] },
      { title: "Mô hình AI", href: "/admin/ai-models", icon: BrainCircuit, roles: ["ADMIN"] },
      { title: "Blog", href: "/admin/blog", icon: FileText, roles: ["ADMIN"] },
    ],
  },
];

export const ACCOUNT_ITEM: NavLeaf = {
  title: "Đổi mật khẩu",
  href: "/change-password",
  icon: KeyRound,
  roles: ["ADMIN", "DOCTOR", "NURSE", "PHARMACIST"],
};

/** Groups filtered to a role's visible items — groups with 0 matching items are
 *  dropped entirely rather than rendered with an empty header. */
export function visibleGroupsForRole(role: Role | undefined): NavGroup[] {
  if (!role) return [];
  return NAV_GROUPS.map((group) => ({
    ...group,
    items: group.items.filter((item) => item.roles.includes(role)),
  })).filter((group) => group.items.length > 0);
}

export function isNavItemActive(item: NavLeaf, pathname: string): boolean {
  return item.isActive ? item.isActive(pathname) : pathname.startsWith(item.href);
}
