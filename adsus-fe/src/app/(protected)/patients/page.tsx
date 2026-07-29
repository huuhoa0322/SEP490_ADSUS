import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Danh sách bệnh nhân | ADSUS",
};

// SCR-09 — Doctor's patient list (Module 4).
// Placeholder so the Doctor sign-in flow has a destination; the real content belongs to
// whoever owns Module 4.
export default function PatientsPage() {
  return (
    <div className="mx-auto max-w-7xl px-6 py-12">
      <h1 className="font-heading text-[32px] font-bold tracking-[-0.02em] text-foreground">
        Danh sách bệnh nhân
      </h1>
      <p className="mt-2 text-muted-foreground">
        Màn hình dành cho bác sĩ. Nội dung sẽ được xây dựng ở Module 4 — Medical Record.
      </p>
    </div>
  );
}
