import { AppError } from './errors';

export function randomSecret(): string {
  const crypto = (
    globalThis as unknown as {
      crypto?: { getRandomValues<T extends Uint8Array>(array: T): T };
    }
  ).crypto;
  if (!crypto?.getRandomValues) {
    throw new AppError('secure_storage_unavailable');
  }
  const bytes = crypto.getRandomValues(new Uint8Array(32));
  return Array.from(bytes, value => value.toString(16).padStart(2, '0')).join(
    '',
  );
}

export function requestKey(prefix: string): string {
  return `${prefix}:${randomSecret()}`;
}
