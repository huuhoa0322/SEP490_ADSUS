import { DoctorAvatar } from "./doctor-avatar";

const DOCTOR = {
  name: "BS. Nguyễn Văn Minh",
  title: "Bác sĩ Chuyên khoa I — Siêu âm Sản khoa",
  experience: "15 năm kinh nghiệm",
  intro:
    "Hơn 15 năm kinh nghiệm trong lĩnh vực siêu âm sản khoa, chuyên khám thai, phát hiện dị tật thai nhi và theo dõi sức khỏe mẹ và bé.",
  clinic: "Phòng khám Siêu âm ADSUS",
};

export function DoctorProfileSection() {
  return (
    <section
      className="px-4 py-16 sm:px-6 lg:px-8"
      style={{ backgroundColor: "var(--lp-surface)" }}
    >
      <div className="mx-auto max-w-6xl">
        {/* Section header */}
        <div className="mb-10 text-center">
          <span
            className="mb-2 inline-block text-xs font-semibold uppercase tracking-widest"
            style={{ color: "var(--lp-teal)" }}
          >
            Đội ngũ bác sĩ
          </span>
          <h2
            className="text-2xl sm:text-3xl"
            style={{
              fontFamily: "var(--lp-font-serif)",
              fontWeight: 600,
              color: "var(--lp-navy)",
            }}
          >
            Gặp gỡ bác sĩ của bạn
          </h2>
        </div>

        {/* Doctor card */}
        <div
          className="mx-auto max-w-2xl overflow-hidden rounded-2xl border shadow-sm"
          style={{
            backgroundColor: "var(--lp-teal-tint)",
            borderColor: "var(--lp-border)",
          }}
        >
          <div className="flex flex-col items-center gap-6 p-8 sm:flex-row sm:items-start sm:p-10">
            {/* Avatar */}
            <div className="shrink-0">
              <DoctorAvatar src="/images/doctor-placeholder.png" alt={DOCTOR.name} />
            </div>

            {/* Info */}
            <div className="text-center sm:text-left">
              <h3
                className="mb-1 text-xl font-semibold"
                style={{
                  fontFamily: "var(--lp-font-serif)",
                  color: "var(--lp-navy)",
                }}
              >
                {DOCTOR.name}
              </h3>
              <p
                className="mb-1 text-sm font-medium"
                style={{ color: "var(--lp-teal)" }}
              >
                {DOCTOR.title}
              </p>
              <p
                className="mb-4 text-xs"
                style={{ color: "var(--lp-muted)", fontFamily: "var(--lp-font-mono)" }}
              >
                {DOCTOR.experience}
              </p>
              <p
                className="text-sm leading-relaxed"
                style={{ color: "var(--lp-muted)" }}
              >
                {DOCTOR.intro}
              </p>
              <p className="mt-3 text-xs" style={{ color: "var(--lp-muted)" }}>
                <span className="font-medium" style={{ color: "var(--lp-navy)" }}>
                  {DOCTOR.clinic}
                </span>
              </p>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
