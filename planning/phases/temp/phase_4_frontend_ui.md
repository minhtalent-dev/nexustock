# PHASE 4: PHÁT TRIỂN GIAO DIỆN WEB NEXT.JS SPA (FRONTEND UI/UX)

Phase này hướng dẫn xây dựng giao diện người dùng (UI/UX) cho hệ thống **Nexustock** trên nền tảng Next.js SPA, áp dụng phong cách thiết kế **Fluent Design / WinUI 3** với giao diện tối (Dark Theme) làm chủ đạo, tích hợp cơ chế bảo mật phân quyền hiển thị phía Client, màn hình quản trị Đối tác, Kiểm kê quét mã, Đóng gói nhận số cân tự động, Sơ đồ kho trực quan, Quản lý đợt gom hàng Wave Picking, Sơ đồ cây gia phả truy vết chất lượng, Đóng gói Pallet (LPN), Tiếp nhận hàng trả về RMA, Lịch hẹn bến bãi Dock Door và Dashboard cảnh báo thông minh.

---

## 🎨 1. QUY CHUẨN THIẾT KẾ (DESIGN SYSTEM TOKENS)

Tuân thủ các nguyên tắc thiết kế Fluent Design: sử dụng các lớp mờ (glassmorphism), viền mảnh, bo góc mềm mại và hiệu ứng hover nhẹ nhàng.

### Cấu hình Hệ thống màu (Color Palette)
Chúng ta định nghĩa hệ màu trong tệp Tailwind Config (`tailwind.config.js`):
* **Background chính**: `#0f0f12` (Đen xám sâu mịn, tránh đen tuyền `#000` để đỡ mỏi mắt).
* **Card/Surface**: `#18181f` (Màu nền của các bảng điều khiển, biểu mẫu).
* **Primary / Accent Color**: `#4f46e5` (Indigo / Tím hiện đại) hoặc `#0078d4` (Xanh Fluent mặc định của Microsoft).
* **Success (Đạt chất lượng)**: `#10b981` (Xanh lá Emerald).
* **Warning (Chờ xử lý / Cảnh báo FIFO)**: `#f59e0b` (Vàng hổ phách).
* **Danger (Lỗi / Hold nguyên liệu)**: `#ef4444` (Đỏ tươi).
* **Bo góc (Border Radius)**: 
  * Nút bấm & Input: `8px` (`rounded-lg`).
  * Panels, Cards, Dialogs: `16px` (`rounded-2xl`).

---

## 🏗️ 2. LAYOUT CHUNG & CẤU TRÚC GIAO DIỆN

### Layout chính: `src/app/layout.tsx`
Thiết kế Layout không sử dụng inline style, tách biệt các lớp CSS và cấu trúc thẻ HTML rõ ràng.
```tsx
import '@/app/globals.css';
import Sidebar from '@/components/Sidebar';
import Header from '@/components/Header';
import { AuthProvider } from '@/hooks/useAuth';

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="vi" className="dark">
      <body className="bg-[#0f0f12] text-slate-100 min-h-screen flex antialiased">
        <AuthProvider>
          <Sidebar className="w-64 bg-[#18181f] border-r border-slate-800 flex-shrink-0" />
          <div className="flex-1 flex flex-col min-w-0">
            <Header className="h-16 border-b border-slate-800 bg-[#18181f]/80 backdrop-blur-md sticky top-0 z-50" />
            <main className="flex-1 p-6 overflow-y-auto">
              <div className="max-w-7xl mx-auto space-y-6">
                {children}
              </div>
            </main>
          </div>
        </AuthProvider>
      </body>
    </html>
  );
}
```

---

## 🔒 3. PHÂN QUYỀN HIỂN THỊ TRÊN GIAO DIỆN (CLIENT-SIDE AUTHORIZATION)

### A. React Hook Quản lý Trạng thái Xác thực: `src/hooks/useAuth.tsx`
```tsx
'use client';

import React, { createContext, useContext, useState, useEffect } from 'react';

interface UserContextType {
  user: any;
  permissions: string[];
  login: (token: string) => void;
  logout: () => void;
  hasPermission: (permissionCode: string) => boolean;
}

const AuthContext = createContext<UserContextType | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<any>(null);
  const [permissions, setPermissions] = useState<string[]>([]);

  useEffect(() => {
    const token = localStorage.getItem('token');
    if (token) {
      const decoded = decodeJwt(token);
      setUser(decoded.user);
      setPermissions(decoded.permissions || []);
    }
  }, []);

  const login = (token: string) => {
    localStorage.setItem('token', token);
    const decoded = decodeJwt(token);
    setUser(decoded.user);
    setPermissions(decoded.permissions || []);
  };

  const logout = () => {
    localStorage.removeItem('token');
    setUser(null);
    setPermissions([]);
  };

  const hasPermission = (code: string) => {
    return permissions.includes(code);
  };

  return (
    <AuthContext.Provider value={{ user, permissions, login, logout, hasPermission }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used within an AuthProvider');
  return context;
}

function decodeJwt(token: string) {
  try {
    const base64Url = token.split('.')[1];
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    return JSON.parse(window.atob(base64));
  } catch (e) {
    return {};
  }
}
```

### B. Component Wrapper ẩn/hiện theo quyền: `src/components/HasPermission.tsx`
```tsx
'use client';

import { useAuth } from '@/hooks/useAuth';

interface HasPermissionProps {
  code: string;
  children: React.ReactNode;
  fallback?: React.ReactNode;
}

export default function HasPermission({ code, children, fallback = null }: HasPermissionProps) {
  const { hasPermission } = useAuth();
  
  if (!hasPermission(code)) {
    return <>{fallback}</>;
  }

  return <>{children}</>;
}
```

---

## 📦 4. THIẾT KẾ CÁC THÀNH PHẦN GIAO DIỆN CHÍNH (UI COMPONENTS)

### A. Màn hình quét tiếp nhận Nhập kho & Đề xuất Slotting
```tsx
'use client';

import { useState } from 'react';
import { QrCode, AlertTriangle, CheckCircle, ArrowRightLeft } from 'lucide-react';
import HasPermission from '@/components/HasPermission';

interface PutawayProposal {
  code: string;
}

export default function MaterialAcceptanceForm() {
  const [lotNo, setLotNo] = useState('');
  const [proposals, setProposals] = useState<PutawayProposal[]>([]);
  const [crossDockMatch, setCrossDockMatch] = useState<boolean>(false);

  const handleScanLot = (e: React.FormEvent) => {
    e.preventDefault();
    if (lotNo) {
      setProposals([{ code: 'A-01-01 (Đất - Tầng 1)' }, { code: 'A-01-02 (Đất - Tầng 1)' }, { code: 'B-03-01' }]);
      setCrossDockMatch(true);
    }
  };

  return (
    <div className="space-y-6">
      <form onSubmit={handleScanLot} className="bg-[#18181f] border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
        <h2 className="text-lg font-semibold text-slate-200">Quét Lot nhập kho</h2>
        <div className="flex gap-4">
          <input
            type="text"
            placeholder="Quét mã Lot tại đây..."
            value={lotNo}
            onChange={(e) => setLotNo(e.target.value)}
            className="flex-1 max-w-sm px-4 py-2 bg-[#0f0f12] border border-slate-800 rounded-lg text-slate-100 focus:outline-none focus:border-indigo-500"
          />
          <button type="submit" className="px-6 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg">Quét</button>
        </div>
      </form>

      {crossDockMatch && (
        <div className="bg-amber-500/10 border border-amber-500/30 rounded-2xl p-6 flex justify-between items-center shadow-xl">
          <div className="flex items-center gap-3">
            <ArrowRightLeft className="w-6 h-6 text-amber-400" />
            <div>
              <h3 className="font-semibold text-amber-400">Phát hiện đề xuất Cross-Docking!</h3>
              <p className="text-sm text-slate-400">Mã vật tư này đang có đơn xuất khẩn cấp chờ sẵn. Đề xuất chuyển thẳng ra khu vực đóng gói.</p>
            </div>
          </div>
          <button className="px-5 py-2 bg-amber-600 hover:bg-amber-700 text-white font-medium rounded-lg transition-colors">
            Chuyển tiếp (Cross-dock)
          </button>
        </div>
      )}

      {proposals.length > 0 && (
        <div className="bg-[#18181f] border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
          <h3 className="font-semibold text-slate-200">Vị trí kệ cất hàng tối ưu khuyên dùng</h3>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            {proposals.map((prop, idx) => (
              <div key={idx} className="p-4 bg-[#0f0f12] border border-slate-800 rounded-xl flex items-center justify-between">
                <span className="font-mono text-indigo-400 font-semibold">{prop.code}</span>
                <span className="text-xs text-slate-500 font-medium">Gợi ý {idx + 1}</span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
```

### B. Màn hình quản lý đóng gói Pallet LPN: `src/app/lpn/page.tsx`
Cho phép gộp các Lot hàng lẻ vào Pallet LPN để thực hiện di chuyển vị trí hàng loạt bằng 1 lần quét duy nhất:

```tsx
'use client';

import { useState } from 'react';
import { Box, Scan, ArrowRight } from 'lucide-react';
import HasPermission from '@/components/HasPermission';

export default function LpnPalletPage() {
  const [lpnCode, setLpnCode] = useState('');
  const [scannedLots, setScannedLots] = useState<string[]>([]);
  const [lotInput, setLotInput] = useState('');

  const handleAddLot = (e: React.FormEvent) => {
    e.preventDefault();
    if (lotInput) {
      setScannedLots([...scannedLots, lotInput]);
      setLotInput('');
    }
  };

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-100">Đóng gói Pallet (LPN Management)</h1>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {/* Form đóng gói Pallet */}
        <div className="bg-[#18181f] border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
          <h2 className="text-lg font-semibold text-slate-200">Cấu hình Pallet</h2>
          <div className="space-y-3">
            <label className="block text-sm text-slate-400">Mã Pallet (LPN Code)</label>
            <input
              type="text"
              placeholder="Nhập hoặc quét mã Pallet..."
              value={lpnCode}
              onChange={(e) => setLpnCode(e.target.value)}
              className="w-full max-w-sm px-4 py-2 bg-[#0f0f12] border border-slate-800 rounded-lg text-slate-100 focus:outline-none"
            />
          </div>

          <form onSubmit={handleAddLot} className="space-y-3 pt-4 border-t border-slate-800/50">
            <label className="block text-sm text-slate-400">Quét mã Lot gom vào Pallet</label>
            <div className="flex gap-4">
              <input
                type="text"
                placeholder="Quét mã Lot..."
                value={lotInput}
                onChange={(e) => setLotInput(e.target.value)}
                className="flex-1 max-w-xs px-4 py-2 bg-[#0f0f12] border border-slate-800 rounded-lg text-slate-100 focus:outline-none"
              />
              <button type="submit" className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg">Gộp</button>
            </div>
          </form>
        </div>

        {/* Danh sách Lot trong Pallet */}
        <div className="bg-[#18181f] border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
          <h2 className="text-lg font-semibold text-slate-200">Danh sách Lot đã gộp ({scannedLots.length})</h2>
          <div className="divide-y divide-slate-800/50 max-h-60 overflow-y-auto">
            {scannedLots.map((lot, idx) => (
              <div key={idx} className="py-2.5 flex items-center gap-3 text-sm">
                <Box className="w-4 h-4 text-indigo-400" />
                <span className="font-mono text-slate-300">{lot}</span>
              </div>
            ))}
          </div>

          <div className="flex justify-end pt-4 border-t border-slate-800/50">
            <HasPermission code="lpn.manage">
              <button className="px-6 py-2 bg-emerald-600 hover:bg-emerald-700 text-white font-medium rounded-lg shadow-lg transition-colors">
                Xác nhận hoàn tất Pallet (LPN)
              </button>
            </HasPermission>
          </div>
        </div>
      </div>
    </div>
  );
}
```

### C. Màn hình tiếp nhận hàng trả về RMA: `src/app/rma/page.tsx`
Giao diện quét nhận hàng trả lại và phân loại QC đưa ra quyết định xử lý:

```tsx
'use client';

import { useState } from 'react';
import { ArrowLeftRight, CheckCircle, Ban, RefreshCw } from 'lucide-react';
import HasPermission from '@/components/HasPermission';

interface RmaItem {
  id: string;
  productCode: string;
  quantity: number;
  customerName: string;
}

export default function RmaPage() {
  const [items] = useState<RmaItem[]>([
    { id: '1', productCode: 'RESN-01-A', quantity: 5.000, customerName: 'Intel Corp.' },
  ]);

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-100">Tiếp nhận Hàng trả về (RMA Management)</h1>

      <div className="bg-[#18181f] border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
        <h2 className="text-lg font-semibold text-slate-200">Danh sách sản phẩm khách hàng trả về chờ QC phân loại</h2>
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="border-b border-slate-800 text-slate-400 text-sm font-medium">
                <th className="py-3 px-4">Mã Sản phẩm</th>
                <th className="py-3 px-4">Khách hàng</th>
                <th className="py-3 px-4 text-right">Số lượng trả</th>
                <th className="py-3 px-4 text-center">Phán quyết QC</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/50 text-sm">
              {items.map((item) => (
                <tr key={item.id} className="hover:bg-slate-800/20 transition-colors">
                  <td className="py-3.5 px-4 font-mono text-indigo-400 font-semibold">{item.productCode}</td>
                  <td className="py-3.5 px-4 text-slate-300">{item.customerName}</td>
                  <td className="py-3.5 px-4 text-right text-slate-200">{item.quantity.toFixed(3)}</td>
                  <td className="py-3.5 px-4">
                    <HasPermission code="rma.manage">
                      <div className="flex gap-2 justify-center">
                        <button className="px-3 py-1 bg-emerald-500/10 hover:bg-emerald-500/20 text-emerald-400 text-xs font-semibold rounded-lg flex items-center gap-1 transition-colors">
                          <CheckCircle className="w-3.5 h-3.5" /> Re-stock (Nhập kho)
                        </button>
                        <button className="px-3 py-1 bg-amber-500/10 hover:bg-amber-500/20 text-amber-400 text-xs font-semibold rounded-lg flex items-center gap-1 transition-colors">
                          <RefreshCw className="w-3.5 h-3.5" /> Rework (Sửa)
                        </button>
                        <button className="px-3 py-1 bg-red-500/10 hover:bg-red-500/20 text-red-400 text-xs font-semibold rounded-lg flex items-center gap-1 transition-colors">
                          <Ban className="w-3.5 h-3.5" /> Scrap (Hủy)
                        </button>
                      </div>
                    </HasPermission>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
```

### D. Màn hình đóng gói & tích hợp cân điện tử dự phòng: `src/app/packaging/page.tsx`
Giao diện đóng gói sản phẩm kết nối WebSocket nhận trọng lượng từ cân điện tử, hỗ trợ cơ chế nhập thủ công ghi log khi mất kết nối:

```tsx
'use client';

import { useState, useEffect } from 'react';
import { Scale, AlertTriangle, Lock, ShieldCheck } from 'lucide-react';
import HasPermission from '@/components/HasPermission';

export default function PackagingPage() {
  const [lotNo, setLotNo] = useState('');
  const [weight, setWeight] = useState('0.00');
  const [isManual, setIsManual] = useState(false);
  const [reason, setReason] = useState('');
  const [wsStatus, setWsStatus] = useState('DISCONNECTED');

  useEffect(() => {
    // Giả lập kết nối WebSocket với Local Agent
    const socket = new WebSocket('ws://localhost:9000');
    
    socket.onopen = () => setWsStatus('CONNECTED');
    socket.onclose = () => setWsStatus('DISCONNECTED');
    socket.onmessage = (event) => {
      if (!isManual) {
        setWeight(event.data); // Nhận cân nặng thời gian thực từ cân điện tử
      }
    };

    return () => socket.close();
  }, [isManual]);

  const handlePack = (e: React.FormEvent) => {
    e.preventDefault();
    if (isManual && !reason) {
      alert('Bắt buộc nhập lý do cân tay!');
      return;
    }
    // Gửi API đóng gói
    console.log('Packing request:', { lotNo, weight, isManual, reason });
  };

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-100">Đóng gói hàng xuất</h1>

      <div className="max-w-2xl bg-[#18181f] border border-slate-800 rounded-2xl p-6 shadow-xl space-y-6">
        {/* Trạng thái kết nối cân */}
        <div className="flex justify-between items-center bg-[#0f0f12] p-4 rounded-xl border border-slate-800">
          <div className="flex items-center gap-3">
            <Scale className={`w-5 h-5 ${wsStatus === 'CONNECTED' ? 'text-emerald-400' : 'text-rose-500'}`} />
            <div>
              <h3 className="font-semibold text-slate-200">Cân điện tử</h3>
              <p className="text-xs text-slate-500">Kết nối: {wsStatus}</p>
            </div>
          </div>
          
          {wsStatus === 'DISCONNECTED' && !isManual && (
            <div className="flex items-center gap-2 text-amber-500 text-xs font-semibold bg-amber-500/10 px-3 py-1.5 rounded-lg">
              <AlertTriangle className="w-4 h-4" /> Mất kết nối cân
            </div>
          )}
        </div>

        <form onSubmit={handlePack} className="space-y-4">
          <div className="space-y-2">
            <label className="block text-sm text-slate-400">Mã Lot quét xuất</label>
            <input
              type="text"
              placeholder="Quét mã Lot..."
              value={lotNo}
              onChange={(e) => setLotNo(e.target.value)}
              className="w-full px-4 py-2 bg-[#0f0f12] border border-slate-800 rounded-lg text-slate-100 focus:outline-none"
            />
          </div>

          <div className="space-y-2">
            <label className="block text-sm text-slate-400">Trọng lượng (kg)</label>
            <div className="flex gap-4">
              <input
                type="text"
                disabled={!isManual}
                value={weight}
                onChange={(e) => setWeight(e.target.value)}
                className="flex-1 px-4 py-2 bg-[#0f0f12] border border-slate-800 rounded-lg text-slate-100 font-mono text-xl focus:outline-none disabled:opacity-50"
              />
              
              {!isManual ? (
                <button
                  type="button"
                  onClick={() => setIsManual(true)}
                  className="px-4 py-2 bg-amber-600 hover:bg-amber-700 text-white rounded-lg flex items-center gap-1.5"
                >
                  <Lock className="w-4 h-4" /> Nhập cân tay
                </button>
              ) : (
                <button
                  type="button"
                  onClick={() => { setIsManual(false); setReason(''); }}
                  className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg"
                >
                  Dùng cân tự động
                </button>
              )}
            </div>
          </div>

          {isManual && (
            <div className="space-y-2 pt-2 border-t border-slate-800/50">
              <label className="block text-sm text-slate-400 flex items-center gap-1.5 text-amber-500">
                <ShieldCheck className="w-4 h-4" /> Lý do nhập cân tay thủ công (Yêu cầu Manager phê duyệt)
              </label>
              <textarea
                placeholder="Nhập lý do chi tiết..."
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                className="w-full px-4 py-2 bg-[#0f0f12] border border-slate-800 rounded-lg text-slate-100 focus:outline-none h-20 text-sm"
              />
            </div>
          )}

          <div className="flex justify-end pt-4 border-t border-slate-800/50">
            <button type="submit" className="px-6 py-2 bg-emerald-600 hover:bg-emerald-700 text-white font-medium rounded-lg shadow-lg">
              Xác nhận đóng gói
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
```

### E. Màn hình quản lý đóng băng kệ kiểm kê: `src/app/stocktake/lock/page.tsx`
Giao diện quản lý hỗ trợ đóng băng (khóa) các vị trí kệ và vùng kho để đảm bảo tính chính xác số liệu trong suốt quá trình kiểm đếm thực tế:

```tsx
'use client';

import { useState } from 'react';
import { ShieldAlert, Unlock, Lock } from 'lucide-react';
import HasPermission from '@/components/HasPermission';

interface StorageLocation {
  id: string;
  code: string;
  zoneName: string;
  isLocked: boolean;
}

export default function StocktakeLockPage() {
  const [locations, setLocations] = useState<StorageLocation[]>([
    { id: '1', code: 'A-01-01', zoneName: 'Khu A - Nhiệt độ phòng', isLocked: false },
    { id: '2', code: 'A-01-02', zoneName: 'Khu A - Nhiệt độ phòng', isLocked: true },
    { id: '3', code: 'B-03-01', zoneName: 'Khu B - Kho lạnh', isLocked: false },
  ]);

  const toggleLockLocation = (id: string) => {
    setLocations(locations.map(loc => 
      loc.id === id ? { ...loc, isLocked: !loc.isLocked } : loc
    ));
  };

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <h1 className="text-2xl font-bold text-slate-100">Đóng băng vị trí (Kiểm kê định kỳ)</h1>
        <div className="flex items-center gap-2 text-rose-400 text-xs font-semibold bg-rose-500/10 px-3 py-1.5 rounded-lg border border-rose-500/20">
          <ShieldAlert className="w-4 h-4" /> Vị trí bị khóa sẽ chặn mọi thao tác Nhập/Xuất/Di chuyển
        </div>
      </div>

      <div className="bg-[#18181f] border border-slate-800 rounded-2xl p-6 shadow-xl">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="border-b border-slate-800 text-slate-400 text-sm font-medium">
                <th className="py-3 px-4">Mã Vị trí kệ</th>
                <th className="py-3 px-4">Vùng kho bảo quản</th>
                <th className="py-3 px-4 text-center">Trạng thái khóa</th>
                <th className="py-3 px-4 text-right">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/50 text-sm">
              {locations.map((loc) => (
                <tr key={loc.id} className="hover:bg-slate-800/20 transition-colors">
                  <td className="py-3.5 px-4 font-mono text-indigo-400 font-semibold">{loc.code}</td>
                  <td className="py-3.5 px-4 text-slate-300">{loc.zoneName}</td>
                  <td className="py-3.5 px-4 text-center">
                    <span className={`px-2.5 py-1 rounded-full text-xs font-semibold ${loc.isLocked ? 'bg-red-500/10 text-red-400 border border-red-500/20' : 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20'}`}>
                      {loc.isLocked ? 'Đã khóa' : 'Sẵn sàng'}
                    </span>
                  </td>
                  <td className="py-3.5 px-4 text-right">
                    <HasPermission code="stocktake.manage">
                      <button
                        onClick={() => toggleLockLocation(loc.id)}
                        className={`px-3 py-1.5 text-xs font-semibold rounded-lg flex items-center gap-1.5 ml-auto transition-colors ${loc.isLocked ? 'bg-emerald-600 hover:bg-emerald-700 text-white' : 'bg-red-600 hover:bg-red-700 text-white'}`}
                      >
                        {loc.isLocked ? <Unlock className="w-3.5 h-3.5" /> : <Lock className="w-3.5 h-3.5" />}
                        {loc.isLocked ? 'Mở khóa' : 'Đóng băng'}
                      </button>
                    </HasPermission>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
```

---

## 📝 5. QUY TRÌNH KIỂM TRA GIAO DIỆN (UI VERIFICATION)

1. **Kiểm tra Phân quyền**: Đăng nhập bằng tài khoản Operator $\rightarrow$ Kiểm tra không nhìn thấy nút "Phê duyệt & Tự động cân bằng kho". Đăng nhập bằng tài khoản Manager $\rightarrow$ Nút hiển thị đầy đủ và hoạt động.
2. **Kiểm tra Responsive**: Co giãn trình duyệt về kích thước 1280px để đảm bảo Grid và Table hiển thị đầy đủ thông tin.
3. **Kiểm tra Hiệu ứng Dynamic**: Đảm bảo các nút bấm có trạng thái `hover` mịn màng, các thông báo lỗi hiển thị bằng Toast popup dạng slide-in mượt mà.
4. **Tuân thủ quy tắc UI Text**: Viết hoa dạng Sentence case (Ví dụ: "Kiểm kê định kỳ", "Danh sách Lot chờ duyệt").
