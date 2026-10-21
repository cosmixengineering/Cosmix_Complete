import * as Keychain from 'react-native-keychain';
import { environment } from '../../config/environment';
import type {
  PendingPunch,
  PendingPunchStore,
  SessionVault,
  StoredSession,
} from '../../types/contracts';
import { AppError } from '../../utils/errors';
import { isGuid, isRecord, isTimestamp } from '../../utils/validation';

export class SecureVault implements SessionVault, PendingPunchStore {
  constructor(
    private readonly clearAdditional: () => Promise<void> = async () =>
      undefined,
  ) {}
  private options(name: string): Keychain.SetOptions {
    return {
      service: `${environment.secureStoragePrefix}.${name}`,
      accessible: Keychain.ACCESSIBLE.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
      securityLevel: Keychain.SECURITY_LEVEL.SECURE_SOFTWARE,
      storage: Keychain.STORAGE_TYPE.AES_GCM_NO_AUTH,
      cloudSync: false,
    };
  }

  private async read(name: string): Promise<string | null> {
    try {
      const value = await Keychain.getGenericPassword(this.options(name));
      return value ? value.password : null;
    } catch {
      throw new AppError('secure_storage_unavailable');
    }
  }

  private async write(name: string, value: string): Promise<void> {
    try {
      if (
        !(await Keychain.setGenericPassword(
          'cosmix',
          value,
          this.options(name),
        ))
      ) {
        throw new Error('storage_failed');
      }
    } catch {
      throw new AppError('secure_storage_unavailable');
    }
  }

  private async clear(name: string): Promise<void> {
    try {
      await Keychain.resetGenericPassword(this.options(name));
      if (await this.read(name)) {
        throw new Error('storage_not_cleared');
      }
    } catch {
      throw new AppError('secure_storage_unavailable');
    }
  }

  readDeviceSecret(): Promise<string | null> {
    return this.read('installation');
  }
  writeDeviceSecret(value: string): Promise<void> {
    return this.write('installation', value);
  }

  async readSession(): Promise<StoredSession | null> {
    const raw = await this.read('session');
    if (!raw) {
      return null;
    }
    const value = this.parse(raw);
    if (
      !isRecord(value) ||
      !isGuid(value.sessionId) ||
      !['ready', 'rotating'].includes(String(value.phase)) ||
      typeof value.refreshToken !== 'string' ||
      value.refreshToken.length < 32 ||
      value.refreshToken.length > 4096 ||
      !isTimestamp(value.refreshTokenExpiresAt) ||
      !isTimestamp(value.accessTokenExpiresAt) ||
      value.tokenType !== 'Bearer' ||
      value.nextStep !== 'authenticated' ||
      'accessToken' in value
    ) {
      throw new AppError('secure_storage_unavailable');
    }
    return value as StoredSession;
  }

  writeSession(value: StoredSession): Promise<void> {
    return this.write('session', JSON.stringify(value));
  }

  async clearSession(): Promise<void> {
    // Attempt both erasures even if one OS storage operation fails.
    const results = await Promise.allSettled([
      this.clear('session'),
      this.clearPending(),
      this.clearAdditional(),
    ]);
    if (results.some(result => result.status === 'rejected')) {
      throw new AppError('secure_storage_unavailable');
    }
  }

  async readPending(): Promise<PendingPunch | null> {
    const raw = await this.read('pending');
    if (!raw) {
      return null;
    }
    const value = this.parse(raw);
    if (
      !isRecord(value) ||
      value.version !== 1 ||
      !isGuid(value.accountId) ||
      !isGuid(value.sessionId) ||
      !['check-in', 'check-out'].includes(String(value.endpoint)) ||
      typeof value.idempotencyKey !== 'string' ||
      !/^[A-Za-z0-9_.:-]{8,100}$/.test(value.idempotencyKey) ||
      !isTimestamp(value.createdAtUtc) ||
      !isRecord(value.body)
    ) {
      throw new AppError('secure_storage_unavailable');
    }
    const body = value.body;
    if (
      Object.keys(body).some(
        key =>
          ![
            'isOffline',
            'deviceCapturedAtUtc',
            'latitude',
            'longitude',
            'accuracyMeters',
            'faceAttemptId',
            'proof',
          ].includes(key),
      ) ||
      body.isOffline !== false ||
      !isTimestamp(body.deviceCapturedAtUtc) ||
      typeof body.latitude !== 'number' ||
      !Number.isFinite(body.latitude) ||
      Math.abs(body.latitude) > 90 ||
      typeof body.longitude !== 'number' ||
      !Number.isFinite(body.longitude) ||
      Math.abs(body.longitude) > 180 ||
      typeof body.accuracyMeters !== 'number' ||
      !Number.isFinite(body.accuracyMeters) ||
      body.accuracyMeters < 0 ||
      (value.endpoint === 'check-in' &&
        (!isGuid(body.faceAttemptId) ||
          typeof body.proof !== 'string' ||
          !/^[a-f0-9]{64}$/i.test(body.proof))) ||
      (value.endpoint === 'check-out' &&
        ('faceAttemptId' in body || 'proof' in body))
    ) {
      throw new AppError('secure_storage_unavailable');
    }
    return value as PendingPunch;
  }

  writePending(value: PendingPunch): Promise<void> {
    return this.write('pending', JSON.stringify(value));
  }
  clearPending(): Promise<void> {
    return this.clear('pending');
  }

  private parse(raw: string): unknown {
    try {
      if (raw.length > 16384) {
        throw new Error('storage_size');
      }
      return JSON.parse(raw) as unknown;
    } catch {
      throw new AppError('secure_storage_unavailable');
    }
  }
}
