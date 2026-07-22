/**
 * DBM Phase 33 — Mobile + Errors i18n browser evidence (VI↔EN).
 * DoD: home + picking + tasks/next; extended: full 7 mobile pages.
 * Output: planning/evidence/phase_33_dbm/
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

const outDir = path.resolve(__dirname, '../../planning/evidence/phase_33_dbm');
fs.mkdirSync(outDir, { recursive: true });
fs.mkdirSync(path.join(outDir, 'shots'), { recursive: true });

const PAGES = [
  { id: 'home', path: '/mobile', titleVi: 'Danh mục chức năng', titleEn: 'Function menu' },
  {
    id: 'picking',
    path: '/mobile/picking',
    titleVi: 'Lấy hàng xuất kho (Picking)',
    titleEn: 'Outbound picking',
  },
  {
    id: 'movement',
    path: '/mobile/movement',
    titleVi: 'Dịch chuyển kho (Movement)',
    titleEn: 'Stock movement',
  },
  {
    id: 'replenishment',
    path: '/mobile/replenishment',
    titleVi: 'Bổ sung Pick Face',
    titleEn: 'Pick-face replenishment',
  },
  {
    id: 'lpn',
    path: '/mobile/lpn',
    titleVi: 'Di chuyển Pallet LPN',
    titleEn: 'LPN pallet move',
  },
  {
    id: 'serial',
    path: '/mobile/serial',
    titleVi: 'Nhận mã Serial',
    titleEn: 'Serial receive',
  },
  {
    id: 'tasks',
    path: '/mobile/tasks/next',
    titleVi: 'Gợi ý việc tiếp theo',
    titleEn: 'Suggested next task',
  },
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
  await page.waitForSelector('[data-testid="language-switcher"]', { timeout: 20000 });
  await page.getByTestId(testId).click();
  await page.waitForTimeout(1200);
  await page.waitForLoadState('networkidle').catch(() => {});
  const lang = await page.locator('html').getAttribute('lang');
  if (lang !== locale) throw new Error(`Locale switch failed: expected ${locale}, got ${lang}`);
}

async function waitReady(page) {
  await page.waitForFunction(
    () => {
      const body = document.body?.innerText || '';
      if (/CHECKING SECURITY/i.test(body)) return false;
      return !!document.querySelector('h2') || !!document.querySelector('[data-testid="language-switcher"]');
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
    switcherOk: false,
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
    row.switcherOk = (await page.getByTestId('language-switcher').count()) > 0;
    const h2 = page.locator('h2').first();
    row.foundTitle = (await h2.count()) ? (await h2.innerText()).trim() : null;
    row.titleOk = row.foundTitle === expectedTitle;
    const body = await page.locator('body').innerText().catch(() => '');
    const crashed = /Application error|Unhandled Runtime Error|MISSING_MESSAGE|Could not find|INVALID_KEY/i.test(
      body
    );
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
    } else if (!row.switcherOk) {
      row.status = 'FAIL';
      row.note = 'missing language-switcher on MobileShell';
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
    recordVideo: { dir: outDir, size: { width: 430, height: 900 } },
    viewport: { width: 430, height: 900 },
    locale: 'vi-VN',
  });
  const page = await context.newPage();
  const results = [];

  try {
    step('Login admin');
    await login(page);

    step('Open mobile home + set VI');
    await page.goto(`${BASE}/mobile`, { waitUntil: 'domcontentloaded', timeout: 45000 });
    await waitReady(page);
    await setLocale(page, 'vi');

    for (const entry of PAGES) {
      await visit(page, entry, 'vi', results);
    }

    step('Set locale EN on shell');
    await page.goto(`${BASE}/mobile`, { waitUntil: 'domcontentloaded', timeout: 45000 });
    await waitReady(page);
    await setLocale(page, 'en');

    for (const entry of PAGES) {
      await visit(page, entry, 'en', results);
    }

    const spotIds = new Set(['home', 'picking', 'tasks']);
    const spot = results.filter((r) => spotIds.has(r.id));
    const spotFail = spot.filter((r) => r.status !== 'PASS');
    const allFail = results.filter((r) => r.status !== 'PASS');

    const cookies = await context.cookies();
    const localeCookie = cookies.find((c) => c.name === 'NEXT_LOCALE');

    const summary = {
      ok: spotFail.length === 0 && allFail.length === 0,
      base: BASE,
      spotDoD: {
        required: ['home', 'picking', 'tasks'],
        fail: spotFail.map((r) => `${r.id}:${r.locale}`),
      },
      full7: {
        total: results.length,
        pass: results.filter((r) => r.status === 'PASS').length,
        fail: allFail.map((r) => `${r.id}:${r.locale}:${r.note}`),
      },
      cookie: localeCookie?.value ?? null,
      checks: {
        homeViEn: spot.filter((r) => r.id === 'home').every((r) => r.status === 'PASS'),
        pickingViEn: spot.filter((r) => r.id === 'picking').every((r) => r.status === 'PASS'),
        tasksViEn: spot.filter((r) => r.id === 'tasks').every((r) => r.status === 'PASS'),
        all7BothLocales: allFail.length === 0,
        switcherOnShell: results.every((r) => r.switcherOk),
        cookieEnAfterSwitch: localeCookie?.value === 'en',
      },
      results,
    };

    fs.writeFileSync(path.join(outDir, 'dbm_log.txt'), log.join('\n') + '\n', 'utf8');
    fs.writeFileSync(path.join(outDir, 'dbm_result.json'), JSON.stringify(summary, null, 2), 'utf8');

    if (!summary.ok) {
      throw new Error(`DBM FAIL: ${JSON.stringify({ spot: summary.spotDoD, full: summary.full7 })}`);
    }
    step(`DBM PASS spot=${spot.length} fullPass=${summary.full7.pass}/${summary.full7.total}`);
  } catch (e) {
    fs.writeFileSync(path.join(outDir, 'dbm_log.txt'), log.join('\n') + `\nERROR: ${e}\n`, 'utf8');
    fs.writeFileSync(
      path.join(outDir, 'dbm_result.json'),
      JSON.stringify({ ok: false, error: String(e) }, null, 2),
      'utf8'
    );
    await page.screenshot({ path: path.join(outDir, '99-error.png'), fullPage: true }).catch(() => {});
    throw e;
  } finally {
    await context.close();
    await browser.close();
    const videos = fs.readdirSync(outDir).filter((f) => f.endsWith('.webm'));
    if (videos.length) {
      const src = path.join(outDir, videos[0]);
      const dest = path.join(outDir, 'walkthrough-mobile-i18n.webm');
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
