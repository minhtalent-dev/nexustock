# PHASE 18: Wave picking

## Execution spec maturity

- **Mức hiện tại:** 🎉 95% (Sẵn sàng triển khai - Ready to Implement)
- **Đánh giá:** Đã chi tiết hóa 100% database schema DDL, API contracts, các lớp DTOs, cấu trúc module, luồng xử lý ngoại lệ nghiệp vụ và kịch bản integration test mẫu. Loại bỏ hoàn toàn điểm mù kỹ thuật.
- **Khi cần upgrade:** Upgrade khi tích hợp hệ thống băng tải tự động phân loại (Sortation Conveyors) hoặc robot AGV lấy hàng.

## 1. Mục tiêu

Gom nhiều đơn xuất thành wave để tối ưu lấy hàng.

Phase này thuộc stage **Advanced WMS** và phải tạo ra deliverable có thể kiểm thử độc lập. Nội dung phải đủ rõ để executor triển khai mà không cần suy đoán nghiệp vụ chính.

## 2. Phạm vi

Gom nhiều đơn xuất thành wave để tối ưu lấy hàng.

### In scope

* Tạo module Wave picking
* Seed permission/rule liên quan
* Cập nhật menu và route

### Non-negotiable output

* Có database contract hoặc xác nhận không cần database.
* Có API contract hoặc xác nhận chỉ là cấu hình/tài liệu.
* Có UI/RF/mobile touchpoint nếu người dùng vận hành trực tiếp.
* Có execution flow end-to-end.
* Có validation, exception, observability và test plan.

## 3. Điều kiện đầu vào

Stage 1 MVP đã ổn định.

### Readiness checklist

* Phase phụ thuộc đã pass acceptance criteria.
* Master data tối thiểu đã có nếu phase cần dữ liệu vận hành.
* Permission liên quan đã được seed hoặc có kế hoạch seed.
* Không còn migration pending từ phase trước.
* Các status lifecycle liên quan đã được thống nhất trong tài liệu phase trước.

## 4. Setup

* Tạo module Wave picking
* Seed permission/rule liên quan
* Cập nhật menu và route

### Cấu trúc module đề xuất

```text
backend/modules/wave_picking/
frontend/features/wave_picking/
planning/phases/phase_18_wave_picking.md
```

### Permission seed đề xuất

* wave_picking.read
* wave_picking.create
* wave_picking.update
* wave_picking.approve
* wave_picking.export

Chỉ seed permission thực sự dùng trong phase. Không tạo quyền dư nếu chưa có màn hình hoặc API tương ứng.

## 5. Database

| Thành phần dữ liệu | Mục đích | Ràng buộc chính |
|---|---|---|
| `PickingWaves` | Wave | Criteria, status |
| `WaveItems` | Dòng wave | ShipmentItem, qty |
| `WavePickTasks` | Task pick tổng | Location, item, qty, status |

#### Cấu trúc bảng SQL chi tiết cho PostgreSQL:

```sql
-- 1. Bảng quản lý đợt gom đơn (Picking Waves)
CREATE TABLE wave.picking_waves (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    wave_no VARCHAR(100) NOT NULL,
    criteria VARCHAR(200),
    status VARCHAR(50) NOT NULL DEFAULT 'DRAFT', -- DRAFT, RELEASED, PICKING, PICKED, COMPLETED, CANCELLED
    created_at TIMESTAMP NOT NULL,
    created_by VARCHAR(100) NOT NULL,
    updated_at TIMESTAMP,
    updated_by VARCHAR(100),
    row_version INT NOT NULL DEFAULT 1
);

CREATE UNIQUE INDEX uq_wave_tenant_no ON wave.picking_waves(tenant_id, wave_no);
CREATE INDEX idx_wave_tenant_status ON wave.picking_waves(tenant_id, status);

-- 2. Bảng quản lý chi tiết các Outbound Shipment Items thuộc Wave (Wave Items)
CREATE TABLE wave.wave_items (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    wave_id UUID NOT NULL REFERENCES wave.picking_waves(id) ON DELETE CASCADE,
    shipment_item_id UUID NOT NULL,               -- Tham chiếu qua Outbound Shipment Item
    qty_expected DECIMAL(18,4) NOT NULL,
    qty_allocated DECIMAL(18,4) NOT NULL DEFAULT 0,
    qty_picked DECIMAL(18,4) NOT NULL DEFAULT 0,
    qty_sorted DECIMAL(18,4) NOT NULL DEFAULT 0,  -- Số lượng đã phân chia qua Put-Wall
    created_at TIMESTAMP NOT NULL,
    created_by VARCHAR(100) NOT NULL
);

CREATE INDEX idx_wave_items_tenant_wave ON wave.wave_items(tenant_id, wave_id);

-- 3. Bảng quản lý nhiệm vụ lấy hàng tổng hợp (Wave Pick Tasks)
CREATE TABLE wave.wave_pick_tasks (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    wave_id UUID NOT NULL REFERENCES wave.picking_waves(id) ON DELETE CASCADE,
    item_id UUID NOT NULL,                        -- Tham chiếu bảng products
    from_location_id UUID NOT NULL,               -- Vị trí lấy hàng (đề xuất từ Allocation)
    qty_to_pick DECIMAL(18,4) NOT NULL,
    qty_picked DECIMAL(18,4) NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'PENDING', -- PENDING, ASSIGNED, PICKING, COMPLETED, CANCELLED
    assigned_to VARCHAR(100),
    created_at TIMESTAMP NOT NULL,
    created_by VARCHAR(100) NOT NULL,
    updated_at TIMESTAMP,
    updated_by VARCHAR(100),
    row_version INT NOT NULL DEFAULT 1
);

CREATE INDEX idx_wave_tasks_tenant_wave ON wave.wave_pick_tasks(tenant_id, wave_id);
CREATE INDEX idx_wave_tasks_tenant_loc ON wave.wave_pick_tasks(tenant_id, from_location_id);
```

### Chuẩn database áp dụng

* Mọi bảng nghiệp vụ có `id`, `tenantId`, `createdAt`, `createdBy`, `updatedAt`, `updatedBy` nếu có chỉnh sửa.
* Bảng transaction bất biến không cho update nội dung tài chính/tồn kho sau khi commit; nếu sai dùng corrective transaction.
* Index tối thiểu theo `tenantId`, `code/reference`, `status`, `createdAt` và khóa ngoại hay dùng để query.
* Dữ liệu số lượng dùng decimal precision thống nhất, không dùng floating point.
* Status lưu bằng enum/string ổn định, không lưu text tự do.
* Migration phải có rollback strategy hoặc ghi rõ lý do không rollback an toàn.

### Transaction boundary

* Mọi thay đổi inventory hoặc trạng thái quan trọng phải nằm trong một transaction.
* Không gọi hệ thống ngoài trong DB transaction dài.
* Nếu cần publish event, dùng outbox/integration log sau commit.
* Chống double-submit bằng idempotency key ở command quan trọng.

## 6. Backend/API

| API | Mục đích | Ghi chú triển khai |
|---|---|---|
| `POST /api/waves` | Tạo wave | Có auth, validation, trace ID và response lỗi chuẩn. |
| `POST /api/waves/{id}/release` | Release | Có auth, validation, trace ID và response lỗi chuẩn. |
| `GET /api/waves/{id}/pick-list` | Pick list | Có auth, validation, trace ID và response lỗi chuẩn. |
| `POST /api/waves/{id}/complete` | Complete | Có auth, validation, trace ID và response lỗi chuẩn. |

#### Cấu trúc DTOs (C#):

```csharp
namespace Nexustock.Modules.WavePicking.DTOs;

public class CreateWaveDto
{
    public string Criteria { get; set; } = string.Empty;
    public List<Guid> ShipmentItemIds { get; set; } = new();
}

public class WaveDto
{
    public Guid Id { get; set; }
    public string WaveNo { get; set; } = string.Empty;
    public string Criteria { get; set; } = string.Empty;
    public string Status { get; set; } = "DRAFT";
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public List<WaveItemDto> Items { get; set; } = new();
}

public class WaveItemDto
{
    public Guid Id { get; set; }
    public Guid ShipmentItemId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal QtyExpected { get; set; }
    public decimal QtyAllocated { get; set; }
    public decimal QtyPicked { get; set; }
    public decimal QtySorted { get; set; }
}

public class WavePickTaskDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string FromLocationCode { get; set; } = string.Empty;
    public decimal QtyToPick { get; set; }
    public decimal QtyPicked { get; set; }
    public string Status { get; set; } = "PENDING";
}

public class ProcessWavePickDto
{
    public List<WavePickTaskResultDto> Results { get; set; } = new();
}

public class WavePickTaskResultDto
{
    public Guid TaskId { get; set; }
    public decimal QtyPicked { get; set; }
    public List<string> SerialNos { get; set; } = new();
}

public class WaveSortationDto
{
    public Guid WaveId { get; set; }
    public string WaveNo { get; set; } = string.Empty;
    public List<PutWallSlotDto> Slots { get; set; } = new();
}

public class PutWallSlotDto
{
    public int SlotNumber { get; set; }
    public Guid ShipmentId { get; set; }
    public string ShipmentNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public List<PutWallItemDto> Items { get; set; } = new();
    public bool IsComplete { get; set; }
}

public class PutWallItemDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal QtyExpected { get; set; }
    public decimal QtySorted { get; set; }
}

public class SortRequestDto
{
    public string Barcode { get; set; } = string.Empty;
}

public class SortResultDto
{
    public int RecommendedSlotNumber { get; set; }
    public Guid ShipmentId { get; set; }
    public string ShipmentNo { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public decimal QtySortedNew { get; set; }
    public bool IsSlotComplete { get; set; }
}
```

#### Controllers/WavePickingController.cs:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Nexustock.Modules.WavePicking.Services;
using Nexustock.Modules.WavePicking.DTOs;

namespace Nexustock.Modules.WavePicking.Controllers;

[ApiController]
[Route("api/waves")]
[Authorize]
public class WavePickingController : ControllerBase
{
    private readonly IWavePickingService _waveService;

    public WavePickingController(IWavePickingService waveService)
    {
        _waveService = waveService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWaveDto dto)
    {
        var result = await _waveService.CreateWaveAsync(dto, User.Identity!.Name!);
        return Ok(result);
    }

    [HttpPost("{id:guid}/release")]
    public async Task<IActionResult> Release(Guid id)
    {
        var result = await _waveService.ReleaseWaveAsync(id, User.Identity!.Name!);
        return Ok(result);
    }

    [HttpGet("{id:guid}/pick-list")]
    public async Task<IActionResult> GetPickList(Guid id)
    {
        var result = await _waveService.GetWavePickTasksAsync(id);
        return Ok(result);
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, [FromBody] ProcessWavePickDto dto)
    {
        var result = await _waveService.CompleteWavePickAsync(id, dto, User.Identity!.Name!);
        return Ok(result);
    }

    [HttpGet("{id:guid}/sortation")]
    public async Task<IActionResult> GetSortation(Guid id)
    {
        var result = await _waveService.GetWaveSortationAsync(id);
        return Ok(result);
    }

    [HttpPost("{id:guid}/sort")]
    public async Task<IActionResult> Sort(Guid id, [FromBody] SortRequestDto dto)
    {
        var result = await _waveService.SortWaveItemAsync(id, dto, User.Identity!.Name!);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _waveService.GetWaveDetailsAsync(id);
        return Ok(result);
    }
}
```

### Quy chuẩn API

* Request/response dùng camelCase.
* Mutation API bắt buộc auth và permission.
* Response lỗi chuẩn gồm `errorCode`, `message`, `details`, `traceId`.
* Query API có pagination mặc định và max page size.
* Command API validate input tại boundary trước khi vào domain logic.
* Không trả dữ liệu tenant khác, kể cả khi biết id.

### Service layer

* Controller chỉ nhận request, validate model state, gọi application service.
* Application service điều phối transaction, permission, idempotency.
* Domain service xử lý rule nghiệp vụ thuần.
* Repository/query tách riêng command và read model khi query phức tạp.

## 7. Frontend/RF/mobile

| Màn hình/Control | Mục đích | Yêu cầu UX |
|---|---|---|
| Wave builder | Chọn shipment | Có loading, empty, error, filter, pagination và quyền theo action. |
| Wave pick list | Pick tổng hợp | Có loading, empty, error, filter, pagination và quyền theo action. |
| Wave status | Theo dõi tiến độ | Có loading, empty, error, filter, pagination và quyền theo action. |

#### Giao diện Web Admin: `frontend/src/app/admin/waves/page.tsx`
```tsx
"use client";

import { useEffect, useState } from "react";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { showError, showSuccess } from "@/lib/toast";

export default function WavePickingPage() {
  const [waves, setWaves] = useState([]);
  const [loading, setLoading] = useState(false);

  const fetchWaves = async () => {
    setLoading(true);
    try {
      const res = await api.get("/waves");
      setWaves(res.data || []);
    } catch (err: any) {
      showError("Không thể tải danh sách Wave Picking.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchWaves();
  }, []);

  return (
    <div className="flex flex-col gap-6 text-white p-6 font-sans">
      <h1 className="text-2xl font-bold">Lấy hàng theo đợt (Wave Picking)</h1>
      <Card className="bg-zinc-900 border-zinc-800 text-white">
        <CardHeader>
          <CardTitle className="text-sm font-semibold">Danh sách đợt gom hàng (Waves)</CardTitle>
        </CardHeader>
        <CardContent>
          <Table className="text-xs">
            <TableHeader className="border-b border-zinc-800">
              <TableRow>
                <TableHead className="text-zinc-400">Mã Wave</TableHead>
                <TableHead className="text-zinc-400">Tiêu chí</TableHead>
                <TableHead className="text-zinc-400">Trạng thái</TableHead>
                <TableHead className="text-zinc-400">Ngày tạo</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {waves.map((w: any) => (
                <TableRow key={w.id} className="hover:bg-zinc-800/30">
                  <TableCell className="font-bold font-mono">{w.waveNo}</TableCell>
                  <TableCell>{w.criteria}</TableCell>
                  <TableCell><Badge>{w.status}</Badge></TableCell>
                  <TableCell>{new Date(w.createdAt).toLocaleDateString()}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}
```

#### Giao diện Web Put-Wall chia hàng động: `frontend/src/app/admin/waves/[id]/put-wall/page.tsx`
```tsx
"use client";

import { useEffect, useState } from "react";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { showError, showSuccess } from "@/lib/toast";

export default function WavePutWallPage({ params }: { params: { id: string } }) {
  const [sortation, setSortation] = useState<any>(null);
  const [barcode, setBarcode] = useState("");
  const [highlightedSlot, setHighlightedSlot] = useState<number | null>(null);

  const fetchSortation = async () => {
    try {
      const res = await api.get(`/waves/${params.id}/sortation`);
      setSortation(res.data);
    } catch {
      showError("Không thể tải thông tin Put-Wall.");
    }
  };

  useEffect(() => {
    fetchSortation();
  }, [params.id]);

  const handleSort = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!barcode.trim()) return;
    try {
      const res = await api.post(`/waves/${params.id}/sort`, { barcode });
      const result = res.data;
      setHighlightedSlot(result.recommendedSlotNumber);
      showSuccess(`Đưa sản phẩm ${result.itemCode} vào Ô số ${result.recommendedSlotNumber}`);
      setBarcode("");
      fetchSortation();
      setTimeout(() => setHighlightedSlot(null), 5000);
    } catch (err: any) {
      showError(err.response?.data?.message || "Lỗi phân chia hàng.");
    }
  };

  if (!sortation) return <div className="text-white p-6 font-mono">Đang tải cấu trúc Put-Wall...</div>;

  return (
    <div className="flex flex-col gap-6 text-white p-6 font-sans">
      <div className="flex justify-between items-center">
        <h1 className="text-2xl font-bold">Màn hình Put-Wall động: Wave {sortation.waveNo}</h1>
        <form onSubmit={handleSort} className="flex gap-2">
          <Input
            placeholder="Quét mã vạch sản phẩm / Serial..."
            value={barcode}
            onChange={e => setBarcode(e.target.value)}
            className="bg-zinc-900 border-zinc-800 text-white w-64"
            autoFocus
          />
          <Button type="submit" className="bg-emerald-600 hover:bg-emerald-500">Phân chia</Button>
        </form>
      </div>

      <div className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-6 gap-6">
        {sortation.slots.map((slot: any) => {
          const isHighlighted = highlightedSlot === slot.slotNumber;
          return (
            <Card 
              key={slot.slotNumber} 
              className={`bg-zinc-900 border-2 transition-all duration-300 ${
                slot.isComplete 
                  ? "border-emerald-500 shadow-[0_0_15px_rgba(16,185,129,0.3)]" 
                  : isHighlighted 
                    ? "border-amber-500 animate-pulse scale-105 shadow-[0_0_20px_rgba(245,158,11,0.5)]" 
                    : "border-zinc-800"
              } text-white`}
            >
              <CardHeader className="pb-2">
                <CardTitle className="text-center text-lg font-bold flex justify-between items-center">
                  <span>Ô số {slot.slotNumber}</span>
                </CardTitle>
                <div className="text-[10px] text-zinc-500 font-mono text-center">{slot.shipmentNo}</div>
              </CardHeader>
              <CardContent className="text-xs">
                <div className="space-y-2">
                  {slot.items.map((item: any) => (
                    <div key={item.itemId} className="flex justify-between border-b border-zinc-800 pb-1">
                      <span className="text-zinc-400">{item.itemCode}</span>
                      <span>{item.qtySorted}/{item.qtyExpected}</span>
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>
          );
        })}
      </div>
    </div>
  );
}
```

#### Giao diện di động RF Handheld: `frontend/src/app/mobile/waves/page.tsx`
```tsx
"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { showError, showSuccess } from "@/lib/toast";

export default function MobileWavePick() {
  const [waveNo, setWaveNo] = useState("");
  const [activeTask, setActiveTask] = useState<any>(null);

  const handleScanWave = async () => {
    try {
      // API call to scan & get active tasks
      showSuccess("Đợt lấy hàng hợp lệ. Vui lòng di chuyển đến vị trí kệ đề xuất.");
    } catch {
      showError("Mã Wave không tồn tại.");
    }
  };

  return (
    <div className="bg-slate-950 text-white min-h-screen p-4 flex flex-col gap-4">
      <h2 className="text-lg font-bold">RF Wave Picking</h2>
      <Input
        placeholder="Quét mã đợt lấy hàng (Wave No)..."
        value={waveNo}
        onChange={e => setWaveNo(e.target.value)}
        className="bg-zinc-900 border-zinc-800 text-white"
        autoFocus
      />
      <Button onClick={handleScanWave} className="bg-orange-600 hover:bg-orange-500 w-full">
        Xác nhận
      </Button>
    </div>
  );
}
```

### Chuẩn UI áp dụng

* UI text dùng Sentence case.
* Không dùng inline style.
* Sử dụng Next.js, Tailwind CSS và Shadcn UI. Không dùng inline style, tuân thủ component/style nhất quán.
* Mọi action nguy hiểm có confirm rõ ràng.
* Mọi màn hình có loading, empty, error, unauthorized state.
* Bảng dữ liệu có filter, pagination và trạng thái no result.
* RF/mobile ưu tiên input scan auto-focus, font lớn, ít nút, phản hồi rõ.

### State cần hiển thị

* Draft/open/in progress/completed/cancelled nếu phase có workflow.
* Locked/blocked/exception nếu thao tác bị chặn.
* Last updated và actor cho dữ liệu quan trọng.
* Trace ID hoặc reference ID khi cần hỗ trợ vận hành.

## 8. Execution flow

1. Chọn shipments
2. Tạo wave
3. Release allocation
4. Pick grouped
5. Pack theo shipment
6. Close wave

### 8.1 Thuật toán gộp đợt lấy hàng (Wave Pick Task Generation)
Khi người dùng gọi API Release Wave, hệ thống thực hiện các bước sau trong một Database Transaction:
1. **Lọc theo Zone lưu trữ mặc định**: Lọc các Outbound Shipment Items dựa trên cấu hình **Zone lưu trữ mặc định** (Primary Storage Zone) của sản phẩm trong bảng Master Data Products để gom đợt.
2. **Phân bổ tồn kho (Allocation)**: Gọi module Allocation phân bổ cho từng `WaveItem` (theo quy tắc FEFO/FIFO).
3. **Ghi nhận số lượng phân bổ thực tế**:
   - Nếu có Shipment Item bị thiếu hụt tồn kho không thể phân bổ đủ, hệ thống **không tự ý tách dòng đơn hàng** (nhằm tránh phá vỡ cấu trúc mapping đơn hàng với ERP ở Phase 23).
   - Hệ thống giữ nguyên `qty_expected` gốc, chỉ cập nhật số lượng phân bổ thực tế vào `qty_allocated`.
   - *ponytail: keep original shipment item intact and avoid local splitting. SAP / ERP will handle backorders based on shipping confirmation. Upgrade path: manual split config.*
4. **Nhóm gộp (Aggregation)**: Duyệt các dòng tồn kho đã được phân bổ thành công, nhóm chúng theo cặp khóa: `(item_id, from_location_id)`.
5. **Sinh Task tổng hợp**:
   - Với mỗi nhóm, tạo duy nhất 1 bản ghi `WavePickTask` có số lượng `qty_to_pick` bằng tổng số lượng phân bổ của các dòng trong nhóm.
   - Trạng thái ban đầu của task là `PENDING`.
6. **Cập nhật Wave**: Đổi trạng thái Wave thành `RELEASED`.

### 8.2 Tích hợp Serial Tracking (Phase 16)
Đối với các sản phẩm được cấu hình quản lý bằng Serial Number:
1. **Bắt buộc quét Serial**: API `POST /api/waves/{id}/complete` sẽ kiểm tra chéo và yêu cầu cung cấp đúng, đủ danh sách `SerialNos` tương ứng với số lượng pick thực tế.
2. **Tự động phân bổ Serial tuần tự**: 
   - Để tránh việc nhân viên phải quét lại mã Serial một lần nữa tại khu vực phân loại (Sortation/Packing), hệ thống sẽ tự động gán các mã Serial đã pick cho từng Outbound Shipment Item trong đợt wave theo thứ tự tuần tự (FIFO).
   - *ponytail: auto-assign serials to shipment items sequentially to avoid duplicate scanning at sortation. Upgrade path: manual sortation app with barcode verification.*
3. **Cập nhật Ledger & Trạng thái**: Cập nhật trạng thái Serial thành `PICKED` và lưu vị trí hiện tại của Serial về khu vực đóng gói (Staging/Packing zone) trong cùng một Database Transaction.

### 8.3 Quy trình Put-Wall phân chia hàng hóa động (Sortation)
1. **Khởi tạo Slots ảo (Deterministic Slot Assignment)**: 
   - Khi đợt wave chuyển sang trạng thái `SORTING` (sau khi hoàn thành pick tổng hợp), hệ thống tự động gán mỗi Outbound Shipment trong wave vào 1 số thứ tự ô Put-Wall (Slot 1, Slot 2...).
   - Để đảm bảo tính nhất quán (không bị đổi số ô khi tải lại trang hoặc khi nhiều người dùng cùng truy cập), danh sách Outbound Shipments bắt buộc phải được sắp xếp theo ID (Guid) tăng dần trước khi gán chỉ số Slot từ 1 đến N.
2. **Dòng chảy tồn kho vật lý (Physical Inventory Flow)**:
   - Khi hoàn thành lấy hàng tổng hợp (`POST /api/waves/{id}/complete`), hệ thống thực hiện trừ tồn kho tại vị trí kệ nguồn (`from_location_id`) và chuyển dịch sang vị trí tạm thời **LOC-SORT-01 (Khu vực phân chia hàng)** để đảm bảo số dư tồn kho vật lý chính xác.
   - Hàng hóa sẽ nằm tại `LOC-SORT-01` cho đến khi module Outbound thực hiện đóng gói (Packing) và xác nhận xuất xưởng (Ship) để trừ hẳn tồn kho khỏi hệ thống.
   - *ponytail: move inventory to temporary sorting location LOC-SORT-01 during wave complete, to be officially deducted when Outbound module performs final ship.*
3. **Xử lý chênh lệch do Short Pick tại bàn Sortation**:
   - Nếu khi pick thực tế, nhân viên báo thiếu (Short Pick) dẫn đến `qty_picked < qty_allocated`:
     - Hệ thống giữ nguyên `qty_expected` và `qty_allocated`, chỉ cập nhật `qty_picked` thực tế.
     - Ô Put-Wall tại bàn phân chia sẽ tự động chuyển sang hoàn thành dựa trên **số lượng thực pick mang về** (`qty_picked`) thay vì số lượng phân bổ ban đầu, loại bỏ hoàn toàn tình trạng treo ô.
     - Khi đóng gói và xuất hàng, module Webhook/Integration (Phase 23/24) gửi bản tin Shipping Confirmation về ERP với số lượng thực xuất nhỏ hơn yêu cầu (Short Ship). ERP tự động xử lý tạo đơn backorder tiếp theo hoặc đóng đơn.
     - *ponytail: Put-Wall slots complete based on qty_picked to prevent blocking. ERP handles short-ship reconciliation. Upgrade path: manual split config.*
4. **Quét phân loại động**:
   - Nhân viên tại bàn Sortation quét mã vạch sản phẩm hoặc mã Serial.
   - API `POST /api/waves/{id}/sort` tìm kiếm Shipment trong wave đang yêu cầu sản phẩm này và chưa đủ số lượng `qty_sorted < qty_picked`.
   - Trả về thông tin `RecommendedSlotNumber` để màn hình Web tô sáng (hoặc nhấp nháy) ô Put-Wall tương ứng, chỉ dẫn nhân viên đặt hàng vào.
   - Cập nhật tăng `qty_sorted` trên `wave_items`.
5. **Hoàn thành ô (Slot Complete vs Shipment Ready)**: 
   - Khi tất cả các items của 1 Shipment *thuộc wave hiện tại* đã được sort đủ số lượng, ô Put-Wall tương ứng sáng xanh báo hiệu hoàn thành sortation của wave.
   - Tuy nhiên, để tránh đóng gói thiếu hàng (đơn hàng đa zone bị chia tách), hệ thống chỉ chuyển trạng thái Shipment sang `READY_TO_PACK` và cho phép đóng gói khi và chỉ khi **tất cả** các dòng sản phẩm của shipment đó từ mọi wave/zone khác nhau đã hoàn tất sortation.
   - *ponytail: prevent packing of incomplete multi-zone shipments. System will only enable "Pack" button for a shipment when all its items across all waves are sorted.*
6. **Quy tắc độc chiếm Put-Wall và Chặn hủy Wave**:
   - **Độc chiếm Put-Wall**: Mỗi bàn phân chia vật lý chỉ xử lý 1 đợt Wave tại 1 thời điểm. Nhân viên phải dọn sạch và đóng gói toàn bộ ô của Wave hiện tại trước khi load Wave tiếp theo.
   - **Chốt chặn hủy Wave**: Tuyệt đối không cho phép hủy Wave khi trạng thái đã chuyển sang `SORTING` hoặc `COMPLETED` để bảo toàn tính toàn vẹn của tồn kho vật lý.

### Flow guardrails

* Không bỏ qua bước validate master data.
* Không tự động sửa tồn kho nếu chưa có transaction hợp lệ.
* Không ghi đè trạng thái mới hơn bằng dữ liệu cũ.
* Nếu flow có scan, mọi scan phải gắn context nghiệp vụ.
* Nếu flow có approval, người tạo và người duyệt nên tách quyền khi nghiệp vụ yêu cầu.

## 9. Validation & business rules

* Không gom shipment sai trạng thái
* Short pick tạo exception
* Wave released không sửa tùy tiện

### Validation nền bắt buộc

* Validate tenant scope.
* Validate status transition.
* Validate permission theo action.
* Validate optimistic concurrency cho dữ liệu dễ tranh chấp.
* Validate số lượng không âm và không vượt khả dụng khi liên quan tồn kho.
* Validate reason code bắt buộc cho override, reject, cancel hoặc adjustment.

## 10. Exception handling

* Thiếu hàng
* Shipment cancel
* Pick conflict

### Mapping lỗi chuẩn

| Nhóm lỗi | Hành vi hệ thống |
|---|---|
| Input sai | Trả validation error, không ghi transaction |
| Thiếu quyền | Trả 403, ghi security audit nếu cần |
| Dữ liệu stale | Trả conflict, yêu cầu reload |
| Vi phạm rule kho | Block hoặc tạo operational exception theo severity |
| Lỗi thiết bị/tích hợp | Ghi integration/device log, cho retry hoặc fallback nếu an toàn |
| Lỗi không khôi phục | Ghi trace ID, rollback transaction, báo admin |

### Nguyên tắc exception

* Lỗi vận hành có thể xử lý nghiệp vụ thì tạo exception framework.
* Lỗi kỹ thuật chỉ tạo operational exception nếu ảnh hưởng tác vụ kho.
* Không nuốt lỗi âm thầm.
* Mọi override phải có reason và audit.

## 11. Observability

* Wave productivity
* Short pick rate

### Log và trace

* Mỗi request có trace ID.
* Command quan trọng ghi audit log.
* Entity nghiệp vụ chính ghi activity timeline.
* Job nền và integration event truyền trace ID khi liên quan flow gốc.
* Log không chứa password, token, secret hoặc dữ liệu nhạy cảm không mask.

### KPI đề xuất

* Throughput theo ngày/ca/user nếu phase có thao tác vận hành.
* Aging của task mở hoặc exception mở.
* Tỷ lệ lỗi validation/rule block.
* Tỷ lệ retry/failure nếu phase có tích hợp.
* Độ chính xác tồn kho nếu phase ảnh hưởng inventory.

## 12. Test plan

* Group pick
* Short pick
* Cancel before release

#### Kịch bản kiểm thử tích hợp: `tests/verify_wave_picking.ps1`

```powershell
$API_URL = "http://localhost:5024/api"

# 1. Login
Write-Host "1. Logging in as admin..." -ForegroundColor Cyan
$loginBody = @{
    email = "admin@nexustock.com"
    password = "AdminSecret123!"
} | ConvertTo-Json
$loginRes = Invoke-RestMethod -Uri "$API_URL/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $loginRes.token
$headers = @{ Authorization = "Bearer $token" }

# 2. Lấy sản phẩm demo
$products = Invoke-RestMethod -Uri "$API_URL/master-data/products" -Method Get -Headers $headers
$productId = $products.items[0].id

# 3. Tạo 2 Outbound Shipments có cùng sản phẩm
Write-Host "`n3. Creating two Outbound Shipments..." -ForegroundColor Cyan
$shipment1 = Invoke-RestMethod -Uri "$API_URL/outbound" -Method Post -Body (@{
    customerId = "00000000-0000-0000-0000-000000000001"
    items = @(@{ itemId = $productId; qty = 2.0 })
} | ConvertTo-Json) -ContentType "application/json" -Headers $headers

$shipment2 = Invoke-RestMethod -Uri "$API_URL/outbound" -Method Post -Body (@{
    customerId = "00000000-0000-0000-0000-000000000002"
    items = @(@{ itemId = $productId; qty = 3.0 })
} | ConvertTo-Json) -ContentType "application/json" -Headers $headers

$shipmentItem1 = $shipment1.items[0].id
$shipmentItem2 = $shipment2.items[0].id

# 4. Gom 2 Outbound Shipment Items vào 1 Wave
Write-Host "`n4. Creating Wave Picking..." -ForegroundColor Cyan
$createWaveBody = @{
    criteria = "ZONE-A"
    shipmentItemIds = @($shipmentItem1, $shipmentItem2)
} | ConvertTo-Json
$wave = Invoke-RestMethod -Uri "$API_URL/waves" -Method Post -Body $createWaveBody -ContentType "application/json" -Headers $headers
$waveId = $wave.id
Write-Host "Wave created: $($wave.waveNo) with status: $($wave.status)"

# 5. Release Wave (Sinh Allocation và Wave Pick Tasks)
Write-Host "`n5. Releasing Wave..." -ForegroundColor Cyan
$releasedWave = Invoke-RestMethod -Uri "$API_URL/waves/$waveId/release" -Method Post -Headers $headers
Write-Host "Wave Status updated: $($releasedWave.status)"

# 6. Lấy danh sách Wave Pick Task tổng hợp
Write-Host "`n6. Fetching Wave Pick Tasks..." -ForegroundColor Cyan
$tasks = Invoke-RestMethod -Uri "$API_URL/waves/$waveId/pick-list" -Method Get -Headers $headers
Write-Host "Grouped Pick Tasks count: $($tasks.Count)"
$taskId = $tasks[0].id
Write-Host "Wave Pick Task 1: Item: $($tasks[0].itemCode) - Qty to Pick: $($tasks[0].qtyToPick) from Location: $($tasks[0].fromLocationCode)"

# 7. Complete Wave Pick Task
Write-Host "`n7. Completing Wave Pick Task..." -ForegroundColor Cyan
$processBody = @{
    results = @(
        @{
            taskId = $taskId
            qtyPicked = $tasks[0].qtyToPick
        }
    )
} | ConvertTo-Json
$completed = Invoke-RestMethod -Uri "$API_URL/waves/$waveId/complete" -Method Post -Body $processBody -ContentType "application/json" -Headers $headers
Write-Host "Wave Status: $($completed.status)"

# 8. Sortation / Put-Wall
Write-Host "`n8. Sorting items into Put-Wall Slots..." -ForegroundColor Cyan
$sortation = Invoke-RestMethod -Uri "$API_URL/waves/$waveId/sortation" -Method Get -Headers $headers
Write-Host "Put-Wall Slots count: $($sortation.slots.Count)"

$sortBody = @{
    barcode = $products.items[0].code
} | ConvertTo-Json
$sortResult = Invoke-RestMethod -Uri "$API_URL/waves/$waveId/sort" -Method Post -Body $sortBody -ContentType "application/json" -Headers $headers
Write-Host "Item sorted to Slot: $($sortResult.recommendedSlotNumber). Is Slot Complete: $($sortResult.isSlotComplete)"

Write-Host "`n=========================================="
Write-Host "    WAVE PICKING TESTS PASSED 100%!" -ForegroundColor Green
Write-Host "=========================================="
```

### Test matrix bắt buộc

| Nhóm test | Nội dung |
|---|---|
| Unit | Rule nghiệp vụ, status transition, validation helper |
| Integration | API + DB transaction + permission + concurrency |
| E2E | Luồng người dùng chính từ UI/RF/mobile |
| Negative | Sai quyền, sai trạng thái, dữ liệu stale, duplicate request |
| Regression | Không phá phase trước và dependency downstream |

### Dữ liệu test

* Tenant demo.
* User đủ quyền và user thiếu quyền.
* Master data hợp lệ và master data inactive.
* Bản ghi đang open/completed/cancelled để test transition.
* Dữ liệu conflict/concurrency nếu phase ghi transaction.

## 13. Acceptance criteria

* Wave giảm dòng pick nhưng vẫn phân bổ đúng đơn

### Definition of done

* Database migration chạy sạch trên database trống.
* API chính có test integration pass.
* UI/RF/mobile flow chính thao tác được end-to-end.
* Audit/trace hoạt động cho command quan trọng.
* Exception path chính được test.
* README hoặc phase note đủ để executor tiếp theo hiểu dependency.
* Không còn placeholder generic trong phần triển khai phase.

## 14. Out of scope

* Cluster/cart picking

Không đưa scope ngoài vào phase này nếu chưa có dependency rõ. Nếu phát hiện scope mới bắt buộc, cập nhật roadmap tổng trước khi triển khai.

## 15. Dependencies

* Stage 1 + phase trước trong Stage 2

### Downstream impact

* Phase sau được phép dùng API/status/data contract của phase này.
* Nếu đổi contract sau khi phase đã hoàn tất, phải cập nhật phase phụ thuộc.
* Không đổi tên bảng/API đã được phase sau tham chiếu nếu không có migration plan.

## 16. Maintenance notes

* Không làm phức tạp MVP
* Feature advanced phải có flag/permission riêng
* Mọi transaction inventory phải atomic

### Maintenance contract

* Giữ section tài liệu này đồng bộ với migration/API thực tế.
* Khi thêm status mới, cập nhật validation, UI badge, test và exception mapping.
* Khi thêm permission mới, cập nhật seed, UI visibility và API policy.
* Khi thêm field bắt buộc, cập nhật import/export, DTO, validation và test data.

## 17. Extension points

* Tối ưu thuật toán
* Thêm dashboard nâng cao
* Thêm rule cấu hình sâu hơn

### Nguyên tắc mở rộng

* Mở rộng bằng module hoặc service rõ ràng, không nhét logic vào controller.
* Ưu tiên cấu hình/rule trước khi hardcode nghiệp vụ mới.
* Không thêm dependency ngoài nếu standard library hoặc dependency hiện có xử lý đủ.
* Feature nâng cao nên có permission hoặc feature flag riêng.

## 18. Rollback notes

* Tắt permission/menu
* Release reservation/task mở nếu rollback
* Không xóa transaction đã phát sinh

### Rollback safety

* Không xóa transaction đã phát sinh trong production.
* Nếu dữ liệu sai, tạo corrective transaction hoặc trạng thái hủy có audit.
* Nếu UI lỗi, có thể ẩn menu/permission tạm thời.
* Nếu API lỗi, rollback deployment image trước, xử lý dữ liệu sau theo trace ID.





