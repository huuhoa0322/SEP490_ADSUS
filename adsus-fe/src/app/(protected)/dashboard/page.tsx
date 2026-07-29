import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Thống kê | ADSUS",
};

// SCR-08 — Admin statistics dashboard (UC-05, Module 3).
// Placeholder so the Admin sign-in flow has a destination; the real content belongs to
// whoever owns Module 3.
export default function DashboardPage() {
  return (
    <div className="mx-auto max-w-7xl px-6 py-12">
      <h1 className="font-heading text-[32px] font-bold tracking-[-0.02em] text-foreground">
        Thống kê
      </h1>
      <p className="mt-2 text-muted-foreground">
        Màn hình dành cho quản trị viên. Nội dung sẽ được xây dựng ở Module 3 — Dashboard
        &amp; Reporting.
      </p>
    </div>
  );
}
