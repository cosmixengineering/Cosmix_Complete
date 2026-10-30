# Android production release

Debug builds keep the approved emulator origin and local test CA. Production builds take their API origin from a native build constant and use a generated Android network security policy. The selected company LAN origin is `https://192.168.1.20`. Its actual IIS certificate/CA, pins and signing identity must still be supplied; no signing identity or certificate pin has been invented.

Provide an approved JSON file outside the checkout and set `COSMIX_ANDROID_RELEASE_CONFIG` to its absolute path. Fields:

- `apiBaseUrl`: HTTPS DNS origin on port 443, without an API path, credentials, query or fragment; explicitly configured private-CA mode also supports RFC1918 IPv4 origins such as `https://192.168.1.20`.
- `networkTrust`: `system` (default) or `private-ca`. Private-CA mode requires `privateTrustAnchor` containing `certificatePem` (one public PEM CA certificate, never a private key) and `sha256` (approved DER certificate SHA-256 fingerprint, 64 hex characters). The generator validates CA status, validity dates, key strength and fingerprint. Only that API host trusts the supplied CA; system trust remains the default for other hosts. The actual IIS leaf certificate must have the IP in its Subject Alternative Name. Never substitute the Ubuntu face-server CA unless that CA actually issued the IIS certificate.
- `certificatePins`: two to five distinct base64 SHA-256 hashes of certificate **SubjectPublicKeyInfo** values: current and independently retained backup keys. These are not whole-certificate hashes.
- `signingCertificateSha256`: approved upload/signing certificate's SHA-256 fingerprint as 64 hex characters, without colons.
- `versionCode`: positive integer, greater than the previously distributed release.
- `versionName`: semantic version, for example `1.0.0`.

Keep the actual keystore outside the checkout. Supply `COSMIX_ANDROID_KEYSTORE`, `COSMIX_ANDROID_KEY_ALIAS`, `COSMIX_ANDROID_STORE_PASSWORD`, and `COSMIX_ANDROID_KEY_PASSWORD` through the private build environment. Do not put passwords in command arguments, tracked Gradle properties, or public client configuration. `COSMIX_NODE_BINARY` can select an installed Node executable when Node is not on PATH.

From `android`, run `gradlew.bat :app:verifyCosmixReleaseSigning`, then `:app:bundleRelease` (Play/internal distribution) or `:app:assembleRelease` (approved direct distribution). The build checks the current configuration, certificate validity/fingerprint, non-debug signing identity, key strength and key/certificate pairing. Missing inputs fail the release task; unsigned/debug-signed output is not a fallback. Debug builds do not require release secrets.

The pin set includes a backup and no expiry that could silently disable pin checking. Before rotating the server key, distribute an app containing the next key's pin and test it against the corresponding staged server. Retain an emergency app-distribution path. Release uses system CAs by default or the explicitly approved public CA for the configured API host. Private-CA trust never overrides pin checks. The debug CA, user-installed CAs and Metro cleartext exceptions do not enter the release resource set. Switching back to system trust removes the generated private CA resource.

For the LAN deployment, build after IIS has its real certificate and current/backup SPKI pins. A public CA is safe to bundle; its private signing key must never enter the app, release JSON or repository. Certificate validation is not disabled to accommodate the private IP. Employees need company LAN/VPN reachability; this IP alone does not provide internet access.

Before distributing, verify the resulting APK/AAB signature against the approved fingerprint, inspect the merged release manifest/network policy, and exercise login, private document download, camera/attendance and upgrade behavior with the production-like server. Confirm that a valid chain with the wrong public key is rejected. These device/TLS/distribution checks need the approved host, pins and signing material and are not replaced by source tests.

References: [Android network security configuration](https://developer.android.com/privacy-and-security/security-config), [Android app signing](https://developer.android.com/studio/publish/app-signing).

## Windows native builds

React Native's generated C++ filenames can exceed Ninja's Windows path limit in a long checkout directory. On Windows, the app stages CMake intermediates in `<Gradle user home>/cosmix-native/<checkout-path hash>` and sets `CMAKE_OBJECT_PATH_MAX=240`, which lets CMake shorten object paths using its deterministic hash. Separate checkout paths receive separate staging directories. Linux paths and signing remain unchanged. See [CMake's object path limit](https://cmake.org/cmake/help/latest/variable/CMAKE_OBJECT_PATH_MAX.html).

For an x86_64 Android emulator, use `gradlew.bat :app:assembleDebug -PreactNativeArchitectures=x86_64`. This verifies that ABI only; omit the architecture override when building the complete configured ABI set. Build intermediates are disposable caches, not release artifacts.

## Local emulator and USB verification

Debug builds use `https://localhost:7241` for the API. With the selected device connected, run `adb reverse tcp:7241 tcp:7241` and `adb reverse tcp:8081 tcp:8081` before launching the app. Select the intended device with `adb -s <serial>` when more than one is connected. The HTTPS API still needs a localhost SAN certificate issued by the public development CA already bundled in the debug resources.

This preserves the backend's local-testing hostname and loopback restrictions. Connecting that guarded test profile through `10.0.2.2` instead returns a rejected-host response even when TLS succeeds. Do not widen the test host's guard or use a wildcard host to accommodate the emulator. Production continues to use the approved release origin and release-only certificate policy; ADB forwarding and debug CA trust are not production deployment steps.
