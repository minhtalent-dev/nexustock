"use client";

import React, { useEffect, useState } from "react";
import { Signal, SignalZero } from "lucide-react";

export default function MobileShell({ children }: { children: React.ReactNode }) {
  const [isOnline, setIsOnline] = useState(true);

  useEffect(() => {
    const handleOnline = () => setIsOnline(true);
    const handleOffline = () => setIsOnline(false);

    setIsOnline(navigator.onLine);
    window.addEventListener("online", handleOnline);
    window.addEventListener("offline", handleOffline);

    return () => {
      window.removeEventListener("online", handleOnline);
      window.removeEventListener("offline", handleOffline);
    };
  }, []);

  return (
    <div className="flex flex-col min-h-screen bg-slate-900 text-white select-none max-w-md mx-auto border-x border-slate-800">
      {/* Offline/Online Status Bar */}
      {!isOnline && (
        <div className="bg-red-600 text-center py-1.5 text-xs font-semibold flex items-center justify-center gap-2 animate-pulse">
          <SignalZero className="h-3 w-3" />
          Mất kết nối mạng! Hệ thống chuyển sang lưu offline.
        </div>
      )}
      
      {isOnline && (
        <div className="bg-green-600 text-center py-1.5 text-xs font-semibold flex items-center justify-center gap-2">
          <Signal className="h-3 w-3" />
          Hệ thống trực tuyến
        </div>
      )}

      <header className="bg-slate-800 p-4 flex items-center justify-between border-b border-slate-700">
        <h1 className="text-lg font-bold">NEXUSTOCK Handheld</h1>
        <span className="text-xs bg-slate-700 px-2 py-1 rounded">User: NV-KHO</span>
      </header>

      <main className="flex-1 p-4 overflow-y-auto space-y-4">{children}</main>
    </div>
  );
}
