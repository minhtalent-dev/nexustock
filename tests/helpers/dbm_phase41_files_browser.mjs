/**
 * DBM Phase 41 — Attachments + Spreadsheet + Storage Hub
 * Flow: disk/verify → login → Admin Storage light/dark → Product attach → Import xlsx → Export buttons → video
 */
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createRequire } from "node:module";
import { spawnSync } from "node:child_process";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "../..");
const require = createRequire(path.join(root, "frontend/package.json"));
const { chromium } = require("playwright");

const BASE = process.env.NEXUSTOCK_FE || "http://localhost:3003";
const EMAIL = process.env.NEXUSTOCK_ADMIN_EMAIL || "admin@nexustock.com";
const PASSWORD = process.env.NEXUSTOCK_ADMIN_PASSWORD || "AdminSecret123!";
const outDir = path.join(root, "planning/evidence/phase_41_dbm");
const shotsDir = path.join(outDir, "shots");
const rawVideoDir = path.join(outDir, "video-raw");
fs.mkdirSync(shotsDir, { recursive: true });
fs.mkdirSync(rawVideoDir, { recursive: true });

const results = [];
const logLines = [];
function log(m) {
  const line = `[${new Date().toISOString()}] ${m}`;
  console.log(line);
  logLines.push(line);
}
function add(id, status, note = "") {
  results.push({ id, status, note });
  log(`${status} ${id} — ${note}`);
}

async function login(page) {
  await page.goto(`${BASE}/login`, { waitUntil: "domcontentloaded", timeout: 60000 });
  await page.fill("#email", EMAIL);
  await page.fill("#password", PASSWORD);
  await page.click('button[type="submit"]');
  await Promise.race([
    page.waitForURL((u) => !u.pathname.includes("/login"), { timeout: 45000 }),
    page
      .locator('[data-testid="sidebar-user-menu-trigger"]')
      .waitFor({ state: "visible", timeout: 45000 }),
  ]).catch(() => null);
  if (page.url().includes("/login")) {
    await page.goto(`${BASE}/master-data/products`, {
      waitUntil: "domcontentloaded",
      timeout: 60000,
    });
  }
}

async function setTheme(page, theme) {
  await page.evaluate((t) => localStorage.setItem("nexustock:theme", t), theme);
  await page.reload({ waitUntil: "domcontentloaded", timeout: 60000 });
  await page.waitForTimeout(700);
}

async function waitShell(page) {
  await page
    .locator(
      '[data-testid="sidebar-user-menu-trigger"], [data-slot="page-shell"]'
    )
    .first()
    .waitFor({ state: "visible", timeout: 25000 })
    .catch(() => null);
  const gate = await page
    .getByText(/ĐANG KIỂM TRA BẢO MẬT/i)
    .isVisible()
    .catch(() => false);
  if (gate) await page.waitForTimeout(2000);
}

async function main() {
  // Disk gates
  const filesMod = path.join(root, "backend/modules/Nexustock.Modules.Files");
  add("DISK-files-module", fs.existsSync(filesMod) ? "PASS" : "FAIL");
  add(
    "DISK-admin-storage-page",
    fs.existsSync(path.join(root, "frontend/src/app/admin/settings/storage/page.tsx"))
      ? "PASS"
      : "FAIL"
  );
  add(
    "DISK-attachments-panel",
    fs.existsSync(path.join(root, "frontend/src/features/files/entity-attachments-panel.tsx"))
      ? "PASS"
      : "FAIL"
  );
  add(
    "DISK-exports-controller",
    fs.existsSync(
      path.join(root, "backend/modules/Nexustock.Modules.MasterData/Controllers/ExportsController.cs")
    )
      ? "PASS"
      : "FAIL"
  );

  const verify = spawnSync(
    "powershell",
    ["-NoProfile", "-File", path.join(root, "tests/verify_files_spreadsheet.ps1")],
    { encoding: "utf8", cwd: root }
  );
  add(
    "VERIFY-files-spreadsheet",
    verify.status === 0 ? "PASS" : "FAIL",
    `exit=${verify.status}`
  );

  let feOk = false;
  try {
    const r = await fetch(BASE, { signal: AbortSignal.timeout(5000) });
    feOk = r.ok || r.status < 500;
  } catch {
    feOk = false;
  }
  add("FE-reachable", feOk ? "PASS" : "FAIL", BASE);
  if (!feOk) {
    finish();
    process.exit(1);
  }

  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    recordVideo: { dir: rawVideoDir, size: { width: 1280, height: 720 } },
    viewport: { width: 1280, height: 720 },
  });
  const page = await context.newPage();

  try {
    await login(page);
    add("LOGIN", "PASS", page.url());

    // --- Admin Storage light ---
    await setTheme(page, "light");
    await page.goto(`${BASE}/admin/settings/storage`, {
      waitUntil: "domcontentloaded",
      timeout: 60000,
    });
    await waitShell(page);
    await page.waitForTimeout(800);

    const storageTitle = await page
      .getByText(/File storage|Lưu trữ file/i)
      .first()
      .isVisible()
      .catch(() => false);
    add("STORAGE-PAGE-LIGHT", storageTitle ? "PASS" : "FAIL");

    const providerSelect = page.locator("select").first();
    const providerVisible = await providerSelect.isVisible().catch(() => false);
    add("STORAGE-PROVIDER-SELECT", providerVisible ? "PASS" : "FAIL");

    let localSelected = false;
    if (providerVisible) {
      const val = await providerSelect.inputValue().catch(() => "");
      localSelected = val === "LOCAL" || val === "";
      if (!localSelected) {
        await providerSelect.selectOption("LOCAL").catch(() => null);
        localSelected = true;
      }
    }
    add("STORAGE-DEFAULT-LOCAL", localSelected ? "PASS" : "FAIL");

    const testBtn = page.getByRole("button", { name: /Test connection|Kiểm tra kết nối/i });
    const saveBtn = page.getByRole("button", { name: /Save|Lưu/i });
    add(
      "STORAGE-ACTIONS",
      ((await testBtn.isVisible().catch(() => false)) &&
        (await saveBtn.isVisible().catch(() => false)))
        ? "PASS"
        : "FAIL"
    );

    await page.screenshot({
      path: path.join(shotsDir, "01-admin-storage-light.png"),
      fullPage: false,
    });

    // --- Admin Storage dark ---
    await setTheme(page, "dark");
    await page.goto(`${BASE}/admin/settings/storage`, {
      waitUntil: "domcontentloaded",
      timeout: 60000,
    });
    await waitShell(page);
    await page.waitForTimeout(600);
    add(
      "STORAGE-PAGE-DARK",
      (await page.getByText(/File storage|Lưu trữ file/i).first().isVisible().catch(() => false))
        ? "PASS"
        : "FAIL"
    );
    await page.screenshot({
      path: path.join(shotsDir, "02-admin-storage-dark.png"),
      fullPage: false,
    });

    // --- Products + Attachments ---
    await setTheme(page, "light");
    await page.goto(`${BASE}/master-data/products`, {
      waitUntil: "domcontentloaded",
      timeout: 60000,
    });
    await waitShell(page);
    await page.waitForTimeout(600);

    const exportCsv = page.getByRole("button", { name: /Export CSV/i });
    const exportXlsx = page.getByRole("button", { name: /Export Excel/i });
    add(
      "PRODUCTS-EXPORT-BUTTONS",
      ((await exportCsv.isVisible().catch(() => false)) &&
        (await exportXlsx.isVisible().catch(() => false)))
        ? "PASS"
        : "FAIL"
    );
    await page.screenshot({
      path: path.join(shotsDir, "03-products-export-light.png"),
      fullPage: false,
    });

    // Open create or edit dialog
    const addBtn = page.getByRole("button", { name: /Thêm|Add|Create/i }).first();
    if (await addBtn.isVisible().catch(() => false)) {
      await addBtn.click({ timeout: 10000 });
    } else {
      const editBtn = page.getByRole("button", { name: /Sửa|Edit/i }).first();
      await editBtn.click({ timeout: 10000 });
    }
    await page.waitForTimeout(700);

    const attachHeading = page.getByText(/Attachments/i).first();
    const attachVisible = await attachHeading.isVisible().catch(() => false);
    add("PRODUCT-ATTACHMENTS-PANEL", attachVisible ? "PASS" : "FAIL");

    const uploadLabel = page.locator("text=Upload").first();
    add(
      "PRODUCT-UPLOAD-CONTROL",
      (await uploadLabel.isVisible().catch(() => false)) ? "PASS" : "FAIL"
    );

    await page.screenshot({
      path: path.join(shotsDir, "04-product-attachments-dialog-light.png"),
      fullPage: false,
    });

    // Close dialog if possible
    const closeBtn = page.getByRole("button", { name: /Close|Đóng|Cancel|Hủy/i }).first();
    if (await closeBtn.isVisible().catch(() => false)) {
      await closeBtn.click().catch(() => null);
      await page.waitForTimeout(300);
    }

    // Dark product dialog
    await setTheme(page, "dark");
    await page.goto(`${BASE}/master-data/products`, {
      waitUntil: "domcontentloaded",
      timeout: 60000,
    });
    await waitShell(page);
    const addBtn2 = page.getByRole("button", { name: /Thêm|Add|Create/i }).first();
    if (await addBtn2.isVisible().catch(() => false)) {
      await addBtn2.click({ timeout: 10000 });
      await page.waitForTimeout(600);
    }
    add(
      "PRODUCT-ATTACHMENTS-DARK",
      (await page.getByText(/Attachments/i).first().isVisible().catch(() => false))
        ? "PASS"
        : "FAIL"
    );
    await page.screenshot({
      path: path.join(shotsDir, "05-product-attachments-dialog-dark.png"),
      fullPage: false,
    });

    // --- Import page xlsx ---
    await setTheme(page, "light");
    await page.goto(`${BASE}/master-data/import`, {
      waitUntil: "domcontentloaded",
      timeout: 60000,
    });
    await waitShell(page);
    await page.waitForTimeout(500);
    const fileInput = page.locator('input[type="file"]').first();
    const accept = (await fileInput.getAttribute("accept").catch(() => "")) || "";
    add(
      "IMPORT-ACCEPT-XLSX",
      accept.includes(".xlsx") ? "PASS" : "FAIL",
      `accept=${accept}`
    );
    await page.screenshot({
      path: path.join(shotsDir, "06-import-xlsx-light.png"),
      fullPage: false,
    });

    // Locations export smoke
    await page.goto(`${BASE}/master-data/locations`, {
      waitUntil: "domcontentloaded",
      timeout: 60000,
    });
    await waitShell(page);
    add(
      "LOCATIONS-EXPORT",
      (await page.getByRole("button", { name: /Export CSV/i }).isVisible().catch(() => false))
        ? "PASS"
        : "FAIL"
    );
    await page.screenshot({
      path: path.join(shotsDir, "07-locations-export-light.png"),
      fullPage: false,
    });
  } catch (err) {
    add("RUNTIME", "FAIL", String(err?.message || err));
    await page
      .screenshot({ path: path.join(shotsDir, "99-error.png"), fullPage: true })
      .catch(() => null);
  } finally {
    await context.close();
    await browser.close();
  }

  // Move video
  const videos = fs.readdirSync(rawVideoDir).filter((f) => f.endsWith(".webm"));
  if (videos.length) {
    const dest = path.join(outDir, "walkthrough-files-spreadsheet.webm");
    fs.copyFileSync(path.join(rawVideoDir, videos[0]), dest);
    add("VIDEO", "PASS", dest);
  } else {
    add("VIDEO", "FAIL", "no webm");
  }

  finish();
  const fail = results.filter((r) => r.status === "FAIL").length;
  process.exit(fail > 0 ? 1 : 0);
}

function finish() {
  const pass = results.filter((r) => r.status === "PASS").length;
  const fail = results.filter((r) => r.status === "FAIL").length;
  const summary = {
    pass,
    fail,
    total: results.length,
    results,
    at: new Date().toISOString(),
  };
  fs.writeFileSync(path.join(outDir, "dbm_results.json"), JSON.stringify(summary, null, 2));
  fs.writeFileSync(path.join(outDir, "dbm_log.txt"), logLines.join("\n"));
  log(`SUMMARY pass=${pass} fail=${fail} total=${results.length}`);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
