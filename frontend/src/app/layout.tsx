import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import { NextIntlClientProvider } from "next-intl";
import { getLocale, getMessages } from "next-intl/server";
import { Toaster } from "sonner";
import { ConfirmDialogProvider } from "@/lib/confirm-dialog";
import { TooltipProvider } from "@/components/ui/tooltip";
import { AuthProvider } from "@/providers/auth-provider";
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
      className={`${geistSans.variable} ${geistMono.variable} h-full antialiased dark`}
    >
      <body className="min-h-full flex flex-col bg-background">
        <NextIntlClientProvider locale={locale} messages={messages}>
          <ConfirmDialogProvider>
            <TooltipProvider>
              <AuthProvider>
                <AuthGuard>{children}</AuthGuard>
              </AuthProvider>
            </TooltipProvider>
            <Toaster
              position="bottom-right"
              theme="dark"
              closeButton
              richColors
            />
          </ConfirmDialogProvider>
        </NextIntlClientProvider>
      </body>
    </html>
  );
}
