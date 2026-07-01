# Local Agent Threat Model — Nexustock WMS

> **Scope:** Local Agent LAN deployment (FOUNDER decision: bind 0.0.0.0, phục vụ nhiều máy trạm trong subnet kho)
> **Prerequisite:** Xem [security_model.md §2](./security_model.md) cho pairing flow và DPAPI spec
> **Nâng maturity:** Phase 20 → 95% execution-ready

---

## 1. System Context

```
[Web Browser HTTPS] <--WebSocket Secure (wss://--> [Local Agent :9000 LAN]
                                                         |
                                                    [COM Port Scale]
                                                    [ZPL Printer LAN/USB]
                                                    [TSPL Printer USB]
```

**LAN scope (v1.0):** Agent bind `0.0.0.0:9000` — tất cả máy trạm trong warehouse subnet có thể kết nối. Không expose ra internet (firewall rule bắt buộc).

---

## 2. STRIDE Threat Matrix

### S — Spoofing (Giả mạo danh tính)

| Threat | Attack scenario | Mitigation | Test case |
|---|---|---|---|
| S-01 | Trang web độc hại (attacker.com) kết nối WebSocket tới Agent | Origin allowlist validation tại handshake | TC-S01: Mở kết nối từ origin không có trong whitelist → expect reject |
| S-02 | Máy tính trong LAN không phải máy trạm WMS kết nối Agent | Pairing token bắt buộc sau handshake | TC-S02: Kết nối không có pairing token → expect close(4001 Unauthorized) |
| S-03 | Attacker giả mạo Cloud API để cấp pairing token giả | Agent verify token với Cloud API qua HTTPS (TLS cert validation) | TC-S03: Agent nhận pairing token từ endpoint không hợp lệ → expect reject |

**Mitigation implementation:**
```csharp
// Kiểm tra Origin header tại WebSocket handshake
if (!_allowedOrigins.Contains(context.Request.Headers["Origin"].ToString()))
{
    context.Response.StatusCode = 403;
    await context.Response.WriteAsync("Origin not allowed");
    return;
}
```

---

### T — Tampering (Giả mạo dữ liệu)

| Threat | Attack scenario | Mitigation | Test case |
|---|---|---|---|
| T-01 | MITM intercept WebSocket message, thay đổi scale weight | HMAC-SHA256 signature + timestamp trên mỗi message | TC-T01: Modify payload bytes → Agent reject do signature mismatch |
| T-02 | Replay attack: gửi lại message in nhãn cũ | Timestamp validation: reject nếu message timestamp > 30 giây lệch | TC-T02: Replay message với timestamp cũ → expect reject |
| T-03 | Modify AgentToken lưu trên disk | DPAPI encryption — decrypt chỉ với Windows user context của service | TC-T03: Copy agent config sang máy khác → DPAPI decrypt fail |

**HMAC validation:**
```csharp
var expectedSig = ComputeHmac(payload, agentToken);
if (!CryptographicOperations.FixedTimeEquals(
    Encoding.UTF8.GetBytes(receivedSig),
    Encoding.UTF8.GetBytes(expectedSig)))
{
    // Reject message
}
// Timestamp check
if (Math.Abs((DateTimeOffset.UtcNow - messageTimestamp).TotalSeconds) > 30)
{
    // Reject — potential replay
}
```

---

### R — Repudiation (Chối bỏ hành động)

| Threat | Attack scenario | Mitigation | Test case |
|---|---|---|---|
| R-01 | Thủ kho chối đã in nhãn sai | Mọi print job ghi audit log với stationId, userId, timestamp, printedPayload hash | TC-R01: In nhãn → verify AuditLogs có bản ghi đầy đủ |
| R-02 | Không xác định được máy trạm nào tạo scale reading | Mỗi WebSocket session gắn stationId sau pairing | TC-R02: Scale reading → trace về stationId cụ thể trong log |

---

### I — Information Disclosure (Lộ thông tin)

| Threat | Attack scenario | Mitigation | Test case |
|---|---|---|---|
| I-01 | AgentToken lưu plaintext trong file config | DPAPI encryption bắt buộc — không ghi clear-text | TC-I01: Đọc file config Agent → không thấy token dạng plaintext |
| I-02 | Log Agent ghi thông tin nhạy cảm | Secret masking trong log: token, password, PII | TC-I02: Trigger print job → log không chứa token/password |
| I-03 | Attacker scan LAN port 9000 để fingerprint hệ thống | Agent không trả version info ở handshake reject response | TC-I03: Gửi HTTP GET /health → 404 hoặc empty response |

---

### D — Denial of Service (Từ chối dịch vụ)

| Threat | Attack scenario | Mitigation | Test case |
|---|---|---|---|
| D-01 | Flood WebSocket connections từ LAN | Max concurrent connections limit: 20 per Agent | TC-D01: Mở 25 WS connections → 21+ bị reject |
| D-02 | Spam print job làm máy in hỏng | Print job queue có rate limit: 10 jobs/phút per station | TC-D02: Gửi 50 print jobs nhanh → 11+ bị queue/throttle |
| D-03 | COM port spam gây nhiễu scale | Scale reading throttle: chỉ emit stable reading (nhiễu filter), không stream raw | TC-D03: Scale nhiễu liên tục → UI không bị spam update |

```csharp
// Max connections
if (_activeConnections.Count >= MaxConcurrentConnections)
{
    context.Response.StatusCode = 503;
    return;
}
```

---

### E — Elevation of Privilege (Leo thang đặc quyền)

| Threat | Attack scenario | Mitigation | Test case |
|---|---|---|---|
| E-01 | Exploit Agent để chạy lệnh OS với SYSTEM privilege | **Service Account: phải là `Network Service` hoặc custom low-privilege account — KHÔNG phải SYSTEM/Local Admin** | TC-E01: `sc qc NexustockLocalAgent` → verify `START_TYPE` và `SERVICE_START_NAME` không phải LocalSystem |
| E-02 | WebSocket message inject OS command qua ZPL/COM | Sanitize mọi input: ZPL template chỉ nhận data variables, không cho phép raw command injection | TC-E02: Gửi ZPL với `^FX<script>` → Agent sanitize, không forward raw |
| E-03 | Attacker chiếm AgentToken → escalate đến Cloud API | AgentToken chỉ có quyền agent-level (không phải user JWT) — phạm vi quyền hạn chế | TC-E03: Dùng AgentToken gọi business API → expect 403 Forbidden |

**Service account setup (PowerShell):**
```powershell
# Tạo service account chuyên dụng
$password = ConvertTo-SecureString "StrongPassword123!" -AsPlainText -Force
New-LocalUser -Name "nexustock-agent" -Password $password -Description "Nexustock Local Agent Service Account"

# Cấp quyền tối thiểu: chỉ Logon as Service
# KHÔNG add vào Administrators group
sc.exe config NexustockLocalAgent obj= ".\nexustock-agent" password= "StrongPassword123!"

# Verify
sc.exe qc NexustockLocalAgent
# SERVICE_START_NAME phải là: .\nexustock-agent (KHÔNG phải LocalSystem)
```

---

## 3. AgentToken Rotation Policy

| Event | Action |
|---|---|
| FOUNDER / Admin revoke station qua Web UI | Backend blacklist token ngay → Agent nhận WS close(4003 Token Revoked) → phải re-pair |
| Agent service restart (upgrade) | Token vẫn valid (lưu DPAPI, persist) — không cần re-pair |
| Token compromise suspected | Admin revoke từ Web UI, yêu cầu re-pair thủ công |
| Periodic rotation | Không bắt buộc tự động (token không có expiry) — rotation khi có lý do bảo mật |

**Revoke mechanism:**
```
Admin [Web UI] -> POST /api/stations/{stationId}/revoke
  -> Backend: mark stationId token_revoked = true
  -> Next WS message from Agent: verify token -> fail -> send close(4003)
  -> Agent: clear local token, status = Unpaired
  -> Web UI: station badge = "Revoked, needs re-pair"
```

---

## 4. Out-of-scope Threats (v1.0)

| Threat | Lý do out-of-scope |
|---|---|
| Network-level MITM giữa browser và Agent (LAN) | wss:// với self-signed cert bảo vệ transport. Nếu attacker đã vào được LAN subnet thì là physical security issue — ngoài phạm vi WMS |
| Agent compromise qua supply chain attack (.NET runtime) | Phụ thuộc vào Microsoft patching — monitor CVE, update .NET runtime định kỳ |
| Physical theft máy tính chạy Agent | Physical security của kho — ngoài phạm vi WMS |

---

## 5. Security Test Checklist (trước khi deploy Phase 20)

- [ ] TC-S01: Origin allowlist reject
- [ ] TC-S02: No-token connection reject
- [ ] TC-T01: HMAC signature validation
- [ ] TC-T02: Replay attack (timestamp > 30s) reject
- [ ] TC-I01: AgentToken không phải plaintext
- [ ] TC-D01: Max connection limit enforce
- [ ] TC-E01: Service account không phải SYSTEM
- [ ] TC-E03: AgentToken không có business API access
- [ ] Revoke station: Agent re-pair sau revoke
- [ ] LAN firewall rule: port 9000 blocked từ internet
