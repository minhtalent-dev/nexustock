/**
 * DBM full P31 inventory (44 pages) + P31a runtime catalogs.
 * Admin: README admin@nexustock.com / AdminSecret123!
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const require = createRequire(path.resolve(__dirname, '../../frontend/package.json'));
const { chromium } = require('playwright');

const BASE = process.env.NEXUSTOCK_FE || 'http://localhost:3003';
const EMAIL = process.env.NEXUSTOCK_ADMIN_EMAIL || 'admin@nexustock.com';
const PASSWORD = process.env.NEXUSTOCK_ADMIN_PASSWORD || 'AdminSecret123!';

const outDir = path.resolve(__dirname, '../../planning/evidence/phase_31_31a_dbm_pages');
fs.mkdirSync(outDir, { recursive: true });
fs.mkdirSync(path.join(outDir, 'shots'), { recursive: true });

/** Routes từ phase_31 §26.2 — dynamic dùng placeholder rồi resolve từ list nếu có. */
const PAGES = [
  { id: '01-home', path: '/' },
  { id: '02-login', path: '/login', skipAuth: true },
  { id: '03-health-ui', path: '/health-ui' },
  { id: '04-allocation', path: '/admin/allocation' },
  { id: '05-audit', path: '/admin/audit' },
  { id: '06-cross-docking', path: '/admin/cross-docking' },
  { id: '07-cross-docking-id', path: '/admin/cross-docking', detailFrom: 'a[href*="/admin/cross-docking/"]' },
  { id: '08-cutover', path: '/admin/cutover' },
  { id: '09-exceptions', path: '/admin/exceptions' },
  { id: '10-genealogy', path: '/admin/genealogy' },
  { id: '11-genealogy-lot', path: '/admin/genealogy', detailFrom: 'a[href*="/admin/genealogy/"]' },
  { id: '12-inbound', path: '/admin/inbound' },
  { id: '13-inbound-receive', path: '/admin/inbound', detailFrom: 'a[href*="/admin/inbound/"]' },
  { id: '14-integrations-import', path: '/admin/integrations/import' },
  { id: '15-integrations-mappings', path: '/admin/integrations/mappings' },
  { id: '16-integrations-messages', path: '/admin/integrations/messages' },
  { id: '17-inventory', path: '/admin/inventory' },
  { id: '18-stocktakes', path: '/admin/inventory/stocktakes' },
  { id: '19-stocktakes-new', path: '/admin/inventory/stocktakes/new' },
  { id: '20-stocktakes-id', path: '/admin/inventory/stocktakes', detailFrom: 'a[href*="/admin/inventory/stocktakes/"]' },
  { id: '21-labor', path: '/admin/labor' },
  { id: '22-labor-sessions', path: '/admin/labor/sessions' },
  { id: '23-local-agent', path: '/admin/local-agent' },
  { id: '24-lots', path: '/admin/lots' },
  { id: '25-lpn', path: '/admin/lpn' },
  { id: '26-observability', path: '/admin/observability' },
  { id: '27-observability-alerts', path: '/admin/observability/alerts' },
  { id: '28-observability-timeline', path: '/admin/observability/timeline' },
  { id: '29-outbound', path: '/admin/outbound' },
  { id: '30-putaway', path: '/admin/putaway' },
  { id: '31-qc', path: '/admin/qc' },
  { id: '32-readiness', path: '/admin/readiness' },
  { id: '33-replenishment', path: '/admin/replenishment' },
  { id: '34-rma', path: '/admin/rma' },
  { id: '35-roles', path: '/admin/roles' },
  { id: '36-rules', path: '/admin/rules' },
  { id: '37-serial', path: '/admin/serial' },
  { id: '38-task-interleaving', path: '/admin/task-interleaving' },
  { id: '39-users', path: '/admin/users' },
  { id: '40-waves', path: '/admin/waves' },
  { id: '41-waves-id', path: '/admin/waves', detailFrom: 'a[href*="/admin/waves/"]' },
  { id: '42-waves-put-wall', path: '/admin/waves', detailFrom: 'a[href*="/admin/waves/"]', detailSuffix: '/put-wall' },
  { id: '43-webhooks-subscriptions', path: '/admin/webhooks/subscriptions' },
  { id: '44-webhooks-deliveries', path: '/admin/webhooks/deliveries' },
];

function slug(s) {
  return s.replace(/[^a-zA-Z0-9-_]/g, '_').slice(0, 80);
}

async function resolveDetailUrl(page, entry) {
  if (!entry.detailFrom) return entry.path;
  await page.goto(`${BASE}${entry.path}`, { waitUntil: 'domcontentloaded', timeout: 45000 });
  await page.waitForTimeout(800);
  const links = page.locator(entry.detailFrom);
  const n = await links.count();
  for (let i = 0; i < n; i++) {
    const href = await links.nth(i).getAttribute('href');
    if (!href) continue;
    if (href.endsWith(entry.path) || href === entry.path) continue;
    // skip "new" for stocktakes detail
    if (href.includes('/stocktakes/new')) continue;
    let url = href.startsWith('http') ? new URL(href).pathname : href;
    if (entry.detailSuffix && !url.endsWith(entry.detailSuffix)) {
      // waves/[id]/put-wall
      if (entry.id === '42-waves-put-wall') {
        const m = url.match(/\/admin\/waves\/([^/]+)/);
        if (m) url = `/admin/waves/${m[1]}/put-wall`;
      } else {
        url = url.replace(/\/$/, '') + entry.detailSuffix;
      }
    }
    // inbound receive
    if (entry.id === '13-inbound-receive') {
      const m = url.match(/\/admin\/inbound\/([^/]+)/);
      if (m && !url.includes('/receive')) url = `/admin/inbound/${m[1]}/receive`;
    }
    return url;
  }
  return null;
}

async function visit(page, urlPath, shotName, results, locale) {
  const pageErrors = [];
  const onErr = (e) => pageErrors.push(String(e));
  page.on('pageerror', onErr);
  const row = {
    id: shotName,
    path: urlPath,
    locale,
    status: 'FAIL',
    httpOk: false,
    lang: null,
    switcher: false,
    pageErrors: [],
    note: '',
  };
  try {
    const res = await page.goto(`${BASE}${urlPath}`, {
      waitUntil: 'domcontentloaded',
      timeout: 45000,
    });
    await page.waitForTimeout(900);
    row.httpOk = !!res && res.status() < 500;
    row.lang = await page.locator('html').getAttribute('lang');
    row.switcher = (await page.getByTestId('language-switcher').count()) > 0;
    // login page always has switcher; admin layout should too
    const body = await page.locator('body').innerText().catch(() => '');
    const crashed = /Application error|Unhandled Runtime Error|missing message/i.test(body);
    if (pageErrors.length || crashed) {
      row.status = 'FAIL';
      row.pageErrors = pageErrors.slice(0, 5);
      row.note = crashed ? 'UI crash text' : 'pageerror';
    } else if (!row.httpOk) {
      row.status = 'FAIL';
      row.note = `http ${res?.status()}`;
    } else if (row.lang !== locale) {
      row.status = 'WARN';
      row.note = `lang=${row.lang} expected=${locale}`;
    } else {
      row.status = 'PASS';
    }
    await page.screenshot({
      path: path.join(outDir, 'shots', `${slug(shotName)}.png`),
      fullPage: true,
    });
  } catch (e) {
    row.status = 'FAIL';
    row.note = String(e).slice(0, 200);
    await page.screenshot({
      path: path.join(outDir, 'shots', `${slug(shotName)}-error.png`),
      fullPage: true,
    }).catch(() => {});
  } finally {
    page.off('pageerror', onErr);
  }
  results.push(row);
  console.log(`${row.status} [${locale}] ${urlPath} ${row.note}`);
  return row;
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
  if (!resp.ok()) {
    throw new Error(`Login API HTTP ${resp.status()}`);
  }
  await page.waitForURL((u) => !u.pathname.includes('/login'), { timeout: 30000 });
  console.log('LOGIN OK', page.url());
}

async function setLocale(page, locale) {
  const testId = locale === 'en' ? 'language-option-en' : 'language-option-vi';
  const btn = page.getByTestId(testId);
  if ((await btn.count()) === 0) {
    // try open a page with switcher
    await page.goto(`${BASE}/admin/users`, { waitUntil: 'domcontentloaded' });
  }
  await page.getByTestId(testId).click();
  await page.waitForTimeout(1200);
  const lang = await page.locator('html').getAttribute('lang');
  if (lang !== locale) throw new Error(`Failed set locale ${locale}, got ${lang}`);
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    recordVideo: { dir: outDir, size: { width: 1400, height: 900 } },
    viewport: { width: 1400, height: 900 },
  });
  const page = await context.newPage();
  const results = [];
  const resolvedPaths = {};

  try {
    // login page first (public)
    await visit(page, '/login', '02-login-vi', results, 'vi');
    await login(page);

    // resolve detail URLs once
    for (const entry of PAGES) {
      if (entry.skipAuth && entry.path === '/login') continue;
      if (entry.detailFrom) {
        const u = await resolveDetailUrl(page, entry);
        resolvedPaths[entry.id] = u;
        if (!u) console.log(`SKIP_DETAIL_NO_DATA ${entry.id}`);
      } else {
        resolvedPaths[entry.id] = entry.path;
      }
    }

    async function runLocale(locale) {
      await setLocale(page, locale);
      for (const entry of PAGES) {
        if (entry.path === '/login') {
          // already covered; skip authenticated pass for login or visit briefly
          if (locale === 'en') {
            // logout not needed; skip
          }
          continue;
        }
        let p = resolvedPaths[entry.id];
        if (entry.detailFrom && !p) {
          results.push({
            id: `${entry.id}-${locale}`,
            path: entry.path + '/*',
            locale,
            status: 'SKIP',
            note: 'no detail row in DB',
            httpOk: true,
            lang: locale,
            switcher: true,
            pageErrors: [],
          });
          console.log(`SKIP [${locale}] ${entry.id} no data`);
          continue;
        }
        // inbound receive: if we got detail without /receive, force
        if (entry.id === '13-inbound-receive' && p && !p.includes('/receive')) {
          const m = p.match(/\/admin\/inbound\/([^/]+)/);
          if (m) p = `/admin/inbound/${m[1]}/receive`;
        }
        await visit(page, p, `${entry.id}-${locale}`, results, locale);
      }
    }

    await runLocale('vi');
    await runLocale('en');

    // P31a assert: no FAIL with pageerror
    const fails = results.filter((r) => r.status === 'FAIL');
    const skips = results.filter((r) => r.status === 'SKIP');
    const passes = results.filter((r) => r.status === 'PASS' || r.status === 'WARN');

    const summary = {
      ok: fails.length === 0,
      totalRows: results.length,
      pass: passes.length,
      skip: skips.length,
      fail: fails.length,
      inventoryTarget: 44,
      admin: EMAIL,
      base: BASE,
      fails: fails.map((f) => ({ id: f.id, path: f.path, note: f.note, pageErrors: f.pageErrors })),
      skips: skips.map((s) => s.id),
    };

    fs.writeFileSync(path.join(outDir, 'dbm_pages_result.json'), JSON.stringify(summary, null, 2));
    fs.writeFileSync(path.join(outDir, 'dbm_pages_detail.json'), JSON.stringify(results, null, 2));

    // markdown matrix
    const lines = [
      '# DBM Pages Matrix — P31 + P31a',
      '',
      `| Metric | Value |`,
      `|---|---|`,
      `| PASS/WARN | ${passes.length} |`,
      `| SKIP (no data) | ${skips.length} |`,
      `| FAIL | ${fails.length} |`,
      `| ok | ${summary.ok} |`,
      '',
      '| ID | Locale | Path | Status | Note |',
      '|---|---|---|---|---|',
      ...results.map(
        (r) =>
          `| ${r.id} | ${r.locale} | \`${r.path}\` | ${r.status} | ${(r.note || '').replace(/\|/g, '/')} |`
      ),
      '',
    ];
    fs.writeFileSync(path.join(outDir, 'matrix.md'), lines.join('\n'));

    console.log(JSON.stringify(summary));
    if (!summary.ok) process.exitCode = 1;
  } finally {
    await context.close();
    await browser.close();
    const videos = fs.readdirSync(outDir).filter((f) => f.endsWith('.webm'));
    if (videos.length) {
      const src = path.join(outDir, videos[0]);
      const dest = path.join(outDir, 'walkthrough-all-pages.webm');
      if (src !== dest) {
        if (fs.existsSync(dest)) fs.unlinkSync(dest);
        fs.renameSync(src, dest);
      }
      console.log('VIDEO', dest);
    }
  }
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
