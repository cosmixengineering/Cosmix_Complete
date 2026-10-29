import { NativeModules } from 'react-native';

// Public client configuration, never an API key or credential.
// Android emulator 10.0.2.2 maps to the laptop's loopback interface.
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
  apiBaseUrl: __DEV__ ? 'https://10.0.2.2:7241' : (releaseUrl as string),
  companyName: 'Cosmix',
  secureStoragePrefix: 'com.cosmix.mobile.v1',
} as const;
