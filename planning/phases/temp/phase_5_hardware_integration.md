# PHASE 5: TÍCH HỢP THIẾT BỊ NGOẠI VI (HARDWARE INTEGRATION WITH LOCAL AGENT)

Phase này hướng dẫn chi tiết cách xây dựng và vận hành **Local Agent** chạy ngầm dưới máy trạm Windows để làm cầu nối giữa trình duyệt Web (Next.js SPA) và các thiết bị phần cứng nhà máy (Máy quét cầm tay qua cổng COM ảo, cân điện tử đọc dữ liệu tự động, máy in nhãn qua cổng USB/LPT).

---

## 🏗️ 1. MÔ HÌNH HOẠT ĐỘNG (INTEGRATION FLOW MODEL)

Trình duyệt Web chạy trong môi trường Sandbox không thể giao tiếp trực tiếp với cổng Serial (COM) hoặc hệ thống tệp tin local của máy tính. Do đó, chúng ta sử dụng kiến trúc WebSocket Server trung gian:

```
+-----------------------------------+
|         Trình Duyệt Web           |
|        (Next.js SPA App)          |
+-----------------+-----------------+
                  |
                  | WebSocket Connection (ws://localhost:9000)
                  v
+-----------------+-----------------+
|      Local Agent (C# Service)     |
|   (Chạy ngầm ở Khay hệ thống)    |
+--------+--------+--------+--------+
         |                 |
         | COM / File      | Đọc Serial / COM Port
         | Watcher         |
         v                 v
+--------+--------+  +-----+----------+
|  Handy Scanner  |  | Cân Điện Tử    |
| (Máy quét mã)   |  | (Trọng lượng)  |
+-----------------+  +----------------+
```

---

## 🛠️ 2. XÂY DỰNG LOCAL AGENT (C# WORKER SERVICE)

Tạo dự án `Nexustock.Agent` dạng Windows Service bằng C# (.NET 8). Dự án này thực hiện 3 nhiệm vụ chính:

### A. Giám sát thư mục kết quả quét Handy Scanner (File System Watcher)
Mỗi khi nhân viên quét dữ liệu từ máy quét, thiết bị tự động xuất file kết quả dạng CSV vào thư mục cấu hình. Local Agent sẽ giám sát thư mục này để đọc dữ liệu:

```csharp
public class ScannerWatcherService : BackgroundService
{
    private readonly string _watchPath = @"C:\Nexustock\ScannerResult";
    private readonly WebSocketServerManager _wsManager;
    private FileSystemWatcher _watcher;

    public ScannerWatcherService(WebSocketServerManager wsManager)
    {
        _wsManager = wsManager;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Directory.Exists(_watchPath))
        {
            Directory.CreateDirectory(_watchPath);
        }

        _watcher = new FileSystemWatcher(_watchPath)
        {
            Filter = "*.csv",
            EnableRaisingEvents = true
        };

        _watcher.Created += OnNewScanResultDetected;

        return Task.CompletedTask;
    }

    private async void OnNewScanResultDetected(object sender, FileSystemEventArgs e)
    {
        await Task.Delay(500); 

        try
        {
            string csvContent = await File.ReadAllTextAsync(e.FullPath);
            var scanData = ParseScannerCsv(csvContent);
            await _wsManager.BroadcastAsync(scanData);
            File.Delete(e.FullPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi đọc file quét: {ex.Message}");
        }
    }
}
```

### B. Đọc trị số Cân Điện Tử qua cổng Serial (SerialPort Connection)
Local Agent kết nối trực tiếp với cổng COM kết nối với cân điện tử để tự động đọc trọng lượng thùng hàng thời gian thực:

```csharp
public class WeightScaleService : BackgroundService
{
    private readonly string _portName = "COM3"; // Cấu hình cổng COM kết nối cân
    private readonly int _baudRate = 9600;
    private SerialPort _serialPort;
    private readonly WebSocketServerManager _wsManager;

    public WeightScaleService(WebSocketServerManager wsManager)
    {
        _wsManager = wsManager;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _serialPort = new SerialPort(_portName, _baudRate, Parity.None, 8, StopBits.One);
            _serialPort.DataReceived += OnWeightDataReceived;
            _serialPort.Open();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Không thể kết nối cân điện tử tại cổng {_portName}: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    private async void OnWeightDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        string rawData = _serialPort.ReadLine();
        string cleanWeight = ExtractWeight(rawData); // Hàm parse chuỗi số cân

        // Gửi dữ liệu cân sang định dạng JSON
        var jsonMessage = $"{{\"type\": \"WEIGHT_SCALE\", \"weight\": \"{cleanWeight}\"}}";
        await _wsManager.BroadcastAsync(jsonMessage);
    }
}
```

### C. Mở WebSocket Server phục vụ Web UI
Sử dụng thư viện `WatsonWebsocket` hoặc `System.Net.WebSockets` để tạo WebSocket Server nội bộ lắng nghe cổng `9000`:

```csharp
public class WebSocketServerManager
{
    private HttpListener _listener;
    private readonly List<WebSocket> _clients = new();

    public async Task StartAsync(int port)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Start();

        while (true)
        {
            var context = await _listener.GetContextAsync();
            if (context.Request.IsWebSocketRequest)
            {
                var wsContext = await context.AcceptWebSocketAsync(null);
                _clients.Add(wsContext.WebSocket);
                _ = HandleClientAsync(wsContext.WebSocket);
            }
        }
    }

    public async Task BroadcastAsync(string message)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        var segment = new ArraySegment<byte>(buffer);

        foreach (var client in _clients.Where(c => c.State == WebSocketState.Open))
        {
            await client.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
```

---

## 🖥️ 3. TÍCH HỢP TRÊN WEB FRONTEND (NEXT.JS CLIENT)

Viết React Hook `useHardwareScanner.ts` để tự động kết nối và lắng nghe sự kiện quét từ Local Agent:

```typescript
import { useEffect, useState } from 'react';

export function useHardwareScanner(onDataReceived: (data: any) => void) {
  const [isConnected, setIsConnected] = useState(false);

  useEffect(() => {
    let ws: WebSocket;

    function connect() {
      ws = new WebSocket('ws://localhost:9000');

      ws.onopen = () => {
        setIsConnected(true);
        console.log('Đã kết nối thành công với Local Agent');
      };

      ws.onmessage = (event) => {
        try {
          const parsedData = JSON.parse(event.data);
          onDataReceived(parsedData);
        } catch (err) {
          console.error('Định dạng dữ liệu WebSocket lỗi:', event.data);
        }
      };

      ws.onclose = () => {
        setIsConnected(false);
        // Tự động kết nối lại sau 3 giây nếu mất kết nối
        setTimeout(connect, 3000);
      };
    }

    connect();

    return () => {
      if (ws) ws.close();
    };
  }, [onDataReceived]);

  return isConnected;
}
```

---

## 🖨️ 4. GIẢI PHÁP IN RAW ZPL/TSPL ĐẾN MÁY IN NHÃN MÃ VẠCH

Khi Web UI phát lệnh in, Frontend gửi API request đến Backend $\rightarrow$ Backend tạo chuỗi mã in thô (ZPL Code cho máy in Zebra, TSPL cho máy in TSC) $\rightarrow$ Backend gửi mã này qua WebSocket của Local Agent $\rightarrow$ Local Agent dùng thư viện Windows API (`RawPrinterHelper`) để đẩy thẳng mã in vào Driver máy in mà không thông qua hộp thoại Print Preview của trình duyệt.
* **Mã ZPL ví dụ**:
  ```text
  ^XA
  ^FO50,50^A0N,50,50^FDNexustock Lot No^FS
  ^FO50,120^BQN,2,10^FDQA,LOT12345678^FS
  ^XZ
  ```
* **Lợi ích**: Tốc độ in dưới 0.1 giây, tem nhãn không bị lệch khung viền và không bị giảm độ phân giải như khi in ảnh/HTML.
