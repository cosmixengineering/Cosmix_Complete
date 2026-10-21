import { AppError } from './errors';
import type {
  LocationSample,
  PunchReceipt,
  SessionResponse,
} from '../types/contracts';

export const MAX_CAPTURE_BYTES = 512 * 1024;

export function normalizePhoneNumber(input: string): string {
  const value = input.trim().replace(/[ ()-]/g, '');
  if (!/^\+[1-9][0-9]{7,14}$/.test(value)) {
    throw new AppError('invalid_phone');
  }
  return value;
}

function asciiDigits(value: string): string {
  return value.replace(/[\u0660-\u0669\u06F0-\u06F9]/g, digit =>
    String(
      digit.charCodeAt(0) - (digit.charCodeAt(0) >= 0x06f0 ? 0x06f0 : 0x0660),
    ),
  );
}

export function normalizePakistanPhoneNumber(input: string): string {
  let national = asciiDigits(input)
    .trim()
    .replace(/[ ()-]/g, '');
  if (national.startsWith('+92')) national = national.slice(3);
  else if (national.startsWith('0092')) national = national.slice(4);
  else if (/^923\d{9}$/.test(national)) national = national.slice(2);
  else if (national.startsWith('0')) national = national.slice(1);
  if (!/^3\d{9}$/.test(national)) throw new AppError('invalid_pakistan_phone');
  return normalizePhoneNumber(`+92${national}`);
}

export function sanitizeOtp(value: string): string {
  return asciiDigits(value)
    .replace(/[^0-9]/g, '')
    .slice(0, 6);
}

export function validateApiBaseUrl(value: string): string {
  // An explicit origin only: no credentials, route prefix, query or fragment.
  if (
    !/^https:\/\/(?:[a-zA-Z0-9.-]+|\[[0-9a-fA-F:]+\])(?::[0-9]{1,5})?\/?$/.test(
      value,
    )
  ) {
    throw new AppError('invalid_server_response');
  }
  return value.replace(/\/$/, '');
}

export function validateCaptureBase64(value: string): void {
  if (
    typeof value !== 'string' ||
    value.length < 32 ||
    value.length > 699052 ||
    value.length % 4 !== 0 ||
    !/^[A-Za-z0-9+/]+={0,2}$/.test(value) ||
    !value.startsWith('/9j/')
  ) {
    throw new AppError('invalid_face_image');
  }
  const padding = value.endsWith('==') ? 2 : value.endsWith('=') ? 1 : 0;
  if ((value.length / 4) * 3 - padding > MAX_CAPTURE_BYTES) {
    throw new AppError('invalid_face_image');
  }
}

export function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

export function isGuid(value: unknown): value is string {
  return (
    typeof value === 'string' &&
    /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
      value,
    )
  );
}

export function isTimestamp(value: unknown): value is string {
  return (
    typeof value === 'string' &&
    value.length <= 40 &&
    Number.isFinite(Date.parse(value))
  );
}

export function parseSession(value: unknown): SessionResponse {
  if (
    !isRecord(value) ||
    typeof value.accessToken !== 'string' ||
    value.accessToken.length < 16 ||
    value.accessToken.length > 16384 ||
    typeof value.refreshToken !== 'string' ||
    value.refreshToken.length < 32 ||
    value.refreshToken.length > 256 ||
    !isGuid(value.sessionId) ||
    !isTimestamp(value.accessTokenExpiresAt) ||
    !isTimestamp(value.refreshTokenExpiresAt) ||
    value.tokenType !== 'Bearer' ||
    value.nextStep !== 'authenticated'
  ) {
    throw new AppError('invalid_server_response');
  }
  return value as SessionResponse;
}

export function validateLocation(value: LocationSample, now: number): void {
  if (value.mocked) {
    throw new AppError('location_mocked');
  }
  if (
    !Number.isFinite(value.latitude) ||
    value.latitude < -90 ||
    value.latitude > 90 ||
    !Number.isFinite(value.longitude) ||
    value.longitude < -180 ||
    value.longitude > 180 ||
    !Number.isFinite(value.accuracyMeters) ||
    value.accuracyMeters < 0 ||
    !isTimestamp(value.capturedAtUtc)
  ) {
    throw new AppError('location_unavailable');
  }
  const age = now - Date.parse(value.capturedAtUtc);
  if (age > 30000 || age < -5000) {
    throw new AppError('location_stale');
  }
}

export function isAcceptedReceipt(
  value: Pick<PunchReceipt, 'outcome'>,
): boolean {
  return value.outcome === 'Accepted';
}
