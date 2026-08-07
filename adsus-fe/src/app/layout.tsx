import type { Metadata } from "next";
import { Exo, Roboto } from "next/font/google";
import "./globals.css";

import { QueryProvider } from "@/providers/query-provider";
import { Toaster } from "react-hot-toast";

// Same fonts as the team's Medizco template:
//   Exo    for headings (style.css line 100)
//   Roboto for body    (style.css line 78)
const exo = Exo({
  variable: "--font-exo",
  subsets: ["latin"],
  weight: ["400", "500", "600", "700", "800"],
});

const roboto = Roboto({
  variable: "--font-roboto",
  subsets: ["latin"],
  weight: ["300", "400", "500", "700"],
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
  const htmlClassName = [exo.variable, roboto.variable, "h-full", "antialiased"]
    .filter(Boolean)
    .join(" ");

  const bodyClassName = ["min-h-full", "flex", "flex-col"].join(" ");

  return (
    <html lang="vi" className={htmlClassName}>
      <body className={bodyClassName}>
        <QueryProvider>{children}</QueryProvider>
        <Toaster position="top-right" />
      </body>
    </html>
  );
}
