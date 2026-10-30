import { readFileSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { X509Certificate } from 'node:crypto';
import { isIP } from 'node:net';
import { resolve, dirname } from 'node:path';
import { pathToFileURL } from 'node:url';

function privateIpv4(host) {
  if (isIP(host) !== 4) return false;
  const [first, second] = host.split('.').map(Number);
  return (
    first === 10 ||
    (first === 172 && second >= 16 && second <= 31) ||
    (first === 192 && second === 168)
  );
}

function validateTrustAnchor(value, now) {
  if (
    !value ||
    typeof value.certificatePem !== 'string' ||
    !/^-----BEGIN CERTIFICATE-----\r?\n[A-Za-z0-9+/=\r\n]+\r?\n-----END CERTIFICATE-----\s*$/.test(
      value.certificatePem,
    ) ||
    typeof value.sha256 !== 'string' ||
    !/^[a-fA-F0-9]{64}$/.test(value.sha256)
  ) {
    throw new Error(
      'Private trust requires exactly one public PEM CA and its approved SHA-256 certificate fingerprint.',
    );
  }
  const certificate = new X509Certificate(value.certificatePem);
  const fingerprint = certificate.fingerprint256.replaceAll(':', '');
  const key = certificate.publicKey;
  const strongKey =
    (['rsa', 'rsa-pss'].includes(key.asymmetricKeyType) &&
      key.asymmetricKeyDetails.modulusLength >= 2048) ||
    (key.asymmetricKeyType === 'ec' &&
      ['prime256v1', 'secp384r1', 'secp521r1'].includes(
        key.asymmetricKeyDetails.namedCurve,
      ));
  if (
    !certificate.ca ||
    !strongKey ||
    fingerprint !== value.sha256.toUpperCase() ||
    now < new Date(certificate.validFrom) ||
    now >= new Date(certificate.validTo)
  ) {
    throw new Error(
      'Private CA must be valid, use a supported strong key and match the approved certificate fingerprint.',
    );
  }
  return { certificatePem: certificate.toString(), sha256: fingerprint };
}

export function validateReleaseConfig(value, now = new Date()) {
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
  const networkTrust = value.networkTrust ?? 'system';
  if (!['system', 'private-ca'].includes(networkTrust)) {
    throw new Error('Release network trust must be system or private-ca.');
  }
  const dnsHost =
    /^(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$/.test(host);
  if (
    url.protocol !== 'https:' ||
    url.username ||
    url.password ||
    url.port ||
    url.search ||
    url.hash ||
    url.pathname !== '/' ||
    !(dnsHost || (networkTrust === 'private-ca' && privateIpv4(host))) ||
    /(?:^|\.)(?:localhost|local|invalid|test)$/.test(host)
  ) {
    throw new Error(
      'Release requires an HTTPS origin on port 443 without credentials or a path; a private IPv4 origin requires explicit private-ca trust.',
    );
  }
  if (networkTrust === 'system' && value.privateTrustAnchor !== undefined) {
    throw new Error('Private trust anchors require explicit private-ca trust.');
  }
  const privateTrustAnchor =
    networkTrust === 'private-ca'
      ? validateTrustAnchor(value.privateTrustAnchor, now)
      : undefined;
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
    networkTrust,
    privateTrustAnchor,
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
${
  config.networkTrust === 'private-ca'
    ? '    <trust-anchors><certificates src="@raw/cosmix_release_ca" overridePins="false" /></trust-anchors>\n'
    : ''
}    <pin-set>
${config.certificatePins
  .map(pin => `      <pin digest="SHA-256">${pin}</pin>`)
  .join('\n')}
    </pin-set>
  </domain-config>
</network-security-config>
`;
}

export function writeNetworkResources(config, outputDirectory) {
  const output = resolve(outputDirectory, 'xml/network_security_config.xml');
  const caOutput = resolve(outputDirectory, 'raw/cosmix_release_ca.pem');
  mkdirSync(dirname(output), { recursive: true });
  if (config.networkTrust === 'private-ca') {
    mkdirSync(dirname(caOutput), { recursive: true });
    writeFileSync(caOutput, config.privateTrustAnchor.certificatePem, 'utf8');
  } else {
    // Remove only our generated public CA when switching back to system trust.
    rmSync(caOutput, { force: true });
  }
  writeFileSync(output, networkSecurityXml(config), 'utf8');
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
      writeNetworkResources(config, process.argv[3]);
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
