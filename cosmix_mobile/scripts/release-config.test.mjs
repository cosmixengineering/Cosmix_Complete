import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  validateReleaseConfig,
  networkSecurityXml,
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
