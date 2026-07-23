import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const require = createRequire(path.join(root, "frontend/package.json"));
const { chromium } = require("playwright");

const BASE = process.env.NEXUSTOCK_FE || "http://localhost:3003";
const EMAIL = process.env.NEXUSTOCK_ADMIN_EMAIL || "admin@nexustock.com";
const PASSWORD = process.env.NEXUSTOCK_ADMIN_PASSWORD || "AdminSecret123!";
const shots = path.join(root, "planning/evidence/phase_39_dbm/shots");

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });

await page.goto(`${BASE}/login`, { waitUntil: "domcontentloaded", timeout: 60000 });
await page.fill("#email", EMAIL);
await page.fill("#password", PASSWORD);
await page.click('button[type="submit"]');
await page
  .waitForURL((u) => !u.pathname.includes("/login"), { timeout: 45000 })
  .catch(() => null);

async function shot(theme, file) {
  await page.evaluate((t) => localStorage.setItem("nexustock:theme", t), theme);
  await page.goto(`${BASE}/mobile/movement`, {
    waitUntil: "domcontentloaded",
    timeout: 60000,
  });
  await page
    .locator('[data-testid="theme-switcher-inline"]')
    .waitFor({ state: "visible", timeout: 20000 });
  await page.waitForTimeout(900);
  const dark = await page.evaluate(() =>
    document.documentElement.classList.contains("dark")
  );
  const inputBg = await page
    .locator("#fromLocScan")
    .evaluate((el) => getComputedStyle(el).backgroundColor)
    .catch(() => "n/a");
  await page.screenshot({ path: path.join(shots, file), fullPage: false });
  console.log(JSON.stringify({ theme, file, dark, inputBg }));
}

await shot("light", "04-mobile-light.png");
await shot("dark", "07-mobile-dark.png");
await page.screenshot({
  path: path.join(shots, "10-mobile-inline.png"),
  fullPage: false,
});
await browser.close();
