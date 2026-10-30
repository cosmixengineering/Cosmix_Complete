import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, mkdtempSync, existsSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { X509Certificate } from 'node:crypto';
import {
  validateReleaseConfig,
  networkSecurityXml,
  writeNetworkResources,
} from './release-config.mjs';

const valid = () => ({
  apiBaseUrl: 'https://payroll.example.com',
  certificatePins: [
    Buffer.alloc(32, 1).toString('base64'),
    Buffer.alloc(32, 2).toString('base64'),
  ],
  signingCertificateSha256: 'AB'.repeat(32),
  versionCode: 1,
  versionName: '1.0.0',
});
const certificatePem = readFileSync(
  new URL('./fixtures/synthetic-release-ca.pem', import.meta.url),
  'utf8',
);
const privateLan = () => ({
  ...valid(),
  apiBaseUrl: 'https://192.168.1.20',
  networkTrust: 'private-ca',
  privateTrustAnchor: {
    certificatePem,
    sha256: new X509Certificate(certificatePem).fingerprint256.replaceAll(
      ':',
      '',
    ),
  },
});
test('approved LAN origin gets only its fingerprint-checked CA and mandatory pins', () => {
  const config = validateReleaseConfig(privateLan());
  const xml = networkSecurityXml(config);
  assert.match(xml, /includeSubdomains="false">192\.168\.1\.20/);
  assert.match(xml, /src="@raw\/cosmix_release_ca" overridePins="false"/);
  assert.equal((xml.match(/<pin digest=/g) ?? []).length, 2);
  assert.doesNotMatch(
    xml,
    /src="user"|debug-overrides|expiration=|Permitted="true"/,
  );
});
test('resource regeneration removes stale private CA when returning to system trust', () => {
  const directory = mkdtempSync(join(tmpdir(), 'cosmix-release-policy-'));
  try {
    writeNetworkResources(validateReleaseConfig(privateLan()), directory);
    const caPath = join(directory, 'raw/cosmix_release_ca.pem');
    assert.equal(
      new X509Certificate(readFileSync(caPath)).fingerprint256,
      new X509Certificate(certificatePem).fingerprint256,
    );
    writeNetworkResources(validateReleaseConfig(valid()), directory);
    assert.equal(existsSync(caPath), false);
    assert.doesNotMatch(
      readFileSync(join(directory, 'xml/network_security_config.xml'), 'utf8'),
      /@raw\/cosmix_release_ca/,
    );
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});
test('private trust requires an explicit LAN origin, matching CA and validity', () => {
  const original = privateLan();
  for (const patch of [
    { networkTrust: 'anything' },
    { networkTrust: 'system' },
    { privateTrustAnchor: undefined },
    { apiBaseUrl: 'https://8.8.8.8' },
    { apiBaseUrl: 'https://127.0.0.1' },
    { apiBaseUrl: 'https://169.254.1.1' },
    { apiBaseUrl: 'https://[::1]' },
    {
      privateTrustAnchor: {
        ...original.privateTrustAnchor,
        sha256: 'AB'.repeat(32),
      },
    },
    {
      privateTrustAnchor: {
        ...original.privateTrustAnchor,
        certificatePem: certificatePem + certificatePem,
      },
    },
    {
      privateTrustAnchor: {
        ...original.privateTrustAnchor,
        certificatePem: certificatePem + '\n-----BEGIN PRIVATE KEY-----',
      },
    },
  ]) {
    assert.throws(() => validateReleaseConfig({ ...original, ...patch }));
  }
  assert.throws(() => validateReleaseConfig(original, new Date('2041-01-01')));
  assert.throws(() => validateReleaseConfig(original, new Date('2024-01-01')));
});
test('a valid leaf certificate cannot be used as a private CA', () => {
  const leaf = readFileSync(
    new URL('./fixtures/synthetic-release-leaf.pem', import.meta.url),
    'utf8',
  );
  assert.throws(
    () =>
      validateReleaseConfig({
        ...privateLan(),
        privateTrustAnchor: {
          certificatePem: leaf,
          sha256: new X509Certificate(leaf).fingerprint256.replaceAll(':', ''),
        },
      }),
    /Private CA must be valid/,
  );
});
test('current and backup pins bind exactly the approved origin without an expiry or debug trust', () => {
  const config = validateReleaseConfig(valid());
  const xml = networkSecurityXml(config);
  assert.match(xml, /includeSubdomains="false">payroll.example.com/);
  assert.equal((xml.match(/<pin digest=/g) ?? []).length, 2);
  assert.doesNotMatch(
    xml,
    /expiration=|src="user"|debug-overrides|cleartextTrafficPermitted="true"/,
  );
});
test('local, cleartext, credential-bearing and non-origin URLs are rejected', () => {
  for (const apiBaseUrl of [
    'http://payroll.example.com',
    'https://10.0.2.2',
    'https://localhost',
    'https://company.local',
    'https://company.invalid',
    'https://user:secret@payroll.example.com',
    'https://payroll.example.com/api',
    'https://payroll.example.com:8443',
    'https://payroll.example.com?x=1',
  ]) {
    assert.throws(() => validateReleaseConfig({ ...valid(), apiBaseUrl }));
  }
});
test('one, duplicate, malformed, empty and noncanonical pins cannot create a release', () => {
  const pin = valid().certificatePins[0];
  for (const certificatePins of [
    [],
    [pin],
    [pin, pin],
    [pin, 'bad'],
    [pin, Buffer.alloc(32).toString('base64')],
    [pin, 'A'.repeat(42) + 'B='],
  ]) {
    assert.throws(() => validateReleaseConfig({ ...valid(), certificatePins }));
  }
});
test('signing identity and version must be explicitly configured', () => {
  for (const patch of [
    { signingCertificateSha256: '' },
    { signingCertificateSha256: '0'.repeat(64) },
    { versionCode: 0 },
    { versionCode: 1.1 },
    { versionName: 'latest' },
  ]) {
    assert.throws(() => validateReleaseConfig({ ...valid(), ...patch }));
  }
});
