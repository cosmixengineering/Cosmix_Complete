// Public client configuration, never an API key or credential.
// Android emulator 10.0.2.2 maps to the laptop's loopback interface.
export const environment = {
  apiBaseUrl: 'https://10.0.2.2:7241',
  companyName: 'Cosmix',
  secureStoragePrefix: 'com.cosmix.mobile.v1',
} as const;
