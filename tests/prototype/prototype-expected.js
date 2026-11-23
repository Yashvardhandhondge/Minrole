// Runs the REAL prototype JavaScript (js/common/core.js, js/data/*.js) in Node and writes the numbers it
// calculates to tests/SecureShift.Tests/Data/prototype-expected.json. The C# tests compare against this file,
// so the console formulas are proven to give exactly the prototype's results.
//
//   node tests/prototype/prototype-expected.js           write the file
//   node tests/prototype/prototype-expected.js --check   fail if the committed file is out of date (CI)
"use strict";
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const ROOT = path.resolve(__dirname, "..", "..");
const OUT = path.join(ROOT, "tests", "SecureShift.Tests", "Data", "prototype-expected.json");

// a tiny browser: window = the context itself, no localStorage (SS.store falls back to memory)
const ctx = { console, Math, JSON, Date, Number, String, Object, Array, Infinity, parseInt };
ctx.window = ctx;
ctx.document = { body: null, querySelector: () => null, querySelectorAll: () => [] };
vm.createContext(ctx);
for (const f of ["js/common/core.js", "js/data/users.js", "js/data/governance-data.js", "js/data/emdt-data.js", "js/data/emdt-calc.js"])
  vm.runInContext(fs.readFileSync(path.join(ROOT, f), "utf8"), ctx, { filename: f });
const SS = ctx.SS, C = SS.EMC;

let seed = 20261122;
function rnd() { seed = (seed * 9301 + 49297) % 233280; return seed / 233280; }
const pick = list => list[Math.floor(rnd() * list.length)];
const int = (min, max) => min + Math.floor(rnd() * (max - min + 1));
const plain = x => JSON.parse(JSON.stringify(x));

/* ---------- Governance (governance-data.js) ---------- */
const gov = {
  // SS.GOV.getDetail ratio: Math.round((1 - used / granted) * 100), for the 128 prototype identities
  identities: SS.GOV.identities.map(i => ({
    id: i.id, granted: i.granted, used: i.used, score: i.score,
    ratio: Math.round((1 - i.used / i.granted) * 100), riskOf: SS.GOV.riskOf(i.score),
  })),
  riskOf: Array.from({ length: 101 }, (_, s) => SS.GOV.riskOf(s)),
  riskBand: [0, 39.99, 40, 54, 59.99, 60, 62, 71, 100].map(a => ({ avg: a, band: SS.GOV.riskBand(a) })),
};

/* ---------- EMDT (emdt-calc.js) ---------- */
const TECHS = Object.keys(C.TECH_VALUES), CLOUDS = Object.keys(C.CLOUD_TYPES);
function randomClassification() {
  const total = int(1, 2000);
  const low = int(0, total), med = int(0, total - low), high = int(0, total - low - med), crit = total - low - med - high;
  const techs = TECHS.filter(() => rnd() < 0.5);
  if (!techs.length) techs.push(pick(TECHS));
  const deps = pick([0, int(1, 25), int(26, 100), int(101, 300), int(301, 5000)]);
  return {
    totalApps: total, deps, apis: pick([0, int(1, 10), int(11, 50), int(51, 150), int(151, 2000)]),
    dbs: pick([1, 5, 6, 15, 16, 30, 31, int(1, 500)]), storageGB: pick([1, 99, 100, 1024, 1025, 5120, 5121, 20480, 20481, int(1, 100000)]),
    envs: int(1, 20), units: pick([1, 3, 4, 6, 7, 10, 11, int(1, 500)]), techs,
    users: pick([1, 50, 51, 200, 201, 500, 501, 1000, 1001, int(1, 100000)]),
    crit: { low, medium: med, high, critical: crit }, policies: C.POLICIES.filter(() => rnd() < 0.45),
    cloud: pick(CLOUDS), loc: pick([1000, 99999, 100000, 500000, 500001, 2000000, 2000001, 10000000, 10000001, int(1000, 50000000)]),
  };
}
const emsiCase = cls => { const r = C.emsi(cls); return { input: plain(cls), score: r.score, category: r.category, multiplier: r.multiplier, rows: r.rows.map(x => ({ label: x.label, score: x.score, weight: x.weight, weighted: x.weighted })) }; };

function randomApp(i) {
  return {
    id: "APP-9" + String(i).padStart(2, "0"), ent: "ENT-999", name: "Random App " + i, age: int(0, 50),
    framework: pick(["Java 8", ".NET 6", "PHP 7.2", "COBOL 85"]), support: pick(["Supported", "Extended", "Unsupported"]),
    docQuality: pick(["Good", "Average", "Poor"]), complexity: pick(["Low", "Medium", "High"]), loc: pick([100, 99999, 100000, 500000, 500001, 2000000, 2000001, int(100, 9000000)]),
    criticality: pick(["Low", "Medium", "High", "Critical"]), sensitive: pick(["Yes", "No"]), securityIssues: pick([0, 1, 2, 3, 5, 6, int(0, 999)]),
    downtime: pick(["Low", "Medium", "High"]), compliance: pick([0, 1, 2, 3, int(0, 10)]),
  };
}
const assessCase = app => { const a = C.assess(app); return { app: plain(app), td: a.td, gr: a.gr, tdFactors: a.tdFactors, grFactors: a.grFactors }; };

const apps = SS.EM.apps;
const emdt = {
  level: Array.from({ length: 101 }, (_, s) => C.level(s)),
  categoryOf: [0, 24.99, 25, 44.91, 44.99, 45, 69.99, 70, 77.87, 100].map(s => ({ score: s, category: C.categoryOf(s).name })),
  enterprises: SS.EM.enterprises.filter(e => e.classification).map(e => ({ id: e.id, name: e.name, ...emsiCase(e.classification) })),
  randomEmsi: Array.from({ length: 150 }, randomClassification).map(emsiCase),
  // seed applications: assessment with the seed dependency links + dependency impact
  seedApps: apps.map(a => ({ id: a.id, name: a.name, ...assessCase(a), di: C.depImpact(a.id).score, raw: C.depImpact(a.id).raw, links: C.depImpact(a.id).count, affected: C.affected(a.id) })),
  randomApps: Array.from({ length: 150 }, (_, i) => assessCase(randomApp(i))),
  depImpactRaw: Array.from({ length: 121 }, (_, raw) => ({ raw, score: Math.round(100 * (1 - Math.exp(-raw / C.DI_K))) })),
  // every seed application x every strategy (seed assessments + seed links + seed classification)
  simulations: apps.flatMap(a => Object.keys(C.STRATEGIES).map(st => {
    const r = C.simulate(a, st);
    return { app: a.id, strategy: st, td: r.td, gr: r.gr, di: r.di, sr: r.sr, mr: r.mr, mrLevel: r.mrLevel, emsi: r.emsi, category: r.category, multiplier: r.multiplier,
             baseWeeks: r.baseWeeks, effort: r.effort, team: r.team, personMonths: r.personMonths, labour: r.labour, testing: r.testing, cost: r.cost,
             recommendation: r.recommendation, sequence: r.sequence };
  })),
  phases: Array.from({ length: 120 }, (_, i) => ({ effort: i + 1, weeks: C.phases(i + 1).map(p => p.weeks), names: C.phases(i + 1).map(p => p.name) })),
  lakh: [0, 9.71, 15.5, 22.15, 26.01, 100].map(n => ({ n, text: C.lakh(n) })),
  fmt: [0, 850, 8000, 99999, 100000, 450000, 1200000, 4500000, 12345678, 1000000000].map(n => ({ n, text: SS.fmt(n) })),
};

const text = JSON.stringify({ generatedFrom: "js/common/core.js + js/data/governance-data.js + js/data/emdt-data.js + js/data/emdt-calc.js", gov, emdt }) + "\n";
if (process.argv.includes("--check")) {
  const committed = fs.existsSync(OUT) ? fs.readFileSync(OUT, "utf8").replace(/\r\n/g, "\n") : "";
  if (committed !== text) { console.error("prototype-expected.json is out of date: run node tests/prototype/prototype-expected.js and commit it."); process.exit(1); }
  console.log("prototype-expected.json is up to date.");
} else {
  fs.mkdirSync(path.dirname(OUT), { recursive: true });
  fs.writeFileSync(OUT, text);
  console.log(`wrote ${path.relative(ROOT, OUT)} (${text.length} bytes): ${gov.identities.length} identities, ${emdt.randomEmsi.length} EMSI cases, ` +
              `${emdt.randomApps.length + emdt.seedApps.length} assessments, ${emdt.simulations.length} simulations, ${emdt.phases.length} phase splits`);
}
