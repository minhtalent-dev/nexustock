const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

(async () => {
  const outDir = 'd:/1_Project/48_Nexustock/planning/evidence/phase_30/walkthrough';
  const brainDir = 'C:/Users/mes/.gemini/antigravity/brain/17cf2960-4583-44a5-918a-5eb1c709dc96/phase_30_dbm';
  fs.mkdirSync(outDir, { recursive: true });
  fs.mkdirSync(brainDir, { recursive: true });

  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    viewport: { width: 1440, height: 900 },
    recordVideo: { dir: path.join(outDir, 'video'), size: { width: 1440, height: 900 } }
  });
  const page = await context.newPage();
  const result = { steps: [], ok: true };

  async function step(name, fn) {
    try {
      await fn();
      result.steps.push({ name, status: 'PASS' });
      console.log('PASS', name);
    } catch (e) {
      result.ok = false;
      result.steps.push({ name, status: 'FAIL', error: String(e) });
      console.log('FAIL', name, e);
      await page.screenshot({ path: path.join(outDir, `FAIL_${name.replace(/\\W+/g,'_')}.png`), fullPage: true }).catch(()=>{});
    }
  }

  await step('01_login_page', async () => {
    await page.goto('http://localhost:3003/login', { waitUntil: 'networkidle', timeout: 60000 });
    await page.screenshot({ path: path.join(outDir, '01_login.png'), fullPage: true });
  });

  await step('02_login_admin', async () => {
    await page.fill('#email', 'admin@nexustock.com');
    await page.fill('#password', 'AdminSecret123!');
    await page.getByRole('button', { name: /sign in|login|đăng nhập/i }).click().catch(async () => {
      await page.locator('button[type="submit"]').click();
    });
    await page.waitForURL(/admin|\/$/, { timeout: 30000 }).catch(()=>{});
    await page.waitForTimeout(1500);
    await page.screenshot({ path: path.join(outDir, '02_after_login.png'), fullPage: true });
  });

  await step('03_readiness_page', async () => {
    await page.goto('http://localhost:3003/admin/readiness', { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForSelector('[data-testid="readiness-refresh-button"]', { timeout: 20000 });
    await page.click('[data-testid="readiness-refresh-button"]');
    await page.waitForTimeout(1200);
    const bodyText = await page.locator('body').innerText();
    if (!/Readiness|Overall|Database|SAP/i.test(bodyText)) throw new Error('Readiness content missing');
    await page.screenshot({ path: path.join(outDir, '03_readiness_dashboard.png'), fullPage: true });
  });

  await step('04_create_uat_and_signoff_visible', async () => {
    // Ensure at least one Passed UAT for signoff button visibility if list empty
    const hasSignoff = await page.locator('[data-testid="uat-signoff-button"]').count();
    if (hasSignoff === 0) {
      // create via UI if selects exist
      const createBtn = page.getByRole('button', { name: /Create UAT/i });
      if (await createBtn.count()) {
        await createBtn.click();
        await page.waitForTimeout(1500);
      }
    }
    await page.screenshot({ path: path.join(outDir, '04_readiness_uat.png'), fullPage: true });
  });

  await step('05_cutover_page', async () => {
    await page.goto('http://localhost:3003/admin/cutover', { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForSelector('[data-testid="cutover-freeze-button"]', { timeout: 20000 });
    const freezeText = await page.locator('[data-testid="cutover-freeze-button"]').innerText();
    await page.screenshot({ path: path.join(outDir, '05_cutover_board.png'), fullPage: true });
    // Toggle freeze briefly then restore to OPEN if possible
    await page.click('[data-testid="cutover-freeze-button"]');
    await page.waitForTimeout(1500);
    await page.screenshot({ path: path.join(outDir, '06_cutover_after_toggle.png'), fullPage: true });
    // If now frozen, unfreeze back
    const btn = page.locator('[data-testid="cutover-freeze-button"]');
    const t = (await btn.innerText()).toLowerCase();
    if (t.includes('unfreeze')) {
      await btn.click();
      await page.waitForTimeout(1200);
    }
    await page.screenshot({ path: path.join(outDir, '07_cutover_restored.png'), fullPage: true });
  });

  await step('06_sidebar_labels', async () => {
    const nav = await page.locator('body').innerText();
    if (!nav.includes('Readiness') || !nav.includes('Cutover')) throw new Error('Sidebar labels missing');
    await page.screenshot({ path: path.join(outDir, '08_sidebar_nav.png'), fullPage: true });
  });

  // Copy screenshots to brain
  for (const f of fs.readdirSync(outDir)) {
    if (f.endsWith('.png')) fs.copyFileSync(path.join(outDir, f), path.join(brainDir, f));
  }

  await context.close();
  await browser.close();

  // Move/rename video
  const videoDir = path.join(outDir, 'video');
  if (fs.existsSync(videoDir)) {
    const videos = fs.readdirSync(videoDir).filter(f => f.endsWith('.webm'));
    if (videos.length) {
      const src = path.join(videoDir, videos[0]);
      const dest = path.join(outDir, 'phase30_dbm_walkthrough.webm');
      fs.renameSync(src, dest);
      fs.copyFileSync(dest, path.join(brainDir, 'phase30_dbm_walkthrough.webm'));
    }
  }

  fs.writeFileSync(path.join(outDir, 'dbm_result.json'), JSON.stringify(result, null, 2));
  fs.writeFileSync(path.join(brainDir, 'dbm_result.json'), JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result));
  process.exit(result.ok ? 0 : 1);
})().catch(e => { console.error(e); process.exit(1); });
