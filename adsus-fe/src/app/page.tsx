import type { Metadata } from "next";
import { LandingNavbar } from "@/components/landing/navbar";
import { LandingFooter } from "@/components/landing/footer";
import { HeroSection } from "@/components/landing/hero-section";
import { DoctorProfileSection } from "@/components/landing/doctor-profile-section";
import { ServicesSection } from "@/components/landing/services-section";
import { BlogPreviewSection } from "@/components/landing/blog-preview-section";
import { CTASection } from "@/components/landing/cta-section";

export const metadata: Metadata = {
  title: "ADSUS — Phòng khám Siêu âm",
  description:
    "Hệ thống phát hiện và phân đoạn bất thường trên ảnh siêu âm có hỗ trợ AI.",
};

export default function RootPage() {
  return (
    <div className="flex min-h-screen flex-col">
      <LandingNavbar />
      <main className="flex-1">
        <HeroSection />
        <DoctorProfileSection />
        <ServicesSection />
        <BlogPreviewSection />
        <CTASection />
      </main>
      <LandingFooter />
    </div>
  );
}
