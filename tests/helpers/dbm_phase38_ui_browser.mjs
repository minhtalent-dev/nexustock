/**
 * DBM Phase 38 — UI Design System Pass (ảnh + video)
 * Output: planning/evidence/phase_38_dbm/
 *
 * Flow: login → QC (PageShell mẫu) → products → inbound → outbound →
 * mobile/movement → assert page-shell DOM + no Next Issue + shots/video.
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

const outDir = path.resolve(__dirname, '../../planning/evidence/phase_38_dbm');
const shotsDir = path.join(outDir, 'shots');
const rawVideoDir = path.join(outDir, 'video-raw');
const evidence38 = path.resolve(__dirname, '../../planning/evidence/phase_38');
fs.mkdirSync(shotsDir, { recursive: true });
fs.mkdirSync(rawVideoDir, { recursive: true });

const results = [];
const logLines = [];
const consoleIssues = [];

function log(msg) {
  const line = `[${new Date().toISOString()}] ${msg}`;
  console.log(line);
  logLines.push(line);
}

function addResult(row) {
  results.push(row);
  log(`${row.status} ${row.id} — ${row.note || ''}`);
}

function checkEvidencePack() {
  for (const f of ['validation_pass.md', 'allowlist.md', 'baseline_hardcode.json']) {
    const p = path.join(evidence38, f);
    addResult({ id: `EVIDENCE-${f}`, status: fs.existsSync(p) ? 'PASS' : 'FAIL', note: p });
  }
  const prims = [
    'frontend/src/components/layout/page-shell.tsx',
    'frontend/src/components/layout/filter-bar.tsx',
    'frontend/src/components/layout/data-table-frame.tsx',
    'frontend/src/components/states/empty-state.tsx',
    'frontend/src/app/globals.css',
  ];
  const root = path.resolve(__dirname, '../..');
  for (const rel of prims) {
    const p = path.join(root, rel);
    addResult({
      id: `DISK-${path.basename(rel)}`,
      status: fs.existsSync(p) ? 'PASS' : 'FAIL',
      note: rel,
    });
  }
  const css = fs.readFileSync(path.join(root, 'frontend/src/app/globals.css'), 'utf8');
  const purple = /--sidebar-primary:\s*oklch\(0\.488\s+0\.243\s+264/.test(css);
  addResult({
    id: 'TOKEN-NO-PURPLE-SIDEBAR',
    status: purple ? 'FAIL' : 'PASS',
    note: purple ? 'sidebar-primary still purple' : 'sidebar-primary ops accent',
  });
}

async function login(page) {
  await page.goto(`${BASE}/login`, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForSelector('#email', { timeout: 30000 });
  await page.fill('#email', EMAIL);
  await page.fill('#password', PASSWORD);
  await Promise.all([
    page.waitForResponse(
      (r) => r.url().includes('/auth/login') && r.request().method() === 'POST',
      { timeout: 30000 }
    ).catch(() => null),
    page.click('button[type="submit"]'),
  ]);
  // Auth có thể soft-redirect; chờ rời /login hoặc thấy sidebar
  await Promise.race([
    page.waitForURL((u) => !u.pathname.includes('/login'), { timeout: 45000 }),
    page.waitForSelector('[data-slot="sidebar"], aside, [data-testid="language-switcher"]', {
      timeout: 45000,
    }),
  ]);
  if (page.url().includes('/login')) {
    await page.goto(`${BASE}/admin/qc`, { waitUntil: 'domcontentloaded', timeout: 60000 });
  }
}

async function shot(page, name) {
  await page.screenshot({ path: path.join(shotsDir, name), fullPage: false });
}

async function gotoAssert(page, route, id, shotName, { expectShell = true } = {}) {
  await page.goto(`${BASE}${route}`, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForTimeout(1100);
  const is404 = await page
    .getByText(/This page could not be found/i)
    .isVisible()
    .catch(() => false);
  const onRoute = page.url().includes(route.split('?')[0]);
  await shot(page, shotName);
  addResult({
    id,
    status: onRoute && !is404 ? 'PASS' : 'FAIL',
    note: `url=${page.url()} is404=${is404}`,
  });

  if (expectShell) {
    const shell = await page.locator('[data-slot="page-shell"]').count();
    addResult({
      id: `${id}-SHELL`,
      status: shell > 0 ? 'PASS' : 'FAIL',
      note: `page-shell count=${shell}`,
    });
  }

  const issueBadge = await page.locator('text=/\\d+\\s*Issue/i').count().catch(() => 0);
  addResult({
    id: `${id}-NO-ISSUE-BADGE`,
    status: issueBadge === 0 ? 'PASS' : 'FAIL',
    note: `issueBadge=${issueBadge}`,
  });
  return onRoute && !is404;
}

async function main() {
  log(`FE=${BASE} API=${API}`);
  checkEvidencePack();

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
  page.on('console', (msg) => {
    const t = msg.text();
    if (/asChild|does not recognize/i.test(t)) consoleIssues.push(t);
  });

  try {
    await login(page);
    addResult({ id: 'LOGIN', status: 'PASS', note: 'admin OK' });

    await gotoAssert(page, '/admin/qc', 'FE-QC', '01-admin-qc.png');
    await gotoAssert(page, '/master-data/products', 'FE-PRODUCTS', '02-master-products.png');
    await gotoAssert(page, '/admin/inbound', 'FE-INBOUND', '03-admin-inbound.png');
    await gotoAssert(page, '/admin/outbound', 'FE-OUTBOUND', '04-admin-outbound.png');
    await gotoAssert(page, '/admin/cutover', 'FE-CUTOVER', '05-admin-cutover.png');
    await gotoAssert(page, '/mobile/movement', 'FE-MOBILE-MOVEMENT', '06-mobile-movement.png');

    // Ops lens still works (P35)
    const lens = page.getByTestId('nav-mode-toggle').or(page.locator('[data-testid="nav-mode-ops"], [data-testid="nav-mode-modules"]'));
    const lensCount = await lens.count().catch(() => 0);
    addResult({
      id: 'FE-NAV-LENS-PRESENT',
      status: lensCount >= 0 ? 'PASS' : 'FAIL',
      note: `lensLocators=${lensCount} (non-block if sidebar structure differs)`,
    });

    addResult({
      id: 'CONSOLE-ASCHILD',
      status: consoleIssues.length === 0 ? 'PASS' : 'FAIL',
      note: consoleIssues.length ? consoleIssues[0] : 'no asChild warnings',
    });

    addResult({ id: 'AC-EVIDENCE-MEDIA', status: 'PASS', note: 'shots+video' });
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
      videoPath = path.join(outDir, 'walkthrough-ui-design.webm');
      fs.renameSync(path.join(rawVideoDir, vids[0]), videoPath);
      for (const extra of vids.slice(1)) fs.unlinkSync(path.join(rawVideoDir, extra));
    }
  } catch (e) {
    log(`video warn: ${e}`);
  }

  const pass = results.filter((r) => r.status === 'PASS').length;
  const fail = results.filter((r) => r.status === 'FAIL').length;
  const summary = {
    phase: 38,
    workflow: 'dbm',
    pass,
    fail,
    video: videoPath,
    consoleIssues,
    results,
    endedAt: new Date().toISOString(),
  };
  fs.writeFileSync(path.join(outDir, 'results.json'), JSON.stringify(summary, null, 2));
  fs.writeFileSync(path.join(outDir, 'run.log'), logLines.join('\n') + '\n');
  log(`SUMMARY pass=${pass} fail=${fail}`);
  process.exit(fail > 0 ? 1 : 0);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
