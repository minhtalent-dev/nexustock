const fs = require('fs');
const path = require('path');

const modules = [
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

const messagesRoot = process.argv[2];
const locale = process.argv[3];
const dir = path.join(messagesRoot, locale);

function deepMerge(t, s) {
  for (const [k, v] of Object.entries(s)) {
    if (
      v &&
      typeof v === 'object' &&
      !Array.isArray(v) &&
      t[k] &&
      typeof t[k] === 'object' &&
      !Array.isArray(t[k])
    ) {
      deepMerge(t[k], v);
    } else {
      t[k] = v;
    }
  }
  return t;
}

function flat(o, p = '') {
  let k = [];
  for (const [a, b] of Object.entries(o)) {
    const n = p ? `${p}.${a}` : a;
    if (b && typeof b === 'object' && !Array.isArray(b)) k = k.concat(flat(b, n));
    else k.push(n);
  }
  return k;
}

let acc = {};
for (const m of modules) {
  const p = path.join(dir, `${m}.json`);
  if (!fs.existsSync(p)) {
    console.log(JSON.stringify({ ok: false, err: 'missing ' + p }));
    process.exit(1);
  }
  const part = JSON.parse(fs.readFileSync(p, 'utf8'));
  const roots = Object.keys(part);
  if (roots.length !== 1 || roots[0] !== m) {
    console.log(JSON.stringify({ ok: false, err: 'bad root ' + p }));
    process.exit(1);
  }
  deepMerge(acc, part);
}

const keys = flat(acc).sort();
const kebab = keys.filter((k) => k.split('.').some((s) => s.includes('-')));
const codes = [
  'CUTOVER_FROZEN',
  'READINESS_DISABLED',
  'READINESS_UNAUTHORIZED',
  'CUTOVER_FREEZE_DENIED',
  'TASK_INTERLEAVING_DISABLED',
  'UNAUTHORIZED',
  'FORBIDDEN',
  'UNKNOWN',
];
const missing = codes.filter((c) => !(acc.Errors && acc.Errors.codes && acc.Errors.codes[c]));
const generic = !!(acc.Errors && acc.Errors.messages && acc.Errors.messages.generic);
console.log(
  JSON.stringify({
    ok: true,
    count: keys.length,
    kebabCount: kebab.length,
    missing,
    generic,
    keys,
  })
);
