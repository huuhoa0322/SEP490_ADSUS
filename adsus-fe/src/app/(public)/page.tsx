import { HeroSection } from "@/components/landing/hero-section";
import { DoctorProfileSection } from "@/components/landing/doctor-profile-section";
import { ServicesSection } from "@/components/landing/services-section";
import { BlogPreviewSection } from "@/components/landing/blog-preview-section";
import { CTASection } from "@/components/landing/cta-section";

export default function LandingPage() {
  return (
    <>
      <HeroSection />
      <DoctorProfileSection />
      <ServicesSection />
      <BlogPreviewSection />
      <CTASection />
    </>
  );
}
