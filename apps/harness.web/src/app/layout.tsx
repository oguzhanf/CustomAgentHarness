import type { Metadata } from "next";
import { JetBrains_Mono } from "next/font/google";
import "./globals.css";

const mono = JetBrains_Mono({
  variable: "--font-jetbrains-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "CustomAgentHarness · Agent 365",
  description:
    "Workshop reference harness: how a custom enterprise agent runtime registers with Microsoft Agent 365, Entra Agent ID, Microsoft Purview, and Microsoft Foundry.",
  icons: {
    icon: [
      { url: "/harness-mark.svg", type: "image/svg+xml" },
    ],
    shortcut: ["/harness-mark.svg"],
    apple: [{ url: "/harness-mark.svg" }],
  },
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html
      lang="en"
      className={`${mono.variable} h-full antialiased`}
    >
      <body className="min-h-full flex flex-col bg-navy-deep text-ink">
        {children}
      </body>
    </html>
  );
}

