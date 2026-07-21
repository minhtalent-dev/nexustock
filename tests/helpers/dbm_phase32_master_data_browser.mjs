/**
 * DBM Phase 32 — Master-data i18n browser evidence (VI↔EN).
 * DoD: products + import; extended: full 8 MD pages.
 * Output: planning/evidence/phase_32_dbm/
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

const outDir = path.resolve(__dirname, '../../planning/evidence/phase_32_dbm');
fs.mkdirSync(outDir, { recursive: true });
fs.mkdirSync(path.join(outDir, 'shots'), { recursive: true });

const PAGES = [
  { id: 'products', path: '/master-data/products', titleVi: 'Vật tư', titleEn: 'Products' },
  { id: 'uoms', path: '/master-data/uoms', titleVi: 'Đơn vị tính', titleEn: 'Units of measure' },
  { id: 'warehouses', path: '/master-data/warehouses', titleVi: 'Nhà kho', titleEn: 'Warehouses' },
  { id: 'zones', path: '/master-data/zones', titleVi: 'Vùng kho', titleEn: 'Storage zones' },
  { id: 'locations', path: '/master-data/locations', titleVi: 'Vị trí kệ', titleEn: 'Storage locations' },
  { id: 'partners', path: '/master-data/partners', titleVi: 'Đối tác', titleEn: 'Partners' },
  { id: 'reasons', path: '/master-data/reasons', titleVi: 'Mã lý do', titleEn: 'Reason codes' },
  { id: 'import', path: '/master-data/import', titleVi: 'Nhập dữ liệu', titleEn: 'Import data' },
];

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
  const sw = page.getByTestId('language-switcher');
  if ((await sw.count()) === 0) {
    // master-data layout should have switcher; navigate home shell if needed
    await page.goto(`${BASE}/master-data/products`, { waitUntil: 'domcontentloaded', timeout: 45000 });
  }
  await page.getByTestId(testId).click();
  await page.waitForTimeout(1200);
  await page.waitForLoadState('networkidle').catch(() => {});
  const lang = await page.locator('html').getAttribute('lang');
  if (lang !== locale) throw new Error(`Locale switch failed: expected ${locale}, got ${lang}`);
}

async function waitReady(page) {
  // AuthGuard có thể hiện CHECKING SECURITY_ — chờ h1 trang MD
  await page.waitForFunction(
    () => {
      const body = document.body?.innerText || '';
      if (/CHECKING SECURITY/i.test(body)) return false;
      return !!document.querySelector('h1');
    },
    { timeout: 45000 }
  );
  await page.waitForTimeout(300);
}

async function visit(page, entry, locale, results) {
  const pageErrors = [];
  const onErr = (e) => pageErrors.push(String(e));
  page.on('pageerror', onErr);
  const expectedTitle = locale === 'vi' ? entry.titleVi : entry.titleEn;
  const row = {
    id: entry.id,
    path: entry.path,
    locale,
    status: 'FAIL',
    titleOk: false,
    expectedTitle,
    foundTitle: null,
    lang: null,
    pageErrors: [],
    note: '',
  };
  try {
    const res = await page.goto(`${BASE}${entry.path}`, {
      waitUntil: 'domcontentloaded',
      timeout: 45000,
    });
    await waitReady(page);
    row.lang = await page.locator('html').getAttribute('lang');
    const h1 = page.locator('h1').first();
    row.foundTitle = (await h1.count()) ? (await h1.innerText()).trim() : null;
    row.titleOk = row.foundTitle === expectedTitle;
    const body = await page.locator('body').innerText().catch(() => '');
    const crashed = /Application error|Unhandled Runtime Error|MISSING_MESSAGE|Could not find/i.test(body);
    if (pageErrors.length || crashed) {
      row.status = 'FAIL';
      row.pageErrors = pageErrors.slice(0, 5);
      row.note = crashed ? 'UI crash / missing message' : 'pageerror';
    } else if (!res || res.status() >= 500) {
      row.status = 'FAIL';
      row.note = `http ${res?.status()}`;
    } else if (row.lang !== locale) {
      row.status = 'FAIL';
      row.note = `lang=${row.lang}`;
    } else if (!row.titleOk) {
      row.status = 'FAIL';
      row.note = `title="${row.foundTitle}" expected="${expectedTitle}"`;
    } else {
      row.status = 'PASS';
    }
    await page.screenshot({
      path: path.join(outDir, 'shots', `${entry.id}-${locale}.png`),
      fullPage: true,
    });
  } catch (e) {
    row.status = 'FAIL';
    row.note = String(e).slice(0, 200);
    await page
      .screenshot({ path: path.join(outDir, 'shots', `${entry.id}-${locale}-error.png`), fullPage: true })
      .catch(() => {});
  } finally {
    page.off('pageerror', onErr);
  }
  results.push(row);
  console.log(`${row.status} [${locale}] ${entry.path} ${row.note || row.foundTitle}`);
  return row;
}

async function main() {
  const log = [];
  const step = (m) => {
    log.push(`[${new Date().toISOString()}] ${m}`);
    console.log(m);
  };

  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    recordVideo: { dir: outDir, size: { width: 1280, height: 720 } },
    viewport: { width: 1280, height: 720 },
    locale: 'vi-VN',
  });
  const page = await context.newPage();
  const results = [];

  try {
    step('Login admin');
    await login(page);
    await page.goto(`${BASE}/master-data/products`, { waitUntil: 'domcontentloaded', timeout: 45000 });
    await page.waitForTimeout(800);

    step('Set locale VI');
    await setLocale(page, 'vi');
    for (const entry of PAGES) {
      await visit(page, entry, 'vi', results);
    }

    step('Set locale EN');
    await setLocale(page, 'en');
    for (const entry of PAGES) {
      await visit(page, entry, 'en', results);
    }

    // Spot DoD: products + import must both pass VI and EN
    const spotIds = new Set(['products', 'import']);
    const spot = results.filter((r) => spotIds.has(r.id));
    const spotFail = spot.filter((r) => r.status !== 'PASS');
    const allFail = results.filter((r) => r.status !== 'PASS');

    const cookies = await context.cookies();
    const localeCookie = cookies.find((c) => c.name === 'NEXT_LOCALE');

    const summary = {
      ok: spotFail.length === 0,
      base: BASE,
      spotDoD: { required: ['products', 'import'], fail: spotFail.map((r) => `${r.id}:${r.locale}`) },
      full8: {
        total: results.length,
        pass: results.filter((r) => r.status === 'PASS').length,
        fail: allFail.map((r) => `${r.id}:${r.locale}:${r.note}`),
      },
      cookie: localeCookie?.value ?? null,
      checks: {
        productsViEn: spot.filter((r) => r.id === 'products').every((r) => r.status === 'PASS'),
        importViEn: spot.filter((r) => r.id === 'import').every((r) => r.status === 'PASS'),
        all8BothLocales: allFail.length === 0,
        cookieEnAfterSwitch: localeCookie?.value === 'en',
      },
      results,
    };

    fs.writeFileSync(path.join(outDir, 'dbm_log.txt'), log.join('\n') + '\n', 'utf8');
    fs.writeFileSync(path.join(outDir, 'dbm_result.json'), JSON.stringify(summary, null, 2), 'utf8');

    if (!summary.ok) {
      throw new Error(`Spot DoD FAIL: ${JSON.stringify(summary.spotDoD)}`);
    }
    step(`DBM PASS spot=${spot.length} fullPass=${summary.full8.pass}/${summary.full8.total}`);
  } catch (e) {
    fs.writeFileSync(path.join(outDir, 'dbm_log.txt'), log.join('\n') + `\nERROR: ${e}\n`, 'utf8');
    fs.writeFileSync(path.join(outDir, 'dbm_result.json'), JSON.stringify({ ok: false, error: String(e) }, null, 2), 'utf8');
    await page.screenshot({ path: path.join(outDir, '99-error.png'), fullPage: true }).catch(() => {});
    throw e;
  } finally {
    await context.close();
    await browser.close();
    const videos = fs.readdirSync(outDir).filter((f) => f.endsWith('.webm'));
    if (videos.length) {
      const src = path.join(outDir, videos[0]);
      const dest = path.join(outDir, 'walkthrough-master-data-i18n.webm');
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
