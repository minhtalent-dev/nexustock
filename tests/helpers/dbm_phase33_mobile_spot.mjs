/**
 * Spot P33 — mobile shell switcher VI↔EN + titles.
 * Output: planning/evidence/phase_33_spot/
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const frontendPkg = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../frontend/package.json');
const require = createRequire(frontendPkg);
const { chromium } = require('playwright');

const BASE = process.env.NEXUSTOCK_FE || 'http://localhost:3003';
const outDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../planning/evidence/phase_33_spot');
fs.mkdirSync(outDir, { recursive: true });

async function login(page) {
  await page.goto(`${BASE}/login`, { waitUntil: 'networkidle', timeout: 60000 });
  await page.fill('#email', 'admin@nexustock.com');
  await page.fill('#password', 'AdminSecret123!');
  await Promise.all([
    page.waitForResponse((r) => r.url().includes('/auth/login') && r.request().method() === 'POST', { timeout: 30000 }).catch(() => null),
    page.click('button[type="submit"]'),
  ]);
  await page.waitForURL((url) => !url.pathname.includes('/login'), { timeout: 30000 }).catch(() => {});
  await page.waitForTimeout(800);
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 430, height: 900 }, locale: 'vi-VN' });
  const page = await context.newPage();
  const pageErrors = [];
  page.on('pageerror', (e) => pageErrors.push(String(e)));
  const log = [];
  const step = (m) => {
    log.push(m);
    console.log(m);
  };

  try {
    await login(page);

    await page.goto(`${BASE}/mobile`, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForSelector('[data-testid="language-switcher"]', { timeout: 30000 });
    const homeVi = await page.locator('h2').first().innerText();
    await page.screenshot({ path: path.join(outDir, '01-mobile-home-vi.png'), fullPage: true });
    step(`home VI title=${homeVi}`);
    if (!homeVi.includes('Danh mục') && !homeVi.toLowerCase().includes('function')) {
      // Accept either locale if cookie leftover
    }

    await page.getByTestId('language-option-en').click();
    await page.waitForTimeout(1200);
    await page.waitForLoadState('networkidle');
    const homeEn = await page.locator('h2').first().innerText();
    const cookies = await context.cookies();
    const localeCookie = cookies.find((c) => c.name === 'NEXT_LOCALE');
    await page.screenshot({ path: path.join(outDir, '02-mobile-home-en.png'), fullPage: true });
    step(`home EN title=${homeEn} cookie=${localeCookie?.value}`);
    if (localeCookie?.value !== 'en') throw new Error(`Expected NEXT_LOCALE=en, got ${localeCookie?.value}`);
    if (homeVi === homeEn) throw new Error('Home title did not change VI→EN');

    await page.goto(`${BASE}/mobile/picking`, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForSelector('[data-testid="language-switcher"]', { timeout: 15000 });
    const pickEn = await page.locator('h2').first().innerText();
    await page.screenshot({ path: path.join(outDir, '03-picking-en.png'), fullPage: true });
    step(`picking EN=${pickEn}`);
    if (!/picking|outbound/i.test(pickEn)) throw new Error(`Unexpected picking EN title: ${pickEn}`);

    await page.getByTestId('language-option-vi').click();
    await page.waitForTimeout(1200);
    await page.waitForLoadState('networkidle');
    const pickVi = await page.locator('h2').first().innerText();
    await page.screenshot({ path: path.join(outDir, '04-picking-vi.png'), fullPage: true });
    step(`picking VI=${pickVi}`);
    if (pickVi !== 'Lấy hàng xuất kho (Picking)') {
      throw new Error(`SoT title mismatch: ${pickVi}`);
    }

    await page.goto(`${BASE}/mobile/tasks/next`, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForSelector('[data-testid="language-switcher"]', { timeout: 15000 });
    const tasksVi = await page.locator('h2').first().innerText();
    await page.screenshot({ path: path.join(outDir, '05-tasks-next-vi.png'), fullPage: true });
    step(`tasks VI=${tasksVi}`);
    if (tasksVi !== 'Gợi ý việc tiếp theo') throw new Error(`tasks title SoT mismatch: ${tasksVi}`);

    if (pageErrors.length) throw new Error(`pageerrors: ${pageErrors.join(' | ')}`);

    const result = { ok: true, homeVi, homeEn, pickVi, pickEn, tasksVi, cookie: localeCookie?.value };
    fs.writeFileSync(path.join(outDir, 'spot_result.json'), JSON.stringify(result, null, 2));
    fs.writeFileSync(path.join(outDir, 'spot_log.txt'), log.join('\n') + '\n');
    step('SPOT PASS');
  } catch (err) {
    fs.writeFileSync(path.join(outDir, 'spot_log.txt'), log.concat([String(err)]).join('\n') + '\n');
    console.error(err);
    process.exitCode = 1;
  } finally {
    await context.close();
    await browser.close();
  }
}

main();
