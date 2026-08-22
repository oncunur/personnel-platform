import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Personel & İdari İşler",
  description: "Personnel and Administrative Affairs Platform",
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="tr">
      <body>{children}</body>
    </html>
  );
}
