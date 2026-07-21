/**
 * One-shot: tách monolith messages/{vi|en}.json → messages/{locale}/{Namespace}.json
 * Hygiene Breadcrumb kebab → camelCase. Backup local, xóa monolith.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const CATALOG_MODULES = [
  'Common',
  'Language',
  'Sidebar',
  'Breadcrumb',
  'Errors',
  'Home',
  'Login',
  'HealthUi',
  'Admin',
  'Features',
];

const HYGIENE = {
  'master-data': 'masterData',
  'health-ui': 'healthUi',
  'put-wall': 'putWall',
  'cross-docking': 'crossDocking',
  'local-agent': 'localAgent',
  'task-interleaving': 'taskInterleaving',
};

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const frontendRoot = path.join(scriptDir, '..');
const repoRoot = path.join(frontendRoot, '..');
const messagesDir = path.join(frontendRoot, 'messages');
const evidenceDir = path.join(repoRoot, 'planning', 'evidence', 'phase_31a');

function flat(o, p = '') {
  let k = [];
  for (const [a, b] of Object.entries(o)) {
    const n = p ? `${p}.${a}` : a;
    if (b && typeof b === 'object' && !Array.isArray(b)) k = k.concat(flat(b, n));
    else k.push(n);
  }
  return k;
}

function applyHygieneBreadcrumb(obj) {
  if (!obj.Breadcrumb || typeof obj.Breadcrumb !== 'object') return;
  const next = {};
  for (const [k, v] of Object.entries(obj.Breadcrumb)) {
    next[HYGIENE[k] ?? k] = v;
  }
  obj.Breadcrumb = next;
}

function applyRenameToKeys(keys) {
  const map = {
    'Breadcrumb.master-data': 'Breadcrumb.masterData',
    'Breadcrumb.health-ui': 'Breadcrumb.healthUi',
    'Breadcrumb.put-wall': 'Breadcrumb.putWall',
    'Breadcrumb.cross-docking': 'Breadcrumb.crossDocking',
    'Breadcrumb.local-agent': 'Breadcrumb.localAgent',
    'Breadcrumb.task-interleaving': 'Breadcrumb.taskInterleaving',
  };
  return keys.map((k) => map[k] ?? k);
}

function assertSetEq(a, b, label) {
  const sa = new Set(a);
  const sb = new Set(b);
  const onlyA = [...sa].filter((x) => !sb.has(x));
  const onlyB = [...sb].filter((x) => !sa.has(x));
  if (onlyA.length || onlyB.length) {
    throw new Error(`${label} mismatch onlyA=${onlyA} onlyB=${onlyB}`);
  }
}

const viPath = path.join(messagesDir, 'vi.json');
const enPath = path.join(messagesDir, 'en.json');

if (!fs.existsSync(viPath) || !fs.existsSync(enPath)) {
  console.error('Idempotent guard: monolith messages/vi.json hoặc en.json không tồn tại (đã migrate?).');
  process.exit(1);
}

const vi = JSON.parse(fs.readFileSync(viPath, 'utf8'));
const en = JSON.parse(fs.readFileSync(enPath, 'utf8'));

assertSetEq(Object.keys(vi), CATALOG_MODULES, 'vi roots');
assertSetEq(Object.keys(en), CATALOG_MODULES, 'en roots');

fs.mkdirSync(evidenceDir, { recursive: true });
const keysBefore = flat(vi).sort();
fs.writeFileSync(path.join(evidenceDir, 'keys_before.txt'), keysBefore.join('\n') + '\n', 'utf8');

applyHygieneBreadcrumb(vi);
applyHygieneBreadcrumb(en);

for (const locale of ['vi', 'en']) {
  const data = locale === 'vi' ? vi : en;
  const dir = path.join(messagesDir, locale);
  fs.mkdirSync(dir, { recursive: true });
  for (const K of CATALOG_MODULES) {
    const payload = { [K]: data[K] };
    const roots = Object.keys(payload);
    if (roots.length !== 1 || roots[0] !== K) {
      throw new Error(`Invalid shape for ${locale}/${K}.json`);
    }
    const out = path.join(dir, `${K}.json`);
    fs.writeFileSync(out, JSON.stringify(payload, null, 2) + '\n', 'utf8');
  }
}

const backupDir = path.join(messagesDir, '_backup');
fs.mkdirSync(backupDir, { recursive: true });
fs.copyFileSync(viPath, path.join(backupDir, 'vi.json.bak'));
fs.copyFileSync(enPath, path.join(backupDir, 'en.json.bak'));
fs.unlinkSync(viPath);
fs.unlinkSync(enPath);

function loadMerged(locale) {
  const acc = {};
  for (const K of CATALOG_MODULES) {
    const part = JSON.parse(
      fs.readFileSync(path.join(messagesDir, locale, `${K}.json`), 'utf8')
    );
    deepMergeLocal(acc, part);
  }
  return acc;
}

function deepMergeLocal(target, source) {
  for (const [k, v] of Object.entries(source)) {
    if (
      v &&
      typeof v === 'object' &&
      !Array.isArray(v) &&
      target[k] &&
      typeof target[k] === 'object' &&
      !Array.isArray(target[k])
    ) {
      deepMergeLocal(target[k], v);
    } else {
      target[k] = v;
    }
  }
  return target;
}

const mergedVi = loadMerged('vi');
const mergedEn = loadMerged('en');
const keysAfter = flat(mergedVi).sort();
fs.writeFileSync(path.join(evidenceDir, 'keys_after.txt'), keysAfter.join('\n') + '\n', 'utf8');

const expected = applyRenameToKeys(keysBefore).sort();
assertSetEq(keysAfter, expected, 'keys_after vs applyRename(keys_before)');
assertSetEq(flat(mergedEn).sort(), keysAfter, 'vi/en parity');

const kebab = keysAfter.filter((k) => k.split('.').some((s) => s.includes('-')));
if (kebab.length) throw new Error(`Kebab segments remain: ${kebab.join(', ')}`);

console.log(
  JSON.stringify({
    ok: true,
    modules: CATALOG_MODULES.length,
    files: CATALOG_MODULES.length * 2,
    keys: keysAfter.length,
  })
);
