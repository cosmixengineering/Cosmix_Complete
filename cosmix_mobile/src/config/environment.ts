import { NativeModules } from 'react-native';

// Public client configuration, never an API key or credential.
// adb reverse forwards the debug API to the laptop while preserving its localhost-only host guard.
const releaseUrl: unknown =
  NativeModules.CosmixReleaseConfiguration?.apiBaseUrl;
if (
  !__DEV__ &&
  (typeof releaseUrl !== 'string' ||
    !/^https:\/\/[a-z0-9.-]+$/.test(releaseUrl))
) {
  throw new Error('Approved production API configuration is missing.');
}
export const environment = {
  apiBaseUrl: __DEV__ ? 'https://localhost:7241' : (releaseUrl as string),
  companyName: 'Cosmix',
  secureStoragePrefix: 'com.cosmix.mobile.v1',
} as const;
