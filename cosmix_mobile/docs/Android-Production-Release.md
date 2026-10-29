# Android production release

Debug builds keep the approved emulator origin and local test CA. Production builds take their API origin from a native build constant and use a generated Android network security policy. No production URL, signing identity or certificate pin has been invented for this company.

Provide an approved JSON file outside the checkout and set `COSMIX_ANDROID_RELEASE_CONFIG` to its absolute path. Fields:

- `apiBaseUrl`: public HTTPS DNS origin on port 443, without an API path, credentials, query or fragment.
- `certificatePins`: two to five distinct base64 SHA-256 hashes of certificate **SubjectPublicKeyInfo** values: current and independently retained backup keys. These are not whole-certificate hashes.
- `signingCertificateSha256`: approved upload/signing certificate's SHA-256 fingerprint as 64 hex characters, without colons.
- `versionCode`: positive integer, greater than the previously distributed release.
- `versionName`: semantic version, for example `1.0.0`.

Keep the actual keystore outside the checkout. Supply `COSMIX_ANDROID_KEYSTORE`, `COSMIX_ANDROID_KEY_ALIAS`, `COSMIX_ANDROID_STORE_PASSWORD`, and `COSMIX_ANDROID_KEY_PASSWORD` through the private build environment. Do not put passwords in command arguments, tracked Gradle properties, or public client configuration. `COSMIX_NODE_BINARY` can select an installed Node executable when Node is not on PATH.

From `android`, run `gradlew.bat :app:verifyCosmixReleaseSigning`, then `:app:bundleRelease` (Play/internal distribution) or `:app:assembleRelease` (approved direct distribution). The build checks the current configuration, certificate validity/fingerprint, non-debug signing identity, key strength and key/certificate pairing. Missing inputs fail the release task; unsigned/debug-signed output is not a fallback. Debug builds do not require release secrets.

The pin set includes a backup and no expiry that could silently disable pin checking. Before rotating the server key, distribute an app containing the next key's pin and test it against the corresponding staged server. Retain an emergency app-distribution path. Only system CAs are trusted in release; the debug CA and Metro cleartext exceptions do not enter the release resource set.

Before distributing, verify the resulting APK/AAB signature against the approved fingerprint, inspect the merged release manifest/network policy, and exercise login, private document download, camera/attendance and upgrade behavior with the production-like server. Confirm that a valid chain with the wrong public key is rejected. These device/TLS/distribution checks need the approved host, pins and signing material and are not replaced by source tests.

References: [Android network security configuration](https://developer.android.com/privacy-and-security/security-config), [Android app signing](https://developer.android.com/studio/publish/app-signing).
