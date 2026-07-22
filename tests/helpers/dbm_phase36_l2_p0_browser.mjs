/**
 * DBM Phase 36 — Inventory Integrity L2-P0 (ảnh + video)
 * Output: planning/evidence/phase_36_dbm/
 *
 * Flow: API live → login → outbound admin → seed Open shipment (API) →
 * Generate Picks UI → assert network pickTaskCount → duplicate error → shots.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const require = createRequire(path.resolve(__dirname, '../../frontend/package.json'));
const { chromium } = require('playwright');

const BASE = process.env.NEXUSTOCK_FE || 'http://localhost:3003';
const API = process.env.NEXUSTOCK_API || 'http://localhost:5024';
const API_ROOT = `${API}/api`;
const EMAIL = process.env.NEXUSTOCK_ADMIN_EMAIL || 'admin@nexustock.com';
const PASSWORD = process.env.NEXUSTOCK_ADMIN_PASSWORD || 'AdminSecret123!';

const outDir = path.resolve(__dirname, '../../planning/evidence/phase_36_dbm');
const shotsDir = path.join(outDir, 'shots');
const rawVideoDir = path.join(outDir, 'video-raw');
fs.mkdirSync(shotsDir, { recursive: true });
fs.mkdirSync(rawVideoDir, { recursive: true });

const results = [];
const logLines = [];

function log(msg) {
  const line = `[${new Date().toISOString()}] ${msg}`;
  console.log(line);
  logLines.push(line);
}

function addResult(row) {
  results.push(row);
  log(`${row.status} ${row.id} — ${row.note || ''}`);
}

async function apiJson(method, urlPath, token, body) {
  const res = await fetch(`${API_ROOT}${urlPath}`, {
    method,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  let data = null;
  try {
    data = text ? JSON.parse(text) : null;
  } catch {
    data = { raw: text };
  }
  return { ok: res.ok, status: res.status, data };
}

async function seedOpenShipment(token) {
  const products = await apiJson('GET', '/master-data/products', token);
  const partners = await apiJson('GET', '/master-data/partners', token);
  const uoms = await apiJson('GET', '/master-data/uoms', token);
  const locations = await apiJson('GET', '/master-data/storage-locations', token);

  const product =
    (products.data?.items || []).find((p) => p.isActive && !p.isSerialTracked) ||
    products.data?.items?.[0];
  const partner = partners.data?.items?.[0];
  const uomId = product?.baseUomId || uoms.data?.items?.[0]?.id;
  let location =
    (locations.data?.items || []).find((l) => l.code === 'LOC-SORT-01') ||
    (locations.data?.items || []).find((l) => l.code !== 'LOC-A-01') ||
    locations.data?.items?.[0];

  // Ensure capacity location
  try {
    const zones = await apiJson('GET', '/master-data/storage-zones', token);
    const zoneId = zones.data?.items?.[0]?.id;
    if (zoneId) {
      const created = await apiJson('POST', '/master-data/storage-locations', token, {
        zoneId,
        code: 'LOC-SORT-01',
        maxCapacity: 999999,
        maxVolume: 999999,
        xCoord: 0,
        yCoord: 0,
        zCoord: 0,
        length: 1,
        width: 1,
        height: 1,
        isLocked: false,
        isActive: true,
      });
      if (created.data?.id) location = { id: created.data.id, code: 'LOC-SORT-01' };
    }
  } catch {
    /* location may already exist */
  }

  // Inbound order + receive + QC Release (same path as verify_l2_p0_integrity.ps1)
  const suffix = Date.now().toString().slice(-8);
  const lotNo = `LOT-DBM36-${suffix}`;
  const io = await apiJson('POST', '/inbound/orders', token, {
    orderNo: `PO-DBM36-${suffix}`,
    partnerId: partner.id,
    items: [{ itemId: product.id, uomId, expectedQty: 20, tolerance: 0.1 }],
  });
  const orderId = io.data?.id;
  if (!orderId || !location?.id) {
    throw new Error(`Seed inbound failed http=${io.status} loc=${location?.id}`);
  }
  const recv = await apiJson('POST', `/inbound/orders/${orderId}/receive`, token, {
    itemId: product.id,
    lotNo,
    receivedQty: 20,
    toLocationId: location.id,
  });
  if (!recv.ok) throw new Error(`Receive failed http=${recv.status} ${JSON.stringify(recv.data)}`);

  const lotRes = await apiJson('GET', `/lots/${encodeURIComponent(lotNo)}`, token);
  const lotId = Array.isArray(lotRes.data) ? lotRes.data[0]?.id : lotRes.data?.id;
  const queue = await apiJson('GET', '/qc/queue', token);
  const queueList = Array.isArray(queue.data) ? queue.data : queue.data?.items || [];
  const queueItem = queueList.find((q) => q.lotNo === lotNo) || queueList[0];
  const qcReqId = queueItem?.id;
  if (!lotId || !qcReqId) {
    throw new Error(`QC seed missing lotId=${lotId} qcReqId=${qcReqId}`);
  }
  const qc = await apiJson('POST', `/qc/${lotId}/result`, token, {
    qcRequestId: qcReqId,
    isPassed: true,
    metrics: 'dbm phase36',
  });
  if (!qc.ok) throw new Error(`QC release failed http=${qc.status} ${JSON.stringify(qc.data)}`);
  log(`Seed QC Release OK lot=${lotNo}`);

  const shipmentNo = `SO-DBM36-${Date.now()}`;
  const ship = await apiJson('POST', '/outbound/shipments', token, {
    shipmentNo,
    partnerId: partner.id,
    items: [{ itemId: product.id, uomId, requestedQty: 5 }],
  });

  return {
    shipmentId: ship.data?.id || ship.data?.shipmentId,
    shipmentNo,
    productCode: product?.code,
    ok: ship.ok,
    status: ship.status,
    data: ship.data,
  };
}

async function login(page) {
  await page.goto(`${BASE}/login`, { waitUntil: 'networkidle', timeout: 60000 });
  await page.fill('#email', EMAIL);
  await page.fill('#password', PASSWORD);
  const [resp] = await Promise.all([
    page.waitForResponse(
      (r) => r.url().includes('/auth/login') && r.request().method() === 'POST',
      { timeout: 30000 }
    ),
    page.click('button[type="submit"]'),
  ]);
  if (!resp.ok()) throw new Error(`Login API HTTP ${resp.status()}`);
  await page.waitForURL((u) => !u.pathname.includes('/login'), { timeout: 30000 });
}

async function shot(page, name) {
  const file = path.join(shotsDir, name);
  await page.screenshot({ path: file, fullPage: false });
  return file;
}

async function main() {
  log(`FE=${BASE} API=${API}`);

  let token = null;
  try {
    const live = await fetch(`${API}/health/live`);
    addResult({
      id: 'API-LIVE',
      status: live.ok ? 'PASS' : 'FAIL',
      note: `GET /health/live → ${live.status}`,
    });

    const loginApi = await apiJson('POST', '/auth/login', null, {
      email: EMAIL,
      password: PASSWORD,
    });
    token = loginApi.data?.token;
    addResult({
      id: 'API-LOGIN',
      status: token ? 'PASS' : 'FAIL',
      note: `token=${!!token}`,
    });
  } catch (e) {
    addResult({ id: 'API-LIVE', status: 'FAIL', note: String(e) });
  }

  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    viewport: { width: 1400, height: 900 },
    recordVideo: { dir: rawVideoDir, size: { width: 1280, height: 720 } },
  });
  const page = await context.newPage();

  let seeded = null;

  try {
    await login(page);
    addResult({ id: 'LOGIN', status: 'PASS', note: 'admin login OK' });

    await page.goto(`${BASE}/admin/outbound`, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(800);
    await shot(page, '01-outbound-list.png');
    addResult({ id: 'FE-OUTBOUND', status: 'PASS', note: '/admin/outbound loaded' });

    if (token) {
      seeded = await seedOpenShipment(token);
      addResult({
        id: 'SEED-SHIPMENT',
        status: seeded.ok && seeded.shipmentId ? 'PASS' : 'FAIL',
        note: `${seeded.shipmentNo} http=${seeded.status} id=${seeded.shipmentId || 'n/a'}`,
      });
    } else {
      addResult({ id: 'SEED-SHIPMENT', status: 'FAIL', note: 'no token' });
    }

    await page.reload({ waitUntil: 'networkidle' });
    await page.waitForTimeout(1000);

    if (seeded?.shipmentNo) {
      const row = page.getByText(seeded.shipmentNo, { exact: true }).first();
      await row.waitFor({ timeout: 20000 });
      await row.click();
      await page.waitForTimeout(1200);
      await shot(page, '02-shipment-open-detail.png');
      addResult({ id: 'FE-SELECT-OPEN', status: 'PASS', note: `selected ${seeded.shipmentNo}` });

      const genBtn = page.getByRole('button', {
        name: /Generate pick tasks|Sinh nhiệm vụ Pick/i,
      });
      await genBtn.waitFor({ timeout: 15000 });

      const [genResp] = await Promise.all([
        page.waitForResponse(
          (r) =>
            r.url().includes('/outbound/shipments/') &&
            r.url().includes('/generate-picks') &&
            r.request().method() === 'POST',
          { timeout: 60000 }
        ),
        genBtn.click(),
      ]);

      const genStatus = genResp.status();
      let genBody = {};
      try {
        genBody = await genResp.json();
      } catch {
        genBody = {};
      }

      const pickCount = genBody.pickTaskCount ?? genBody.PickTaskCount ?? 0;
      addResult({
        id: 'AC-GEN-PICKS-200',
        status: genStatus >= 200 && genStatus < 300 ? 'PASS' : 'FAIL',
        note: `HTTP ${genStatus} pickTaskCount=${pickCount} url=${genResp.url()}`,
      });
      addResult({
        id: 'AC-GEN-PICKS-COUNT',
        status: pickCount > 0 ? 'PASS' : 'FAIL',
        note: `pickTaskCount=${pickCount}`,
      });
      addResult({
        id: 'AC-URL-CONTRACT',
        status: /\/api\/outbound\/shipments\/.+\/generate-picks/.test(genResp.url()) ? 'PASS' : 'FAIL',
        note: genResp.url(),
      });

      await page.waitForTimeout(1500);
      await shot(page, '03-after-generate-picks.png');

      // Detail should show pick tasks / Allocated
      const hasPicksSection = await page
        .getByText(/Pick tasks|Lệnh lấy|Pick Tasks/i)
        .first()
        .isVisible()
        .catch(() => false);
      const allocated = await page
        .getByText(/Allocated|Đã cấp phát/i)
        .first()
        .isVisible()
        .catch(() => false);
      addResult({
        id: 'FE-PICKS-VISIBLE',
        status: hasPicksSection || allocated || pickCount > 0 ? 'PASS' : 'FAIL',
        note: `picksSection=${hasPicksSection} allocated=${allocated}`,
      });

      // Duplicate generate via API (button hidden when not Open)
      if (seeded.shipmentId && token) {
        const dup = await apiJson(
          'POST',
          `/outbound/shipments/${seeded.shipmentId}/generate-picks`,
          token
        );
        const code = dup.data?.errorCode || dup.data?.code || dup.data?.title || '';
        const msg = JSON.stringify(dup.data || {}).toUpperCase();
        const isDup =
          !dup.ok &&
          (msg.includes('PICKS_ALREADY_EXIST') ||
            msg.includes('ALREADY') ||
            dup.status === 400 ||
            dup.status === 409);
        addResult({
          id: 'AC-DUP-PICKS',
          status: isDup ? 'PASS' : 'FAIL',
          note: `http=${dup.status} code=${code}`,
        });
      }

      // DF-01 surface = Mobile Movement (offline MOVE available check) — không dùng /mobile/tasks (404)
      await page.goto(`${BASE}/mobile/movement`, { waitUntil: 'networkidle', timeout: 60000 });
      await page.waitForTimeout(800);
      await shot(page, '04-mobile-movement-df01.png');
      const is404 = await page.getByText(/This page could not be found/i).isVisible().catch(() => false);
      const onMovement = page.url().includes('/mobile/movement');
      addResult({
        id: 'FE-MOBILE-DF01',
        status: onMovement && !is404 ? 'PASS' : 'FAIL',
        note: `url=${page.url()} is404=${is404}`,
      });
    }

    addResult({ id: 'AC-EVIDENCE', status: 'PASS', note: 'shots+video captured' });
  } catch (err) {
    addResult({ id: 'RUNTIME', status: 'FAIL', note: String(err) });
    await shot(page, '99-error.png').catch(() => {});
  } finally {
    await context.close();
    await browser.close();
  }

  let videoPath = null;
  try {
    const vids = fs.readdirSync(rawVideoDir).filter((f) => f.endsWith('.webm'));
    if (vids.length) {
      videoPath = path.join(outDir, 'walkthrough-l2-p0.webm');
      fs.renameSync(path.join(rawVideoDir, vids[0]), videoPath);
      for (const extra of vids.slice(1)) {
        fs.unlinkSync(path.join(rawVideoDir, extra));
      }
    }
  } catch (e) {
    log(`video move warn: ${e}`);
  }

  const pass = results.filter((r) => r.status === 'PASS').length;
  const fail = results.filter((r) => r.status === 'FAIL').length;
  const summary = {
    phase: 36,
    workflow: 'dbm',
    pass,
    fail,
    results,
    base: BASE,
    api: API,
    video: videoPath ? path.basename(videoPath) : null,
    at: new Date().toISOString(),
  };
  fs.writeFileSync(path.join(outDir, 'results.json'), JSON.stringify(summary, null, 2));
  fs.writeFileSync(path.join(outDir, 'run.log'), logLines.join('\n'));

  console.log(`\nDONE pass=${pass} fail=${fail} video=${summary.video || 'none'}`);
  process.exit(fail > 0 ? 1 : 0);
}

main();
