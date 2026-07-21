/**
 * DBM P31/P31a — browser evidence: switcher VI↔EN, cookie, lang, catalogs load.
 * Output: planning/evidence/phase_31_31a_dbm/
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const frontendPkg = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '../../frontend/package.json'
);
const require = createRequire(frontendPkg);
const { chromium } = require('playwright');

const BASE = process.env.NEXUSTOCK_FE || 'http://localhost:3003';
const outDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../planning/evidence/phase_31_31a_dbm');
fs.mkdirSync(outDir, { recursive: true });

async function main() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    recordVideo: { dir: outDir, size: { width: 1280, height: 720 } },
    viewport: { width: 1280, height: 720 },
    locale: 'vi-VN',
  });
  const page = await context.newPage();
  const log = [];
  const pageErrors = [];
  page.on('pageerror', (e) => pageErrors.push(String(e)));

  function step(msg) {
    log.push(`[${new Date().toISOString()}] ${msg}`);
    console.log(msg);
  }

  try {
    await page.goto(`${BASE}/login`, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForSelector('[data-testid="language-switcher"]', { timeout: 30000 });
    const langVi = await page.locator('html').getAttribute('lang');
    const bodyTextVi = await page.locator('body').innerText();
    await page.screenshot({ path: path.join(outDir, '01-login-vi.png'), fullPage: true });
    step(`AC-01 login lang=${langVi}`);
    if (langVi !== 'vi') throw new Error(`Expected html lang=vi, got ${langVi}`);

    const sw = page.getByTestId('language-switcher');
    if (!(await sw.isVisible())) throw new Error('language-switcher not visible');
    step('AC-02 switcher visible on login');

    await page.getByTestId('language-option-en').click();
    await page.waitForTimeout(1500);
    await page.waitForLoadState('networkidle');
    const langEn = await page.locator('html').getAttribute('lang');
    await page.screenshot({ path: path.join(outDir, '02-login-en.png'), fullPage: true });
    const cookies = await context.cookies();
    const localeCookie = cookies.find((c) => c.name === 'NEXT_LOCALE');
    step(`After EN: lang=${langEn} cookie=${localeCookie?.value}`);
    if (langEn !== 'en') throw new Error(`Expected html lang=en, got ${langEn}`);
    if (!localeCookie || localeCookie.value !== 'en') {
      throw new Error(`Expected NEXT_LOCALE=en, got ${localeCookie?.value}`);
    }

    const bodyEn = await page.locator('body').innerText();
    step(bodyTextVi === bodyEn ? 'WARN: body text identical VI/EN' : 'Body text changed after locale switch');

    await page.reload({ waitUntil: 'networkidle' });
    const langEn2 = await page.locator('html').getAttribute('lang');
    await page.screenshot({ path: path.join(outDir, '03-login-en-reload.png'), fullPage: true });
    step(`Reload keep lang=${langEn2}`);
    if (langEn2 !== 'en') throw new Error('Locale not persisted after reload');

    await page.getByTestId('language-option-vi').click();
    await page.waitForTimeout(1500);
    await page.waitForLoadState('networkidle');
    const langVi2 = await page.locator('html').getAttribute('lang');
    await page.screenshot({ path: path.join(outDir, '04-login-vi-back.png'), fullPage: true });
    step(`Back to VI lang=${langVi2}`);
    if (langVi2 !== 'vi') throw new Error('Failed switch back to vi');

    await page.goto(`${BASE}/health-ui`, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(1000);
    await page.screenshot({ path: path.join(outDir, '05-health-ui.png'), fullPage: true });
    const hasSwHealth = await page.getByTestId('language-switcher').count();
    step(`health-ui switcher count=${hasSwHealth}`);

    await page.goto(`${BASE}/`, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(1000);
    await page.screenshot({ path: path.join(outDir, '06-home.png'), fullPage: true });
    step('home captured');

    await page.goto(`${BASE}/login`, { waitUntil: 'networkidle' });
    await page.getByTestId('language-option-en').click();
    await page.waitForTimeout(1000);
    if (pageErrors.length) throw new Error('pageerror: ' + pageErrors.join('; '));
    step('P31a: no pageerror on locale switch (module catalogs)');

    fs.writeFileSync(path.join(outDir, 'dbm_log.txt'), log.join('\n') + '\n', 'utf8');
    fs.writeFileSync(
      path.join(outDir, 'dbm_result.json'),
      JSON.stringify(
        {
          ok: true,
          base: BASE,
          checks: {
            defaultLangVi: true,
            switchToEn: true,
            cookieNextLocale: true,
            persistReload: true,
            switchBackVi: true,
            noPageError: true,
            healthUiVisited: true,
            homeVisited: true,
          },
        },
        null,
        2
      ),
      'utf8'
    );
    step('DBM PASS');
  } catch (e) {
    fs.writeFileSync(path.join(outDir, 'dbm_log.txt'), log.join('\n') + '\nERROR: ' + e + '\n', 'utf8');
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
      const dest = path.join(outDir, 'walkthrough-locale-switch.webm');
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
