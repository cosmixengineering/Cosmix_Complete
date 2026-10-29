import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { pathToFileURL } from 'node:url';

export function validateReleaseConfig(value) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    throw new Error('An approved Android release configuration is required.');
  }
  let url;
  try {
    url = new URL(value.apiBaseUrl);
  } catch {
    throw new Error('Release API URL is invalid.');
  }
  const host = url.hostname;
  if (
    url.protocol !== 'https:' ||
    url.username ||
    url.password ||
    url.port ||
    url.search ||
    url.hash ||
    url.pathname !== '/' ||
    !/^(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$/.test(host) ||
    /(?:^|\.)(?:localhost|local|invalid|test)$/.test(host)
  ) {
    throw new Error(
      'Release requires an HTTPS DNS origin on port 443, without credentials or a path.',
    );
  }
  const pins = value.certificatePins;
  if (
    !Array.isArray(pins) ||
    pins.length < 2 ||
    pins.length > 5 ||
    new Set(pins).size !== pins.length ||
    pins.some(
      pin =>
        typeof pin !== 'string' ||
        !/^[A-Za-z0-9+/]{43}=$/.test(pin) ||
        Buffer.from(pin, 'base64').length !== 32 ||
        Buffer.from(pin, 'base64').toString('base64') !== pin ||
        Buffer.from(pin, 'base64').every(byte => byte === 0),
    )
  ) {
    throw new Error(
      'Provide distinct SHA-256 SPKI pins for the current key and at least one backup key.',
    );
  }
  if (
    typeof value.signingCertificateSha256 !== 'string' ||
    !/^[A-Fa-f0-9]{64}$/.test(value.signingCertificateSha256) ||
    /^0+$/.test(value.signingCertificateSha256)
  ) {
    throw new Error(
      'The approved signing certificate SHA-256 fingerprint is required.',
    );
  }
  if (
    !Number.isSafeInteger(value.versionCode) ||
    value.versionCode < 1 ||
    value.versionCode > 2100000000 ||
    typeof value.versionName !== 'string' ||
    !/^\d+\.\d+\.\d+(?:[-.][A-Za-z0-9]+)*$/.test(value.versionName)
  ) {
    throw new Error(
      'A valid release version code and version name are required.',
    );
  }
  return {
    apiBaseUrl: url.origin,
    host,
    certificatePins: pins,
    signingCertificateSha256: value.signingCertificateSha256.toUpperCase(),
    versionCode: value.versionCode,
    versionName: value.versionName,
  };
}

export function networkSecurityXml(config) {
  // No expiry: an expired pin set must never silently disable pin checking.
  return `<?xml version="1.0" encoding="utf-8"?>
<network-security-config>
  <base-config cleartextTrafficPermitted="false">
    <trust-anchors><certificates src="system" /></trust-anchors>
  </base-config>
  <domain-config cleartextTrafficPermitted="false">
    <domain includeSubdomains="false">${config.host}</domain>
    <pin-set>
${config.certificatePins
  .map(pin => `      <pin digest="SHA-256">${pin}</pin>`)
  .join('\n')}
    </pin-set>
  </domain-config>
</network-security-config>
`;
}

if (
  process.argv[1] &&
  import.meta.url === pathToFileURL(resolve(process.argv[1])).href
) {
  try {
    const configPath = process.env.COSMIX_ANDROID_RELEASE_CONFIG;
    if (!configPath)
      throw new Error(
        'Set COSMIX_ANDROID_RELEASE_CONFIG to the approved private configuration path.',
      );
    const config = validateReleaseConfig(
      JSON.parse(readFileSync(configPath, 'utf8')),
    );
    if (process.argv[2] === '--json')
      process.stdout.write(JSON.stringify(config));
    else if (process.argv[2] === '--resources' && process.argv[3]) {
      const output = resolve(
        process.argv[3],
        'xml/network_security_config.xml',
      );
      mkdirSync(dirname(output), { recursive: true });
      writeFileSync(output, networkSecurityXml(config), 'utf8');
      process.stdout.write('Android release network policy generated.\n');
    } else
      throw new Error(
        'Use --json or --resources <generated-resource-directory>.',
      );
  } catch (error) {
    process.stderr.write(
      `Android release configuration rejected: ${
        error instanceof Error ? error.message : 'invalid configuration'
      }\n`,
    );
    process.exitCode = 1;
  }
}
