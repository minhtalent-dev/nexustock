# Nexustock — Enterprise Modular Monolith Warehouse Management System

Nexustock is an enterprise-grade, production-ready Warehouse Management System (WMS) built on a modern Modular Monolith architecture. It provides high-performance, real-time inventory control, end-to-end inbound/outbound fulfillment, quality inspection, material genealogy, labor tracking, and hardware integration.

---

## 🏛️ Architecture & Tech Stack

- **Backend**: .NET 8.0 (C# Web API, Entity Framework Core 8, MediatR / CQRS pattern)
- **Frontend**: Next.js 16.2 (App Router, React 19, Tailwind CSS, Lucide Icons)
- **Database**: PostgreSQL 16 (Operational Relational Storage)
- **Cache & Message Broker**: Redis 7 (Distributed Caching, Lock Manager, Pub/Sub)
- **Background Jobs**: Hangfire (Scheduled Tasks, Queue Processing, Data Sync)
- **Hardware Integration**: LocalAgent (WebSocket Bridge for Industrial Scales, Barcode Scanners, ZPL Label Printers)
- **Observability**: OpenTelemetry, Serilog, Structured Logging, Prometheus/Grafana ready

---

## 📦 Core Modules

- **Identity & Access Management**: Role-based access control (RBAC), multi-warehouse tenancy, JWT authentication.
- **Master Data**: Warehouses, zones, aisles, racks, bins, SKUs, units of measure (UoM), partners, and CSV batch import.
- **Inbound Management**: Advanced Shipping Notices (ASN), Purchase Orders (PO), dock receiving, cross-docking, and blind receiving.
- **Inventory Control**: Real-time multi-location balances, stock adjustments, cycle counts, stock movements, and zero-negative stock enforcement.
- **Putaway & Rules Engine**: Configurable ABC velocity rules, zone affinity, dimensional/weight constraints, and optimized location suggestions.
- **Allocation & Wave Picking**: Multi-order batch allocation, zone picking, cluster picking, and pick path optimization.
- **Outbound Fulfillment**: Sales order processing, packing, cartonization, shipping manifest, and carrier integration.
- **Quality Control (QC / IQC)**: Lot hold/release workflows, sampling plans, inspection checklists, and quarantine management.
- **RMA (Return Merchandise Authorization)**: Customer/vendor returns, inspection, restocking, and scrap disposition.
- **LPN (License Plate Number) & Serial Tracking**: Pallet/tote identification, containerization, and unit-level serial traceability.
- **Material Genealogy**: Comprehensive forward/backward lot lineage, transformation history, and recall trace analysis.
- **Labor Tracking & Task Interleaving**: Operator productivity monitoring, standard hour benchmarks, and interleaved move-to-pick recommendations.
- **Readiness & Cutover**: System pre-flight checklist, cutover data freeze gates, and rollback runbook enforcement.
- **Webhooks & ERP Integration**: Outbox-pattern reliable webhook delivery, exponential backoff retries, and RESTful ERP connectors.
- **Storage & Files Hub**: Multi-provider secure attachment management (S3 / Local / MinIO) with safe document preview.

---

## 📐 Architecture Topology

```
+-----------------------------------------------------------------------------------+
|                                 Client Layer                                      |
|  +---------------------------+  +---------------------------+  +---------------+  |
|  | Web Admin (Next.js App)   |  | RF / Mobile Handheld Scan |  | Health UI     |  |
|  +---------------------------+  +---------------------------+  +---------------+  |
+----------------------------------------+------------------------------------------+
                                         | HTTP / REST / WebSocket
                                         v
+-----------------------------------------------------------------------------------+
|                        Nexustock Modular Monolith (.NET 8)                        |
|                                                                                   |
|  [ API Host / Gateway / Auth Middleware / Global Exception Handling / Health ]    |
|                                                                                   |
|  +------------------+  +------------------+  +------------------+                 |
|  | Identity Module  |  | MasterData Module|  | Inbound Module   |                 |
|  +------------------+  +------------------+  +------------------+                 |
|  +------------------+  +------------------+  +------------------+                 |
|  | Inventory Module |  | Putaway & Alloc  |  | Outbound Module  |                 |
|  +------------------+  +------------------+  +------------------+                 |
|  +------------------+  +------------------+  +------------------+                 |
|  | QC & RMA Module  |  | LPN & Serial     |  | Genealogy Module |                 |
|  +------------------+  +------------------+  +------------------+                 |
|  +------------------+  +------------------+  +------------------+                 |
|  | Labor & Tasks    |  | Webhooks Engine  |  | Files Hub & Sec  |                 |
|  +------------------+  +------------------+  +------------------+                 |
+-------------------+--------------------+--------------------+---------------------+
                    |                    |                    |
                    v                    v                    v
           +-----------------+  +-----------------+  +-----------------+
           |  PostgreSQL 16  |  |     Redis 7     |  |    LocalAgent   |
           |  (Primary DB)   |  | (Cache / Lock)  |  | (Hardware WSS)  |
           +-----------------+  +-----------------+  +-----------------+
                                                              |
                                                    +---------+---------+
                                                    |                   |
                                                    v                   v
                                             [ZPL Printers]      [Digital Scales]
```

---

## 🚀 Quick Start

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+ / npm](https://nodejs.org/)
- [Docker & Docker Compose](https://www.docker.com/)

### 1. Start Infrastructure (PostgreSQL & Redis)
```bash
docker compose up -d
```

### 2. Configure Environment
Copy development environment template:
```bash
cp .env.example .env
```

### 3. Run Backend Service
```bash
cd backend/Nexustock.Api
dotnet run
```
API Host will listen on `http://localhost:5024`.

### 4. Run Frontend Application
```bash
cd frontend
npm install
npm run dev
```
Web client will be available at `http://localhost:3003`.

### 5. Run LocalAgent (Hardware Bridge - Optional)
```bash
cd local-agent
dotnet run
```
LocalAgent WebSocket runs on `ws://localhost:5088`.

---

## 🩺 API & Health Check Endpoints

| Endpoint | Description | Expected Status |
|---|---|---|
| `GET /health/live` | Liveness probe verifying API process state | `200 OK` |
| `GET /health/ready` | Readiness probe verifying PostgreSQL & Redis connectivity | `200 OK` |
| `GET /swagger` | Swagger UI / OpenAPI specification documentation | `200 OK` |
| `GET /health-ui` | Real-time system monitoring dashboard (Frontend) | `200 OK` |

---

## 🧪 Testing & Verification Suite

Run full integration and unit test suite:
```bash
dotnet test Nexustock.sln
```

Run attachment security and storage verification:
```powershell
powershell -ExecutionPolicy Bypass -File .\tests\verify_attachment_content_p46a.ps1
```

Run end-to-end RF/Mobile operational acceptance verification:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\verify_rf_acceptance_p46e.ps1
```

---

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

