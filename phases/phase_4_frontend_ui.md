# PHASE 4: PHÁT TRIỂN GIAO DIỆN WEB NEXT.JS SPA (FRONTEND UI/UX)

Phase này hướng dẫn xây dựng giao diện người dùng (UI/UX) cho hệ thống **Nexustock** trên nền tảng Next.js SPA, áp dụng phong cách thiết kế **Fluent Design / WinUI 3** với giao diện tối (Dark Theme) làm chủ đạo, tích hợp cơ chế bảo mật phân quyền hiển thị phía Client, màn hình quản trị Đối tác, Kiểm kê quét mã, Đóng gói nhận số cân tự động, Sơ đồ kho trực quan, Quản lý đợt gom hàng Wave Picking, Sơ đồ cây gia phả truy vết chất lượng, Lịch hẹn bến bãi Dock Door và Dashboard cảnh báo thông minh.

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

### B. Màn hình quản lý đợt gom hàng xuất: `src/app/wave-picking/page.tsx`
Hiển thị danh sách Pick List tối ưu hóa để đi lấy hàng một lần duy nhất cho nhiều đơn hàng:

```tsx
'use client';

import { useState } from 'react';
import { Layers, CheckCircle2 } from 'lucide-react';

interface PickItem {
  locationCode: string;
  productCode: string;
  quantity: number;
  totalLots: number;
}

export default function WavePickingPage() {
  const [pickList] = useState<PickItem[]>([
    { locationCode: 'A-01-01', productCode: 'RESN-01-A', quantity: 150.000, totalLots: 3 },
    { locationCode: 'B-02-05', productCode: 'ALUM-4040', quantity: 20.000, totalLots: 1 },
  ]);

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <h1 className="text-2xl font-bold text-slate-100">Đợt gom hàng xuất (Wave Picking)</h1>
        <span className="px-3 py-1 bg-indigo-500/10 text-indigo-400 text-sm font-medium rounded-full">
          Đợt ID: WAVE-20260630-01
        </span>
      </div>

      <div className="bg-[#18181f] border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
        <h2 className="text-lg font-semibold text-slate-200">Danh sách lấy hàng gom (Pick List) tối ưu</h2>
        <p className="text-xs text-slate-500">Hệ thống tự động gom các Lot cùng loại và cùng vị trí để rút ngắn 90% quãng đường di chuyển.</p>
        
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="border-b border-slate-800 text-slate-400 text-sm font-medium">
                <th className="py-3 px-4">Vị trí kệ lấy</th>
                <th className="py-3 px-4">Mã Vật tư</th>
                <th className="py-3 px-4 text-right">Tổng số lượng cần lấy</th>
                <th className="py-3 px-4 text-center">Tổng số Lot gom</th>
                <th className="py-3 px-4">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/50">
              {pickList.map((item, idx) => (
                <tr key={idx} className="hover:bg-slate-800/20 transition-colors text-sm">
                  <td className="py-3.5 px-4 font-mono text-indigo-400 font-semibold">{item.locationCode}</td>
                  <td className="py-3.5 px-4 font-medium text-slate-300">{item.productCode}</td>
                  <td className="py-3.5 px-4 text-right text-slate-200 font-semibold">{item.quantity.toFixed(3)}</td>
                  <td className="py-3.5 px-4 text-center text-slate-300">{item.totalLots} Lô</td>
                  <td className="py-3.5 px-4">
                    <button className="px-4 py-1.5 bg-indigo-600/10 hover:bg-indigo-600/20 active:bg-indigo-600/30 text-indigo-400 text-xs font-semibold rounded-lg transition-colors">
                      Xác nhận đã lấy
                    </button>
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

### C. Màn hình sơ đồ cây gia phả truy vết chất lượng: `src/app/traceability/page.tsx`
Sử dụng cấu trúc hình cây hiển thị phả hệ các đời Lot để phục vụ kiểm toán sự cố chất lượng:

```tsx
'use client';

import { useState } from 'react';
import { Search, GitFork, ShieldAlert } from 'lucide-react';

export default function GenealogyPage() {
  const [lotInput, setLotInput] = useState('');
  const [showTree, setShowTree] = useState(false);

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-100">Truy vết Gia phả Vật tư (Material Genealogy)</h1>

      {/* Form tra cứu */}
      <form onSubmit={(e) => { e.preventDefault(); if (lotInput) setShowTree(true); }} className="bg-[#18181f] border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
        <label className="block text-sm font-medium text-slate-300">Nhập mã Lot con hoặc Lot cha để truy vết ngược</label>
        <div className="flex gap-4">
          <input
            type="text"
            placeholder="Ví dụ: LOT-KOWAKE-002"
            value={lotInput}
            onChange={(e) => setLotInput(e.target.value)}
            className="flex-1 max-w-sm px-4 py-2 bg-[#0f0f12] border border-slate-800 rounded-lg text-slate-100 focus:outline-none focus:border-indigo-500"
          />
          <button type="submit" className="px-6 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg flex items-center gap-2">
            <Search className="w-4 h-4" /> Truy vết
          </button>
        </div>
      </form>

      {/* Sơ đồ cây Gia phả giả lập */}
      {showTree && (
        <div className="bg-[#18181f] border border-slate-800 rounded-2xl p-6 shadow-xl space-y-6 overflow-x-auto">
          <h2 className="text-lg font-semibold text-slate-200">Sơ đồ phả hệ của Lot: {lotInput}</h2>
          
          <div className="flex flex-col items-center gap-6 min-w-[600px] py-4">
            {/* Cấp 1: Lot cha gốc */}
            <div className="p-4 bg-[#0f0f12] border border-indigo-500 rounded-2xl text-center shadow-lg w-64">
              <span className="text-[10px] uppercase font-bold text-indigo-400">Lot Cha Gốc (Outer Lot)</span>
              <p className="font-mono font-bold text-slate-200 mt-1">LOT-20260630-999</p>
              <p className="text-xs text-slate-500 mt-1">PO: PO-9876543 | Vendor: SUMCO Inc.</p>
              <span className="inline-block px-2 py-0.5 bg-emerald-500/10 text-emerald-400 text-[10px] rounded-full mt-2 font-medium">IQC Pass</span>
            </div>

            <div className="w-0.5 h-8 bg-slate-700 relative">
              <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 bg-[#18181f] text-slate-500 text-[10px] px-1 font-semibold">KOWAKE (Chia tách)</div>
            </div>

            {/* Cấp 2: Các Lot con */}
            <div className="flex gap-12">
              <div className="p-4 bg-[#0f0f12] border border-slate-800 rounded-2xl text-center shadow-lg w-60 relative border-red-500/50">
                <span className="text-[10px] uppercase font-bold text-red-400">Lot Con 1 (Đang chọn)</span>
                <p className="font-mono font-bold text-slate-200 mt-1">{lotInput}</p>
                <p className="text-xs text-slate-500 mt-1">Qty: 25.000 Kg | Vị trí: A-01-02</p>
                <span className="inline-block px-2 py-0.5 bg-red-500/10 text-red-400 text-[10px] rounded-full mt-2 font-medium flex items-center justify-center gap-1"><ShieldAlert className="w-3 h-3" /> QC HOLD</span>
              </div>

              <div className="p-4 bg-[#0f0f12] border border-slate-800 rounded-2xl text-center shadow-lg w-60">
                <span className="text-[10px] uppercase font-bold text-slate-500">Lot Con 2</span>
                <p className="font-mono font-bold text-slate-200 mt-1">LOT-KOWAKE-003</p>
                <p className="text-xs text-slate-500 mt-1">Qty: 25.000 Kg | Vị trí: B-02-01</p>
                <span className="inline-block px-2 py-0.5 bg-emerald-500/10 text-emerald-400 text-[10px] rounded-full mt-2 font-medium">Sẵn sàng</span>
              </div>
            </div>
          </div>
        </div>
      )}
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
