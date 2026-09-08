import type { Metadata } from "next";
import { Geist_Mono, Inter } from "next/font/google";
import "./globals.css";
import "react-hot-toast";

import { QueryProvider } from "@/providers/query-provider";
import { SignalRProvider } from "@/providers/signalr-provider";
import { Toaster } from "react-hot-toast";
import { Analytics } from "@vercel/analytics/next";
import { SpeedInsights } from "@vercel/speed-insights/next";

// One UI typeface for the whole app — headings and body alike (replaces the earlier
// Google Sans Flex pass: unlike that font, Inter has metrics data already baked into
// Next.js, so it gets a proper size-matched fallback with no build warning). Variable
// font (wght 100-900) covers every font-weight utility already used across the
// codebase (font-500, font-600, font-700...) natively. --font-heading and --font-sans
// both point at this one family below (see globals.css) so no component needs to
// change which class it uses.
const inter = Inter({
  variable: "--font-inter",
  subsets: ["latin", "vietnamese"],
  weight: "variable",
});

// Monospace companion — patient IDs, batch numbers, and other fixed-width data
// (see font-mono usages across features/*). Same reasoning as Inter: a real
// variable font with Next.js metrics support, instead of the generic system
// monospace stack.
const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin", "vietnamese"],
  weight: "variable",
});

export const metadata: Metadata = {
  title: "ADSUS",
  description:
    "Hệ thống phát hiện và phân đoạn bất thường trên ảnh siêu âm có hỗ trợ AI.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  const htmlClassName = [inter.variable, geistMono.variable, "h-full", "antialiased"]
    .filter(Boolean)
    .join(" ");

  const bodyClassName = ["min-h-full", "flex", "flex-col"].join(" ");

  return (
    <html lang="vi" className={htmlClassName}>
      <body className={bodyClassName}>
        <QueryProvider>
          <SignalRProvider>{children}</SignalRProvider>
        </QueryProvider>
        <Toaster
          position="bottom-center"
          toastOptions={{
            duration: 4000,
            style: {
              background: "#2E37A4",
              color: "#ffffff",
              borderRadius: "999px",
              padding: "12px 20px",
              fontFamily: "var(--font-inter)",
              fontSize: "14px",
            },
            success: {
              iconTheme: { primary: "#00D3C7", secondary: "#fff" },
            },
            error: {
              iconTheme: { primary: "#F13A66", secondary: "#fff" },
            },
          }}
        />
        <Analytics />
        <SpeedInsights />
      </body>
    </html>
  );
}

