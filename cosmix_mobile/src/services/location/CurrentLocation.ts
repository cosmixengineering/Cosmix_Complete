import { PermissionsAndroid, Platform } from 'react-native';
import Geolocation from '@react-native-community/geolocation';
import type { LocationSample } from '../../types/contracts';
import { AppError } from '../../utils/errors';

Geolocation.setRNConfiguration({
  skipPermissionRequests: Platform.OS === 'android',
  authorizationLevel: 'whenInUse',
  enableBackgroundLocationUpdates: false,
  locationProvider: 'android',
});

export class CurrentLocation {
  async ensurePermission(): Promise<void> {
    if (Platform.OS === 'android') {
      const grants = await PermissionsAndroid.requestMultiple([
        PermissionsAndroid.PERMISSIONS.ACCESS_FINE_LOCATION,
        PermissionsAndroid.PERMISSIONS.ACCESS_COARSE_LOCATION,
      ]);
      if (
        grants[PermissionsAndroid.PERMISSIONS.ACCESS_FINE_LOCATION] !==
        PermissionsAndroid.RESULTS.GRANTED
      ) {
        throw new AppError('location_permission');
      }
    }
    // iOS requests permission through the bounded position read. Waiting for
    // another authorization-change callback hangs once access is determined.
  }

  read(): Promise<LocationSample> {
    return new Promise((resolve, reject) =>
      Geolocation.getCurrentPosition(
        position =>
          resolve({
            latitude: position.coords.latitude,
            longitude: position.coords.longitude,
            accuracyMeters: position.coords.accuracy,
            capturedAtUtc: new Date(position.timestamp).toISOString(),
            mocked:
              (position as typeof position & { mocked?: boolean }).mocked ===
              true,
          }),
        error =>
          reject(
            new AppError(
              error.code === 1 ? 'location_permission' : 'location_unavailable',
            ),
          ),
        { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 },
      ),
    );
  }
}
