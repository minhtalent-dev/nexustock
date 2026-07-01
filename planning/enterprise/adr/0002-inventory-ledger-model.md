# ADR 0002: Lựa chọn mô hình Inventory Ledger (Sổ cái tồn kho)

## Trạng thái
Đã duyệt (Approved)

## Bối cảnh
Trong các hệ thống quản lý kho (WMS), việc giữ cho số liệu tồn kho luôn chính xác và có thể kiểm toán được (auditable) là tối quan trọng. Hai mô hình quản lý tồn kho phổ biến là:
1. **Direct Update Model:** Cập nhật trực tiếp số lượng tồn kho (Quantity) trên bảng số dư (`InventoryBalances`).
2. **Ledger-based Model (Sổ cái):** Mọi thay đổi tồn kho được ghi nhận bằng các bản ghi giao dịch bất biến (`InventoryTransactions`). Số dư (`InventoryBalances`) chỉ là một bảng tổng hợp (read model) được tính toán từ các giao dịch này hoặc được cập nhật đồng thời trong cùng một transaction.

Nếu sử dụng Direct Update Model, hệ thống sẽ rất khó truy tìm nguyên nhân vì sao số lượng tồn kho bị thay đổi (không biết do ai, khi nào, từ nghiệp vụ nào). Ngoài ra, rủi ro tranh chấp tài nguyên ghi (Write Concurrency) dẫn đến số dư âm kho là rất lớn.

## Quyết định
Chúng tôi quyết định chọn **Mô hình Inventory Ledger bất biến (Append-only Ledger)** kết hợp với bảng số dư đồng bộ (`InventoryBalances`).

### Chi tiết mô hình triển khai:
1. **Bảng Giao dịch Bất biến (`InventoryTransactions`):**
   - Chỉ cho phép chèn mới (`INSERT`), cấm tuyệt đối cập nhật (`UPDATE`) và xóa (`DELETE`).
   - Mọi thay đổi tồn kho (nhập, xuất, di chuyển, điều chỉnh kiểm kê, QC Hold, QC Release) bắt buộc phải tạo ít nhất 1 dòng ghi giao dịch trong bảng này.
   - Bắt buộc chứa các cột: `tenantId`, `warehouseId`, `itemId`, `locationId`, `lotId`, `lpnId`, `qty` (có thể âm hoặc dương), `transactionType` (RECEIVE, MOVE, HOLD, RELEASE, PICK, PACK, SHIP, ADJUST, vv), `sourceType`, `sourceId` (ID của phiếu nhập, phiếu xuất, nhiệm vụ cất hàng), và `traceId`.

2. **Bảng Số dư Đồng bộ (`InventoryBalances`):**
   - Lưu trữ số lượng khả dụng tức thời của từng mặt hàng tại một vị trí cụ thể (với Lot, LPN, Trạng thái QC xác định).
   - Được cập nhật đồng thời trong cùng Database Transaction với việc chèn dòng vào `InventoryTransactions`.
   - Có Unique Constraint: `tenantId + warehouseId + locationId + itemId + lotId + lpnId + inventoryStatus`.
   - Sử dụng cơ chế Optimistic Concurrency Control (OCC) qua cột `rowVersion` để chống ghi đè khi nhiều luồng cùng thao tác trên một số dư tồn kho.

3. **Cấm số dư âm kho (Zero Negative Inventory Invariant):**
   - Hệ thống chặn commit transaction nếu số lượng tồn kho sau thay đổi nhỏ hơn 0 tại bất kỳ thời điểm nào. Điều này được cấu hình bằng DB Constraint (`qty >= 0` trên bảng `InventoryBalances`) kết hợp kiểm tra ở Application Service.

4. **Sửa sai bằng Giao dịch bù (Corrective Transactions):**
   - Nếu phát hiện giao dịch trước đó bị sai, tuyệt đối không sửa dòng giao dịch cũ. Người vận hành phải thực hiện một nghiệp vụ điều chỉnh (ví dụ: điều chỉnh kiểm kê - Adjustment) để sinh ra một giao dịch bù (Compensating Transaction) điều chỉnh số dư về đúng thực tế.

## Hệ quả & Đánh giá

### Ưu điểm (Benefits):
- **Khả năng truy vết hoàn hảo (Perfect Audit Trail):** Có thể dựng lại lịch sử tồn kho của bất kỳ sản phẩm nào tại bất kỳ thời điểm nào trong quá khứ bằng cách cộng dồn các giao dịch từ ngày đầu tiên.
- **Tính toàn vẹn dữ liệu cao:** Không bao giờ xảy ra hiện tượng lệch số dư mà không rõ nguyên nhân.
- **Hỗ trợ tốt việc gỡ lỗi:** Khi xảy ra lỗi âm kho hoặc lệch số, dev chỉ cần truy vấn bảng `InventoryTransactions` theo `traceId` là biết ngay API hay tác vụ nào gây ra lỗi.

### Nhược điểm & Cách giảm thiểu (Risks & Mitigations):
- **Hiệu năng và dung lượng lưu trữ:** Bảng `InventoryTransactions` sẽ phình to rất nhanh trong môi trường kho tần suất cao.
  - *Biện pháp giảm thiểu:* Thực hiện partition bảng theo tháng/quý trên PostgreSQL. Định kỳ (ví dụ: cuối năm) lưu trữ (archive) các giao dịch cũ sang cold storage và giữ lại các bản ghi chốt số dư đầu kỳ (Opening Balance).

*ponytail: Trong tương lai, nếu lượng transaction vượt quá 10 triệu dòng/tháng, chúng tôi sẽ tách bảng read model InventoryBalances ra PostgreSQL Replica hoặc dùng Redis cache để tăng tốc độ truy vấn danh sách tồn kho cho màn hình UI/RF.*
