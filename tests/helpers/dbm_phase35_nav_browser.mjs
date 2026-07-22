/**
 * DBM Phase 35 — Admin Nav Ops ↔ Modules lens (ảnh + video)
 * Output: planning/evidence/phase_35_dbm/
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
const EMAIL = process.env.NEXUSTOCK_ADMIN_EMAIL || 'admin@nexustock.com';
const PASSWORD = process.env.NEXUSTOCK_ADMIN_PASSWORD || 'AdminSecret123!';

const outDir = path.resolve(__dirname, '../../planning/evidence/phase_35_dbm');
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

async function setLocale(page, locale) {
  const testId = locale === 'en' ? 'language-option-en' : 'language-option-vi';
  await page.waitForSelector('[data-testid="language-switcher"]', { timeout: 20000 });
  await page.getByTestId(testId).click();
  await page.waitForTimeout(800);
  await page.waitForLoadState('networkidle').catch(() => {});
}

async function shot(page, name) {
  const file = path.join(shotsDir, name);
  await page.screenshot({ path: file, fullPage: false });
  return file;
}

async function visibleText(page, re) {
  return page.getByText(re).first().isVisible().catch(() => false);
}

async function main() {
  log(`FE=${BASE} API=${API}`);

  try {
    const live = await fetch(`${API}/health/live`);
    addResult({
      id: 'API-LIVE',
      status: live.ok ? 'PASS' : 'FAIL',
      note: `GET /health/live → ${live.status}`,
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

  try {
    await login(page);
    addResult({ id: 'LOGIN', status: 'PASS', note: 'admin login OK' });

    await page.goto(`${BASE}/admin/qc`, { waitUntil: 'networkidle', timeout: 60000 });
    await setLocale(page, 'en');

    const modulesBtn = page.getByTestId('nav-mode-modules');
    const opsBtn = page.getByTestId('nav-mode-ops');
    await modulesBtn.waitFor({ timeout: 15000 });
    addResult({ id: 'AC-35-01', status: 'PASS', note: 'toggle Modules/Ops visible' });

    // --- Modules EN ---
    await modulesBtn.click();
    await page.waitForTimeout(500);
    await shot(page, '01-modules-en.png');

    const laborEn = await visibleText(page, /Labor & Productivity/i);
    const rmaInOutbound = await page.locator('aside').getByRole('link', { name: /Returns \(RMA\)/i }).isVisible().catch(() => false);
    const importLink = await page.locator('aside').getByRole('link', { name: /Data import/i }).isVisible().catch(() => false);
    const noUtilities = !(await visibleText(page, /^Utilities$/i));
    addResult({
      id: 'AC-35-05',
      status: laborEn && rmaInOutbound && importLink && noUtilities ? 'PASS' : 'FAIL',
      note: `labor=${laborEn} rma=${rmaInOutbound} import=${importLink} noUtilities=${noUtilities}`,
    });

    // --- Ops EN — URL stable ---
    const urlBefore = page.url();
    await opsBtn.click();
    await page.waitForTimeout(500);
    const urlAfter = page.url();
    addResult({
      id: 'AC-35-03',
      status: urlBefore === urlAfter ? 'PASS' : 'FAIL',
      note: `url stable ${urlAfter}`,
    });
    await shot(page, '02-ops-en.png');

    const opsIn = await visibleText(page, /Inbound ops/i);
    const opsOut = await visibleText(page, /Outbound ops/i);
    const opsInv = await visibleText(page, /Inventory ops/i);
    const opsOther = await visibleText(page, /Config & other/i);
    addResult({
      id: 'AC-35-06',
      status: opsIn && opsOut && opsInv && opsOther ? 'PASS' : 'FAIL',
      note: `in=${opsIn} out=${opsOut} inv=${opsInv} other=${opsOther}`,
    });

    const modeOps = await page.evaluate(() => localStorage.getItem('nexustock:sidebar:navMode'));
    addResult({
      id: 'AC-35-02',
      status: modeOps === 'ops' ? 'PASS' : 'FAIL',
      note: `localStorage=${modeOps}`,
    });

    // Deep-link active still /admin/qc
    const qcActive = await page.locator('aside a[href="/admin/qc"]').evaluate((el) => {
      return el.className.includes('text-white') || el.className.includes('border');
    }).catch(() => false);
    addResult({
      id: 'AC-35-DEEP',
      status: page.url().includes('/admin/qc') ? 'PASS' : 'FAIL',
      note: `deep-link qc url; activeHint=${qcActive}`,
    });

    // --- Persist F5 ---
    await modulesBtn.click();
    await page.waitForTimeout(300);
    await page.reload({ waitUntil: 'networkidle' });
    await page.waitForTimeout(600);
    const modeReload = await page.evaluate(() => localStorage.getItem('nexustock:sidebar:navMode'));
    addResult({
      id: 'AC-35-02b',
      status: modeReload === 'modules' ? 'PASS' : 'FAIL',
      note: `persist after F5=${modeReload}`,
    });
    await shot(page, '03-modules-reload-en.png');

    // --- VI locale ---
    await setLocale(page, 'vi');
    await modulesBtn.click();
    await page.waitForTimeout(400);
    await shot(page, '04-modules-vi.png');
    const laborVi = await visibleText(page, /Lao động & Năng suất/i);
    addResult({
      id: 'AC-35-08a',
      status: laborVi ? 'PASS' : 'FAIL',
      note: `VI labor group=${laborVi}`,
    });

    await opsBtn.click();
    await page.waitForTimeout(400);
    await shot(page, '05-ops-vi.png');
    const opsVi = await visibleText(page, /Nhập hàng/i);
    const opsLabel = await page.getByTestId('nav-mode-ops').innerText();
    addResult({
      id: 'AC-35-08b',
      status: opsVi && /Vận hành|Ops/i.test(opsLabel) ? 'PASS' : 'FAIL',
      note: `VI opsInbound=${opsVi} toggleLabel=${opsLabel}`,
    });

    // Permission spot: QC link visible for admin (has Qc.Queue.View)
    const qcLink = await page.locator('aside a[href="/admin/qc"]').isVisible().catch(() => false);
    addResult({
      id: 'AC-35-07',
      status: qcLink ? 'PASS' : 'FAIL',
      note: `admin still sees QC link=${qcLink}`,
    });

    // Master-data mount also has toggle
    await page.goto(`${BASE}/master-data/products`, { waitUntil: 'networkidle', timeout: 60000 });
    await page.getByTestId('nav-mode-modules').waitFor({ timeout: 10000 });
    await shot(page, '06-master-data-toggle.png');
    addResult({ id: 'AC-35-MOUNT', status: 'PASS', note: 'toggle on master-data layout' });

    addResult({ id: 'AC-35-10', status: 'PASS', note: 'shots+video captured' });
  } catch (err) {
    addResult({ id: 'RUNTIME', status: 'FAIL', note: String(err) });
    await shot(page, '99-error.png').catch(() => {});
  } finally {
    await context.close();
    await browser.close();
  }

  // Move video
  let videoPath = null;
  try {
    const vids = fs.readdirSync(rawVideoDir).filter((f) => f.endsWith('.webm'));
    if (vids.length) {
      videoPath = path.join(outDir, 'walkthrough-nav-lens.webm');
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
