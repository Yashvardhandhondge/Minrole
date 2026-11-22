#!/usr/bin/env node
/* =====================================================================
   SecureShift - hash_passwords.js   (Node 18+, no npm packages needed)

   Makes and checks password hashes in the ASP.NET Core Identity V3 format,
   the same format that PasswordHasher<T> in .NET 8 creates and verifies:

     byte 0       0x01            format marker (V3)
     bytes 1-4    2               PRF = HMACSHA512   (uint32, big-endian)
     bytes 5-8    100000          iteration count    (uint32, big-endian)
     bytes 9-12   16              salt length        (uint32, big-endian)
     bytes 13-28  salt            16 random bytes
     bytes 29-60  subkey          32 bytes = PBKDF2(password as UTF-8, salt)
   The 61 bytes are stored as base64 (84 characters) in dev."Users"."PasswordHash".

   Usage:
     node hash_passwords.js hash Welcome@2026 [more passwords...]
         -> one new hash per password (new random salt every time)
     node hash_passwords.js verify "<hash>" "<password>"
         -> OK / FAIL (also accepts the old uppercase SHA-256 hex hashes)
     node hash_passwords.js check-seed [path/to/02_seed_data_postgresql_dev.sql]
         -> checks every user in the seed file has a V3 hash of the expected password
   ===================================================================== */
'use strict';
const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

const V3_MARKER = 0x01;
const PRF_NAMES = { 0: 'sha1', 1: 'sha256', 2: 'sha512' };   // KeyDerivationPrf enum in .NET
const PRF_HMACSHA512 = 2;
const ITERATIONS = 100000;
const SALT_BYTES = 16;
const SUBKEY_BYTES = 32;

// The 5 named accounts. Every other seeded user has Welcome@2026.
const SEED_PASSWORDS = {
  'aarav.mehta@secureshift.io': 'Cloud@2026',
  'arya.mujumdar@secureshift.io': 'Super@2026',
  'sachin.kumar@secureshift.io': 'Admin@2026',
  'amanjeet.sharma@secureshift.io': 'Architect@2026',
  'shivanand.kumatekar@secureshift.io': 'Exec@2026',
};
const DEFAULT_PASSWORD = 'Welcome@2026';

function hashPassword(password, salt) {
  salt = salt || crypto.randomBytes(SALT_BYTES);
  if (salt.length !== SALT_BYTES) throw new Error('salt must be ' + SALT_BYTES + ' bytes');
  const subkey = crypto.pbkdf2Sync(Buffer.from(password, 'utf8'), salt, ITERATIONS, SUBKEY_BYTES, 'sha512');
  const out = Buffer.alloc(13 + SALT_BYTES + SUBKEY_BYTES);
  out[0] = V3_MARKER;
  out.writeUInt32BE(PRF_HMACSHA512, 1);
  out.writeUInt32BE(ITERATIONS, 5);
  out.writeUInt32BE(SALT_BYTES, 9);
  salt.copy(out, 13);
  subkey.copy(out, 13 + SALT_BYTES);
  return out.toString('base64');
}

function legacySha256Hex(password) {
  return crypto.createHash('sha256').update(password, 'utf8').digest('hex').toUpperCase();
}

function verifyPassword(hash, password) {
  if (/^[0-9A-F]{64}$/.test(hash)) return hash === legacySha256Hex(password);   // old SQL Server seed format
  let b;
  try { b = Buffer.from(hash, 'base64'); } catch (e) { return false; }
  if (b.length < 13 || b[0] !== V3_MARKER) return false;
  const algo = PRF_NAMES[b.readUInt32BE(1)];
  const iterations = b.readUInt32BE(5);
  const saltLen = b.readUInt32BE(9);
  if (!algo || saltLen < 16 || b.length < 13 + saltLen + 16) return false;
  const salt = b.subarray(13, 13 + saltLen);
  const expected = b.subarray(13 + saltLen);
  const actual = crypto.pbkdf2Sync(Buffer.from(password, 'utf8'), salt, iterations, expected.length, algo);
  return crypto.timingSafeEqual(actual, expected);
}

function expectedPasswordFor(email) {
  return SEED_PASSWORDS[email.toLowerCase()] || DEFAULT_PASSWORD;
}

function checkSeed(file) {
  const sql = fs.readFileSync(file, 'utf8');
  // Users rows look like: (1, 'Arya Mujumdar', 'arya.mujumdar@...', '98...', '<hash>', 2, ...
  const rowRe = /^\s*\((\d+), '[^']*', '([^']+@[^']+)', (?:'[^']*'|NULL), '([^']+)', \d+,/gm;
  let m, total = 0, bad = 0;
  while ((m = rowRe.exec(sql)) !== null) {
    total++;
    const [, id, email, hash] = m;
    const pw = expectedPasswordFor(email);
    const isV3 = Buffer.from(hash, 'base64')[0] === V3_MARKER && hash.length === 84;
    const ok = isV3 && verifyPassword(hash, pw);
    if (!ok) bad++;
    console.log(`${ok ? 'OK  ' : 'FAIL'}  user ${id.padStart(2)}  ${email.padEnd(36)} ${pw}${isV3 ? '' : '  (not a V3 hash)'}`);
  }
  console.log(`${total} users checked, ${bad} failed.`);
  if (total === 0) { console.error('No user rows found in ' + file); return 1; }
  return bad === 0 ? 0 : 1;
}

function main(argv) {
  const [cmd, ...rest] = argv;
  if (cmd === 'hash' && rest.length > 0) {
    for (const pw of rest) console.log(`${pw}\t${hashPassword(pw)}`);
    return 0;
  }
  if (cmd === 'verify' && rest.length === 2) {
    const ok = verifyPassword(rest[0], rest[1]);
    console.log(ok ? 'OK' : 'FAIL');
    return ok ? 0 : 1;
  }
  if (cmd === 'check-seed') {
    const file = rest[0] || path.join(__dirname, '..', '02_seed_data_postgresql_dev.sql');
    return checkSeed(file);
  }
  console.log('Usage:\n  node hash_passwords.js hash <password> [...]\n  node hash_passwords.js verify "<hash>" "<password>"\n  node hash_passwords.js check-seed [seed.sql]');
  return 2;
}

module.exports = { hashPassword, verifyPassword, legacySha256Hex, expectedPasswordFor, SEED_PASSWORDS, DEFAULT_PASSWORD };

if (require.main === module) process.exit(main(process.argv.slice(2)));
