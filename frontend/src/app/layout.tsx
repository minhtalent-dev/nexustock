import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import { NextIntlClientProvider } from "next-intl";
import { getLocale, getMessages } from "next-intl/server";
import { ConfirmDialogProvider } from "@/lib/confirm-dialog";
import { TooltipProvider } from "@/components/ui/tooltip";
import { AuthProvider } from "@/providers/auth-provider";
import { ThemeProvider } from "@/providers/theme-provider";
import { ThemeAwareToaster } from "@/components/theme-aware-toaster";
import AuthGuard from "@/components/auth-guard";
import "./globals.css";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "Nexustock",
  description: "Nexustock warehouse management system",
};

export default async function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  const locale = await getLocale();
  const messages = await getMessages();

  return (
    <html
      lang={locale}
      suppressHydrationWarning
      className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`}
    >
      <body className="flex min-h-full flex-col bg-background">
        <NextIntlClientProvider locale={locale} messages={messages}>
          <ThemeProvider>
            <ConfirmDialogProvider>
              <TooltipProvider>
                <AuthProvider>
                  <AuthGuard>{children}</AuthGuard>
                </AuthProvider>
              </TooltipProvider>
              <ThemeAwareToaster />
            </ConfirmDialogProvider>
          </ThemeProvider>
        </NextIntlClientProvider>
      </body>
    </html>
  );
}
