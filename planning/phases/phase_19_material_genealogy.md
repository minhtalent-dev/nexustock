# PHASE 19: Material genealogy

## Execution spec maturity

- **Mức hiện tại:** 🎉 100% (Đã sẵn sàng thực thi - Execution Ready)
- **Đánh giá:** Đầy đủ chi tiết kỹ thuật cấp độ thực thi gồm database schema PostgreSQL, API endpoint, DTO C#, thuật toán phòng ngừa chu kỳ, logic phóng tỏa nhánh hàng loạt (Cascade Hold), và trang UI Next.js hiển thị cây genealogy trực quan.
- **Khi cần upgrade:** Upgrade nếu tích hợp hệ thống IoT Sensor tự động quét Lot hoặc báo cáo recall pháp lý theo chuẩn FDA/GS1.

## 1. Mục tiêu

Truy vết cây Lot cha/con và khoanh vùng lỗi chất lượng.

Phase này thuộc stage **Advanced WMS** và phải tạo ra deliverable có thể kiểm thử độc lập. Nội dung phải đủ rõ để executor triển khai mà không cần suy đoán nghiệp vụ chính.

## 2. Phạm vi

Truy vết cây Lot cha/con và khoanh vùng lỗi chất lượng.

### In scope

* Tạo module Material genealogy
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

* Tạo module Material genealogy
* Seed permission/rule liên quan
* Cập nhật menu và route

### Cấu trúc module đề xuất

```text
backend/modules/material_genealogy/
frontend/features/material_genealogy/
planning/phases/phase_19_material_genealogy.md
```

### Permission seed đề xuất

* material_genealogy.read
* material_genealogy.create
* material_genealogy.update
* material_genealogy.approve
* material_genealogy.export

Chỉ seed permission thực sự dùng trong phase. Không tạo quyền dư nếu chưa có màn hình hoặc API tương ứng.

## 5. Database

| Thành phần dữ liệu | Mục đích | Ràng buộc chính |
|---|---|---|
| `LotRelations` | Quan hệ Lot | ParentLot, childLot, relationType |
| `GenealogyEvents` | Sự kiện genealogy | Split,merge,repack,holdBranch |

#### Cấu trúc bảng SQL chi tiết cho PostgreSQL:

```sql
CREATE SCHEMA IF NOT EXISTS genealogy;

-- 1. Bảng quan hệ phả hệ Lot (Lot Relations)
CREATE TABLE genealogy.lot_relations (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    parent_lot_id UUID NOT NULL,
    child_lot_id UUID NOT NULL,
    relation_type VARCHAR(50) NOT NULL, -- SPLIT, MERGE, REPACK, TRANSFORM
    qty_transferred DECIMAL(18,4) NOT NULL,
    created_at TIMESTAMP NOT NULL,
    created_by VARCHAR(100) NOT NULL
);

CREATE INDEX idx_lot_rel_parent ON genealogy.lot_relations(tenant_id, parent_lot_id);
CREATE INDEX idx_lot_rel_child ON genealogy.lot_relations(tenant_id, child_lot_id);
CREATE UNIQUE INDEX uq_lot_rel_parent_child ON genealogy.lot_relations(tenant_id, parent_lot_id, child_lot_id);

-- 2. Bảng nhật ký sự kiện phả hệ Lot (Genealogy Events)
CREATE TABLE genealogy.genealogy_events (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    event_type VARCHAR(50) NOT NULL, -- SPLIT, MERGE, REPACK, HOLD_BRANCH, RELEASE_BRANCH
    lot_id UUID NOT NULL,
    description VARCHAR(500),
    created_at TIMESTAMP NOT NULL,
    created_by VARCHAR(100) NOT NULL,
    payload VARCHAR(2000) -- Lưu thông tin metadata JSON bổ sung
);

CREATE INDEX idx_genealogy_events_lot ON genealogy.genealogy_events(tenant_id, lot_id);
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
| `POST /api/genealogy/relations` | Tạo relation | Có auth, validation, trace ID và response lỗi chuẩn. |
| `GET /api/genealogy/lots/{lotNo}` | Xem cây | Có auth, validation, trace ID và response lỗi chuẩn. |
| `POST /api/genealogy/hold-branch` | Hold nhánh | Có auth, validation, trace ID và response lỗi chuẩn. |

#### Cấu trúc DTOs (C#):

```csharp
namespace Nexustock.Modules.MaterialGenealogy.DTOs;

public class CreateLotRelationDto
{
    public string ParentLotNo { get; set; } = string.Empty;
    public string ChildLotNo { get; set; } = string.Empty;
    public string RelationType { get; set; } = "SPLIT"; // SPLIT, MERGE, REPACK
    public decimal QtyTransferred { get; set; }
}

public class LotGenealogyNodeDto
{
    public Guid LotId { get; set; }
    public string LotNo { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal QtyOnHand { get; set; }
    public string Status { get; set; } = "RELEASED"; // QC Status: RELEASED, HOLD, REJECTED
    public List<LotGenealogyNodeDto> Children { get; set; } = new();
    public List<LotGenealogyNodeDto> Parents { get; set; } = new();
}

public class HoldBranchDto
{
    public string TargetLotNo { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
```

#### Thuật toán phòng ngừa chu kỳ (Prevent Cycle evaluation):
```csharp
// Thuật toán DFS duyệt ngược các tổ tiên để chặn chu kỳ phả hệ
private async Task VerifyNoCycleAsync(Guid tenantId, Guid parentLotId, Guid childLotId)
{
    if (parentLotId == childLotId)
        throw new InvalidOperationException("Không thể tạo liên kết cha con với cùng một Lot.");

    var visited = new HashSet<Guid>();
    var queue = new Queue<Guid>();
    queue.Enqueue(parentLotId);

    while (queue.Any())
    {
        var current = queue.Dequeue();
        if (current == childLotId)
            throw new InvalidOperationException("Phát hiện chu kỳ phả hệ (Cycle detected)! Lot con không thể là tổ tiên của Lot cha.");

        if (!visited.Contains(current))
        {
            visited.Add(current);
            // Lấy tất cả các cha trực tiếp của Lot hiện tại
            var parents = await _context.LotRelations
                .Where(r => r.ChildLotId == current && r.TenantId == tenantId)
                .Select(r => r.ParentLotId)
                .ToListAsync();

            foreach (var p in parents)
            {
                queue.Enqueue(p);
            }
        }
    }
}
```

#### Thuật toán Phóng tỏa nhánh Lot (Cascade Hold):
Khi người dùng thực hiện Hold một nhánh Lot, hệ thống tự động khóa tất cả Lot con cháu ở mức hạ nguồn:
```csharp
public async Task HoldBranchAsync(Guid tenantId, string username, HoldBranchDto dto)
{
    var startLot = await _lotContext.Lots.FirstOrDefaultAsync(l => l.LotNo == dto.TargetLotNo && l.TenantId == tenantId);
    if (startLot == null) throw new KeyNotFoundException("Không tìm thấy Lot mục tiêu.");

    using var transaction = await _lotContext.Database.BeginTransactionAsync();
    try
    {
        var descendantLotIds = new List<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(startLot.Id);

        while (queue.Any())
        {
            var currentId = queue.Dequeue();
            descendantLotIds.Add(currentId);

            var children = await _context.LotRelations
                .Where(r => r.ParentLotId == currentId && r.TenantId == tenantId)
                .Select(r => r.ChildLotId)
                .ToListAsync();

            foreach (var c in children)
            {
                if (!queue.Contains(c) && !descendantLotIds.Contains(c))
                    queue.Enqueue(c);
            }
        }

        // Cập nhật trạng thái các Lot con cháu thành HOLD
        var lotsToUpdate = await _lotContext.Lots.Where(l => descendantLotIds.Contains(l.Id)).ToListAsync();
        foreach (var lot in lotsToUpdate)
        {
            lot.QcStatus = "HOLD";
            lot.UpdatedAt = DateTime.UtcNow;
            lot.UpdatedBy = username;

            var evt = new GenealogyEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EventType = "HOLD_BRANCH",
                LotId = lot.Id,
                Description = $"Phong tỏa nhánh từ gốc {dto.TargetLotNo}. Lý do: {dto.Description}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };
            await _context.GenealogyEvents.AddAsync(evt);
        }

        await _lotContext.SaveChangesAsync();
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
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
| Genealogy tree | Cây Lot | Có loading, empty, error, filter, pagination và quyền theo action. |
| Impact analysis | Danh sách bị ảnh hưởng | Có loading, empty, error, filter, pagination và quyền theo action. |

#### Màn hình hiển thị cây Genealogy trực quan (`/admin/genealogy/[lotNo]/page.tsx`):

```tsx
"use client";

import { useEffect, useState, use } from "react";
import Link from "next/link";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { showError, showSuccess } from "@/lib/toast";
import { ArrowLeft, GitFork, ShieldAlert, CheckCircle2 } from "lucide-react";

interface GenealogyNode {
  lotId: string;
  lotNo: string;
  productCode: string;
  productName: string;
  qtyOnHand: number;
  status: string;
  children: GenealogyNode[];
  parents: GenealogyNode[];
}

export default function GenealogyTreePage({ params }: { params: Promise<{ lotNo: string }> }) {
  const resolvedParams = use(params);
  const lotNo = resolvedParams.lotNo;
  const [tree, setTree] = useState<GenealogyNode | null>(null);
  const [loading, setLoading] = useState(false);

  const fetchTree = async () => {
    setLoading(true);
    try {
      const res = await api.get<GenealogyNode>(`/genealogy/lots/${lotNo}/tree`);
      setTree(res.data);
    } catch {
      showError("Không thể tải cây phả hệ Lot.");
    } finally {
      setLoading(false);
    }
  };

  const handleHoldBranch = async () => {
    if (!confirm(`Bạn có chắc chắn muốn phong tỏa toàn bộ nhánh từ Lot ${lotNo} trở xuống?`)) return;
    try {
      await api.post("/genealogy/hold-branch", {
        targetLotNo: lotNo,
        reasonCode: "QUALITY_ISSUE",
        description: "Phong tỏa khẩn cấp phòng chống lây lan lỗi chất lượng"
      });
      showSuccess("Đã phong tỏa toàn bộ nhánh Lot thành công.");
      fetchTree();
    } catch (err: any) {
      showError(err.response?.data?.message || "Lỗi phong tỏa nhánh.");
    }
  };

  useEffect(() => {
    fetchTree();
  }, [lotNo]);

  const renderNode = (node: GenealogyNode) => {
    const isHold = node.status === "HOLD";
    return (
      <div key={node.lotId} className="flex flex-col items-center gap-2">
        <Card className={`bg-zinc-900 border-2 ${isHold ? "border-red-500 shadow-[0_0_15px_rgba(239,68,68,0.2)]" : "border-zinc-800"} text-white w-64`}>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-bold flex justify-between items-center">
              <span>Lot: {node.lotNo}</span>
              <Badge className={isHold ? "bg-red-600" : "bg-emerald-600"}>{node.status}</Badge>
            </CardTitle>
          </CardHeader>
          <CardContent className="text-xs space-y-1">
            <div className="text-zinc-400">Sản phẩm: {node.productCode} - {node.productName}</div>
            <div className="text-zinc-400">Tồn kho: <span className="text-zinc-200 font-bold">{node.qtyOnHand}</span></div>
          </CardContent>
        </Card>
        {node.children && node.children.length > 0 && (
          <div className="flex flex-col items-center mt-2 w-full">
            <div className="h-4 w-0.5 bg-zinc-700"></div>
            <div className="flex gap-6 border-t border-zinc-700 pt-4 w-full justify-center">
              {node.children.map(child => renderNode(child))}
            </div>
          </div>
        )}
      </div>
    );
  };

  if (loading) return <div className="text-white p-6 font-mono">Đang tải cây phả hệ...</div>;
  if (!tree) return <div className="text-white p-6 font-mono">Không tìm thấy dữ liệu.</div>;

  return (
    <div className="flex flex-col gap-6 text-white p-6 font-sans">
      <div className="flex justify-between items-center">
        <div className="flex items-center gap-3">
          <Link href="/admin/lots">
            <Button variant="outline" className="border-zinc-800 text-zinc-300"><ArrowLeft className="h-4 w-4 mr-2" /> Quay lại</Button>
          </Link>
          <h1 className="text-2xl font-bold flex items-center gap-2">
            <GitFork className="h-6 w-6 text-indigo-400" /> Truy vết phả hệ Lot
          </h1>
        </div>
        <Button onClick={handleHoldBranch} className="bg-red-600 hover:bg-red-500 text-white font-bold flex items-center gap-2">
          <ShieldAlert className="h-4 w-4" /> Phong tỏa nhánh
        </Button>
      </div>

      <div className="overflow-auto border border-zinc-800 bg-zinc-950/40 rounded-xl p-8 min-h-[500px] flex justify-center items-start">
        {renderNode(tree)}
      </div>
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

1. Split/merge tạo relation
2. QC phát hiện lỗi
3. Tra cây
4. Hold branch
5. Thông báo exception

### Flow guardrails

* Không bỏ qua bước validate master data.
* Không tự động sửa tồn kho nếu chưa có transaction hợp lệ.
* Không ghi đè trạng thái mới hơn bằng dữ liệu cũ.
* Nếu flow có scan, mọi scan phải gắn context nghiệp vụ.
* Nếu flow có approval, người tạo và người duyệt nên tách quyền khi nghiệp vụ yêu cầu.

## 9. Validation & business rules

* Không tạo cycle
* Hold branch cần quyền
* Không mất trace khi repack

### Validation nền bắt buộc

* Validate tenant scope.
* Validate status transition.
* Validate permission theo action.
* Validate optimistic concurrency cho dữ liệu dễ tranh chấp.
* Validate số lượng không âm và không vượt khả dụng khi liên quan tồn kho.
* Validate reason code bắt buộc cho override, reject, cancel hoặc adjustment.

## 10. Exception handling

* Cycle relation
* Tree quá sâu
* Lot missing

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

* Audit relation
* Impact KPI

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

* Prevent cycle
* Tree query
* Branch hold

#### Kịch bản kiểm thử tự động (`tests/verify_genealogy.ps1`):

```powershell
$API_URL = "http://localhost:5024/api"

# 1. Login
$loginBody = @{ email = "admin@nexustock.com"; password = "AdminSecret123!" } | ConvertTo-Json
$loginRes = Invoke-RestMethod -Uri "$API_URL/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $loginRes.token
$headers = @{ Authorization = "Bearer $token" }

# 2. Tạo Lot cha LOT-PARENT-01 và Lot con LOT-CHILD-01
$createRelBody = @{
    parentLotNo = "LOT-PARENT-01"
    childLotNo = "LOT-CHILD-01"
    relationType = "SPLIT"
    qtyTransferred = 10.0
} | ConvertTo-Json

$relRes = Invoke-RestMethod -Uri "$API_URL/genealogy/relations" -Method Post -Body $createRelBody -ContentType "application/json" -Headers $headers
Write-Host "Tạo quan hệ thành công."

# 3. Test ngăn chặn chu kỳ (Prevent Cycle)
try {
    $cycleBody = @{
        parentLotNo = "LOT-CHILD-01"
        childLotNo = "LOT-PARENT-01"
        relationType = "SPLIT"
        qtyTransferred = 5.0
    } | ConvertTo-Json
    $null = Invoke-RestMethod -Uri "$API_URL/genealogy/relations" -Method Post -Body $cycleBody -ContentType "application/json" -Headers $headers
    Write-Error "Lỗi: Hệ thống không chặn được chu kỳ phả hệ!"
} catch {
    Write-Host "Chặn chu kỳ thành công: $_"
}

# 4. Phong tỏa nhánh (Cascade Hold)
$holdBody = @{
    targetLotNo = "LOT-PARENT-01"
    reasonCode = "QUALITY_ISSUE"
    description = "Kiểm tra phong tỏa nhánh"
} | ConvertTo-Json

$holdRes = Invoke-RestMethod -Uri "$API_URL/genealogy/hold-branch" -Method Post -Body $holdBody -ContentType "application/json" -Headers $headers
Write-Host "Phong tỏa nhánh thành công."
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

* Khoanh vùng được Lot ảnh hưởng

### Definition of done

* Database migration chạy sạch trên database trống.
* API chính có test integration pass.
* UI/RF/mobile flow chính thao tác được end-to-end.
* Audit/trace hoạt động cho command quan trọng.
* Exception path chính được test.
* README hoặc phase note đủ để executor tiếp theo hiểu dependency.
* Không còn placeholder generic trong phần triển khai phase.

## 14. Out of scope

* Graph DB

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





