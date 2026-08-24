import type { Metadata } from "next";
import "./globals.css";
import { AppFrame } from "./components/AppFrame";

export const metadata: Metadata = {
  title: "Personel & İdari İşler Platformu",
  description: "Personel, bordro ve idari operasyonlar için kurumsal yönetim platformu",
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="tr">
      <body><AppFrame>{children}</AppFrame></body>
    </html>
  );
}
