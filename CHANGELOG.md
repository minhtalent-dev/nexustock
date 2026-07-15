# Lịch sử cập nhật dự án

## Phiên bản 0.7.0 - Quản lý Pallet/LPN, Truy vết mã Serial và Xử lý hàng trả về RMA (Phase 15, 16 & 17)
*Ngày cập nhật: 15/07/2026*

### Tính năng mới
- **Xử lý hàng trả về RMA (RMA Return Flow - Phase 17)**: Hỗ trợ quy trình tiếp nhận hàng trả lại từ khách hàng, kiểm định chất lượng (QC) và phân loại định vị xử lý (Restock/Scrap).
  - Tích hợp kiểm định QC: Chỉ những sản phẩm vượt qua kiểm định (QC PASS) mới được phép đưa vào tồn kho khả dụng (RESTOCK).
  - Định vị kệ ưu tiên: Tự động đưa hàng hoàn về kệ Staging (`LOC-STG-01`) để tránh lỗi quá tải dung tích kho.
  - Phân quyền bảo mật: Seed các quyền hệ thống tương ứng: `rma.read`, `rma.create`, `rma.update`, `rma.qc`.
- **Quản lý Pallet/LPN (License Plate Number)**: Hỗ trợ gom nhiều lô hàng hóa khác nhau lên cùng một mã Pallet/LPN để theo dõi và di chuyển nguyên khối.
- **Truy vết mã Serial (Serial Tracking)**: Hỗ trợ quản lý vòng đời và truy vết dòng thời gian hoạt động của từng đơn vị sản phẩm cụ thể bằng số Serial.
- **Import Serial hàng loạt qua CSV**: Cho phép đăng ký hàng loạt mã Serial bằng cách tải lên file CSV trong quá trình nhận hàng (Inbound/Receive), bên cạnh việc quét mã thủ công.
- **Đồng bộ xác thực vị trí (Validation & Concurrency)**: Kiểm tra chéo vị trí kệ và trạng thái Serial khi picking. Sử dụng concurrency token `xmin` để chống tranh chấp khi quét trùng mã trên nhiều thiết bị di động đồng thời.
- **Giao diện Web & Handheld Mobile**:
  - Web: Trang quản lý RMA tích hợp QC nhanh, trang truy vết mã Serial hiển thị timeline chi tiết các sự kiện (RECEIVE, QC, PICK, SHIP) và công cụ import CSV.
  - Mobile: Màn hình quét nhận mã Serial hỗ trợ tự động lấy nét (autofocus) và quét liên tục.
- **Phân quyền bảo mật**: Seed thêm 4 quyền hệ thống mới: `serial.read`, `serial.create`, `serial.update`, `serial.execute`.
- **Thuật toán Attach/Detach (Đóng/Rút hàng)**: Tự động chia tách dòng tồn kho (Split Row) khi đóng hoặc rút một phần số lượng từ pallet, xử lý chính xác tỷ lệ số lượng khóa giữ (QtyReserved) để tránh lệch Allocation.
- **Dịch chuyển nguyên Pallet (Atomic Move)**: Cập nhật vị trí kệ của LPN và đồng loạt tất cả các dòng tồn kho thuộc LPN đó trong cùng một Database Transaction. Tích hợp ghi nhận lịch sử dịch chuyển (InventoryMovement) và sự kiện LPN (LpnEvent).
- **Giao diện Web & Handheld Mobile**:
  - Web: Trang quản lý danh sách LPN, tạo LPN trống, đóng hàng, dịch chuyển kệ và xem lịch sử dòng thời gian sự kiện của LPN.
  - Mobile: Màn hình quét mã LPN -> quét kệ đích -> xác nhận di chuyển nguyên khối nhanh gọn bằng thiết bị cầm tay.
- **Phân quyền bảo mật**: Seed thêm 4 quyền hệ thống mới: `lpn.read`, `lpn.create`, `lpn.update`, `lpn.execute`.

## Phiên bản 0.6.0 - Bổ sung hàng Pick Face tự động và thiết bị cầm tay (Phase 14)
*Ngày cập nhật: 14/07/2026*

### Tính năng mới
- **Bổ sung hàng tự động (Replenishment)**: Hỗ trợ thiết lập quy tắc mức tồn kho tối thiểu (Min) và tối đa (Max) cho sản phẩm tại các vị trí lấy hàng (Pick Face). Hệ thống tự động tính toán nhu cầu bổ sung dựa trên tồn kho khả dụng hiện tại kết hợp lượng hàng đang vận chuyển để tạo nhiệm vụ di chuyển hàng từ khu vực lưu trữ (Bulk) về Pick Face.
- **Phân bổ theo FEFO/FIFO**: Thuật toán tự động sắp xếp các lô hàng Bulk nguồn theo nguyên tắc hạn dùng trước xuất trước (FEFO) hoặc nhập trước xuất trước (FIFO). Tự động khóa giữ tồn kho ở Bulk nguồn khi tạo nhiệm vụ để tránh trùng lặp.
- **Quy trình hoàn thành & hủy nhiệm vụ**:
  - Hỗ trợ hoàn thành nhiệm vụ bổ sung hàng bằng cách quét kệ Bulk -> quét số lô -> quét kệ Pick Face -> nhập số lượng thực tế.
  - Tự động ghi nhận sự cố chênh lệch số lượng thực tế (nếu có) thành phiếu lỗi để kiểm tra, đồng thời tự động sinh lịch sử dịch chuyển tồn kho chi tiết.
  - Hủy nhiệm vụ bổ sung hàng để giải phóng hoàn toàn lượng tồn kho đang bị khóa giữ.
- **Tự động hóa quét định kỳ**: Tích hợp công cụ chạy ngầm tự động quét và sinh nhiệm vụ bổ sung hàng định kỳ, quản trị trực tiếp trên bảng điều khiển quản lý.
- **Giao diện di động cầm tay (RF Scanner)**: Thiết kế màn hình di động 3 bước dành riêng cho quy trình bổ sung hàng, nâng cao tốc độ và độ chính xác khi vận chuyển.

## Phiên bản 0.5.0 - Quét mã cầm tay, Kiểm kê kho, Cất hàng tự động và Phân bổ giữ hàng (Phase 8, 9, 12 & 13)
*Ngày cập nhật: 13/07/2026*

### Tính năng mới
- **Cất hàng tự động (Putaway slotting - Phase 12)**: Đề xuất vị trí cất hàng tối ưu dựa trên quy tắc phân vùng, sức chứa và khoảng cách di chuyển thực tế. Bản đồ lưới 2D Grid hỗ trợ hiển thị trực quan các kệ đề xuất, kệ đã chứa hàng và kệ trống.
- **Phân bổ giữ hàng tối ưu (Allocation & reservation - Phase 13)**: Thuật toán phân bổ theo nguyên tắc FEFO/FIFO, tích hợp tie-break và pessimistic locking chống deadlock đồng thời (Resource Ordering). Hỗ trợ giải phóng tồn giữ hàng hết hạn tự động theo lô thông qua background worker.
- **Giao diện di động cầm tay (Handheld UI)**: Thiết kế giao diện di động chuyên dụng, tối ưu kích thước màn hình thiết bị cầm tay RF của nhân viên kho, hỗ trợ bẫy trạng thái kết nối mạng thời gian thực.
- **Tự động giao việc tối ưu khoảng cách**: Áp dụng mô hình hồ việc chung cho nhân viên tự nhận việc (Claim task). Hệ thống tự động tính toán và ưu tiên giao các nhiệm vụ có vị trí kệ (Location) gần vị trí khai báo hiện tại của nhân viên đó nhất để giảm thiểu quãng đường di chuyển.
- **Dịch chuyển kho cơ động (Movement)**: Cho phép quét mã vạch để thực hiện dịch chuyển vị trí lưu trữ của các lô hàng trực tiếp trên thiết bị cầm tay.
- **Lấy hàng cơ động (Picking)**: Nhân viên kho thực hiện các nhiệm vụ lấy hàng xuất kho bằng cách quét vị trí kệ và quét lô hàng trực quan trên màn hình di động.
- **Đồng bộ ngoại tuyến (Offline Sync)**: Hỗ trợ ghi nhận thao tác ngoại tuyến khi mất kết nối mạng và tự động đồng bộ lên hệ thống ngay khi có kết nối trở lại, bảo đảm không gián đoạn công việc.
- **Kiểm kê kho phê duyệt phân cấp L1-L3 (Cycle Count)**: Hỗ trợ tạo đợt kiểm kê, phong tỏa vị trí kệ tự động và nhập kết quả kiểm đếm thực tế. Hệ thống tự động phân loại chênh lệch và yêu cầu phê duyệt phân cấp L1-L3 tùy theo giá trị chênh lệch.
- **Tự động ghi nhận và xử lý sự cố vận hành**: Tự động phát hiện và ghi nhận các sự cố phát sinh trong quá trình vận hành kho (như quét sai mã kệ, vị trí kệ bị khóa, hoặc chênh lệch số lượng) thành một phiếu lỗi để điều tra và khắc phục.
- **Quản lý SLA xử lý sự cố**: Hỗ trợ gán phiếu lỗi cho nhân viên phụ trách kèm theo thời gian cam kết xử lý (SLA), tự động cảnh báo và chuyển trạng thái quá hạn nếu không được giải quyết kịp thời.
- **Tự động đồng bộ số dư khắc phục**: Tự động thực hiện các bút toán điều chỉnh tăng/giảm tồn kho thực tế tương ứng ngay khi phê duyệt phương án khắc phục lỗi vận hành.

### Cải tiến và tối ưu
- **Quét mã vạch thông minh**: Tích hợp cơ chế tự động focus vào ô quét mã trên thiết bị handheld giúp nhân viên kho thao tác liên tục không cần chạm màn hình.
- **Chống trùng lặp dữ liệu**: Tích hợp cơ chế chống đồng bộ trùng lặp đối với các giao dịch ngoại tuyến, đảm bảo tính toàn vẹn dữ liệu số dư tồn kho.

## Phiên bản 0.4.0 - Quản lý xuất kho, kiểm soát lấy hàng và đóng gói (Phase 7)
*Ngày cập nhật: 11/07/2026*

### Tính năng mới
- **Quản lý đơn xuất kho**: Tạo mới đơn xuất kho, chọn đối tác và danh sách vật tư cần xuất với số lượng yêu cầu trực quan.
- **Tự động phân bổ lấy hàng**: Hệ thống tự động phân bổ lô hàng theo nguyên tắc nhập trước xuất trước (FIFO), tự động loại trừ các lô hàng chưa qua kiểm định chất lượng (QC Hold) và các vị trí lưu trữ đang bị khóa chiều xuất.
- **Xác nhận lấy hàng (Picking)**: Cho phép nhân viên vận hành xác nhận số lượng thực tế lấy được tại từng vị trí kệ, ngăn chặn việc lấy quá số lượng yêu cầu và kiểm soát trạng thái lô hàng thời gian thực.
- **Đóng gói kiện hàng (Packing)**: Cho phép ghi nhận thông tin đóng gói, mã kiện hàng và cân nặng thực tế để sẵn sàng xuất xưởng.

### Cải tiến và tối ưu
- **An toàn chất lượng**: Tích hợp chốt chặn kiểm soát chất lượng nghiêm ngặt ở cả 2 bước phân bổ và lấy hàng thực tế, đảm bảo không xuất nhầm hàng lỗi hay hàng chưa kiểm định.
- **Quản lý tồn kho tức thời**: Số lượng hàng hóa được trừ trực tiếp và cập nhật trạng thái khả dụng ngay khi hoàn thành nhiệm vụ lấy hàng.

## Phiên bản 0.3.0 - Quản trị hệ thống, Phân quyền & Nhập kho nhận hàng (Phase 3 & 4)
*Ngày cập nhật: 10/07/2026*

### Tính năng mới
- **Trang đăng nhập bảo mật**: Cho phép đăng nhập hệ thống bằng email và mật khẩu an toàn.
- **Quản lý người dùng**: Xem danh sách người dùng, thay đổi trạng thái kích hoạt và gán nhiều vai trò.
- **Quản lý vai trò và Ma trận quyền**: Cho phép tạo vai trò mới và tích chọn phân quyền chi tiết cho từng vai trò trên giao diện trực quan.
- **Nhật ký hệ thống**: Tra cứu lịch sử thao tác dữ liệu chi tiết, cho phép lọc theo thực thể, hành động, thời gian và xem chi tiết giá trị cũ/mới dạng JSON.
- **Tự động ẩn hiện menu**: Hệ thống tự động lọc và chỉ hiển thị các chức năng menu trên sidebar tương ứng với quyền hạn thực tế của người dùng.
- **Phiếu nhập hàng (Inbound Orders)**: Tạo mới và quản lý danh sách phiếu nhập PO/Invoice với giao diện trực quan.
- **Nhận hàng thực tế (Receiving)**: Ghi nhận số lượng thực nhận, kiểm soát dung sai (tolerance) cho phép, tự động sinh hoặc khớp số lô (Lot no), ngày sản xuất, hạn sử dụng và lưu vị trí kho nhận hàng chi tiết.
- **Tra cứu lô hàng (Lots Search)**: Tra cứu nhanh thông tin ngày sản xuất, hạn sử dụng và trạng thái QC của lô hàng thực tế theo số lô.

### Cải tiến và tối ưu
- **Quay vòng mã Token**: Tích hợp cơ chế Refresh Token Rotation và ngăn chặn tấn công replay attack tự động phía Client-side.
- **Ghi log thay đổi tự động**: Tích hợp SaveChangesInterceptor ghi nhận lịch sử thay đổi thực thể tự động trên database.
- **Khắc phục lỗi ghi log PostgreSQL**: Sửa lỗi tương thích kiểu dữ liệu DBNull khi ghi nhận log thay đổi trên cơ sở dữ liệu PostgreSQL.
- **Kiểm soát dung sai nghiêm ngặt**: Tích hợp phân quyền phê duyệt vượt dung sai (Inbound.Orders.Approve) ngay tại API Controller backend.

## Phiên bản 0.2.0 - Quản lý danh mục nền tảng (Phase 2)
*Ngày cập nhật: 02/07/2026*

### Tính năng mới
- **Quản lý danh mục vật tư**: Cho phép xem danh sách, thêm mới, cập nhật thông tin chi tiết của các loại vật tư và đơn vị tính (UOM).
- **Import dữ liệu 2 bước**: Hỗ trợ tải dữ liệu hàng loạt từ file mẫu. Hệ thống tự động kiểm tra và chỉ ra các lỗi dữ liệu trực quan trước khi xác nhận lưu chính thức.
- **Tải tệp mẫu và tệp lỗi**: Cho phép tải tệp dữ liệu mẫu để nhập liệu dễ dàng. Khi có lỗi import, hệ thống xuất tệp lỗi chỉ rõ nguyên nhân ở từng dòng.
- **Tài liệu hướng dẫn tích hợp**: Hệ thống API được chuẩn hóa và tài liệu hóa trực quan giúp kết nối dễ dàng.
- **Thông báo hệ thống trực quan**: Tích hợp thư viện Sonner toast thông minh giúp hiển thị thông báo thành công, cảnh báo, lỗi đẹp mắt và đồng bộ.

### Cải tiến và tối ưu
- **Xác nhận thao tác đồng bộ**: Thay hộp xác nhận mặc định của trình duyệt bằng dialog hiện đại, rõ ràng và đồng nhất với giao diện hệ thống.
- **Khắc phục lỗi form Thêm mới**: Sửa lỗi logic điều khiển dialog giúp nút "Thêm mới" hoạt động mượt mà trên tất cả các trang Master Data.
- **Độ tin cậy dữ liệu**: Đảm bảo toàn bộ dữ liệu import được lưu trữ đồng bộ, không xảy ra tình trạng lưu trữ nửa chừng khi có lỗi phát sinh.
- **Tăng tốc độ tìm kiếm**: Cải thiện hiệu năng tải danh sách danh mục vật tư và kho bãi.