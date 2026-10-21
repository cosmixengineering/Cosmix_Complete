import * as Keychain from 'react-native-keychain';
import ReactNativeBlobUtil from 'react-native-blob-util';
import { environment } from '../../config/environment';
import type { RequestDraft, StoredRequest } from '../../types/selfService';
import { AppError } from '../../utils/errors';
import { isGuid, isRecord, isTimestamp } from '../../utils/validation';

const options = {
  service: `${environment.secureStoragePrefix}.service-request`,
  cloudSync: false,
  accessible: Keychain.ACCESSIBLE.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
  securityLevel: Keychain.SECURITY_LEVEL.SECURE_SOFTWARE,
  storage: Keychain.STORAGE_TYPE.AES_GCM_NO_AUTH,
};
const filePattern = /^ess_request_[a-f0-9]{64}\.json$/;

/** Credentials stay in Keychain. Bounded request content/evidence uses app-private,
 * backup-excluded OS cache only while an exact submission needs reconciliation. */
export class SecureRequestStore {
  private queue: Promise<unknown> = Promise.resolve();
  private path(fileName: string): string {
    if (!filePattern.test(fileName)) {
      throw new AppError('secure_storage_unavailable');
    }
    return `${ReactNativeBlobUtil.fs.dirs.CacheDir}/${fileName}`;
  }
  private serial<T>(action: () => Promise<T>): Promise<T> {
    const result = this.queue.then(action);
    this.queue = result.catch(() => undefined);
    return result;
  }
  async read(): Promise<RequestDraft | null> {
    return this.serial(async () => {
      const metadata = await this.readMetadata();
      const names = await ReactNativeBlobUtil.fs.ls(
        ReactNativeBlobUtil.fs.dirs.CacheDir,
      );
      await Promise.allSettled(
        names
          .filter(name => filePattern.test(name) && name !== metadata?.fileName)
          .map(name => ReactNativeBlobUtil.fs.unlink(this.path(name))),
      );
      if (
        metadata &&
        Date.now() - Date.parse(metadata.createdAtUtc) > 24 * 3600000
      ) {
        await ReactNativeBlobUtil.fs
          .unlink(this.path(metadata.fileName))
          .catch(() => undefined);
      }
      return metadata;
    });
  }
  private async readMetadata(): Promise<RequestDraft | null> {
    try {
      const stored = await Keychain.getGenericPassword(options);
      if (!stored) {
        return null;
      }
      if (stored.password.length > 4096) {
        throw new Error('invalid_metadata');
      }
      const value: unknown = JSON.parse(stored.password);
      if (
        !isRecord(value) ||
        value.version !== 1 ||
        !isGuid(value.accountId) ||
        !isGuid(value.sessionId) ||
        typeof value.fileName !== 'string' ||
        !filePattern.test(value.fileName) ||
        typeof value.route !== 'string' ||
        typeof value.key !== 'string' ||
        !/^[A-Za-z0-9_.:-]{8,100}$/.test(value.key) ||
        !isTimestamp(value.createdAtUtc) ||
        typeof value.service !== 'string' ||
        typeof value.label !== 'string' ||
        value.label.length > 100
      ) {
        throw new Error('invalid_metadata');
      }
      return value as RequestDraft;
    } catch {
      throw new AppError('secure_storage_unavailable');
    }
  }
  write(value: StoredRequest): Promise<void> {
    return this.serial(async () => {
      if (await this.readMetadata()) {
        throw new AppError('pending_service_request');
      }
      const { body, ...metadata } = value;
      const content = JSON.stringify(body);
      if (content.length > 29 * 1024 * 1024) {
        throw new AppError('evidence_too_large');
      }
      const path = this.path(value.fileName);
      try {
        await ReactNativeBlobUtil.fs.writeFile(path, content, 'utf8');
        if (
          !(await Keychain.setGenericPassword(
            'cosmix',
            JSON.stringify(metadata),
            options,
          ))
        ) {
          throw new Error('storage_failed');
        }
      } catch {
        await ReactNativeBlobUtil.fs.unlink(path).catch(() => undefined);
        throw new AppError('secure_storage_unavailable');
      }
    });
  }
  load(value: RequestDraft): Promise<StoredRequest> {
    return this.serial(async () => {
      const current = await this.readMetadata();
      if (
        !current ||
        current.key !== value.key ||
        current.fileName !== value.fileName
      ) {
        throw new AppError('request_reconciliation_required');
      }
      const path = this.path(value.fileName);
      if (Date.now() - Date.parse(value.createdAtUtc) > 24 * 3600000) {
        await ReactNativeBlobUtil.fs.unlink(path).catch(() => undefined);
        throw new AppError('request_reconciliation_required');
      }
      try {
        const stat = await ReactNativeBlobUtil.fs.stat(path);
        if (Number(stat.size) > 29 * 1024 * 1024) {
          throw new Error('size');
        }
        const body: unknown = JSON.parse(
          await ReactNativeBlobUtil.fs.readFile(path, 'utf8'),
        );
        return { ...current, body };
      } catch {
        throw new AppError('request_reconciliation_required');
      }
    });
  }
  clear(): Promise<void> {
    return this.serial(async () => {
      const metadata = await this.readMetadata();
      // Deleting the body first leaves a fail-closed marker if Keychain cannot clear.
      if (
        metadata &&
        (await ReactNativeBlobUtil.fs.exists(this.path(metadata.fileName)))
      ) {
        await ReactNativeBlobUtil.fs.unlink(this.path(metadata.fileName));
      }
      await Keychain.resetGenericPassword(options);
      if (await Keychain.getGenericPassword(options)) {
        throw new AppError('secure_storage_unavailable');
      }
    });
  }
}
