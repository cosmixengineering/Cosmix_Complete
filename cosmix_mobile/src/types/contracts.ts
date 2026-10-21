export type OtpChallenge = {
  challengeId: string;
  expiresAt: string;
  resendAvailableAt: string;
  nextStep: 'enter_otp';
};

export type SessionResponse = {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  sessionId: string;
  tokenType: 'Bearer';
  nextStep: 'authenticated';
};

export type StoredSession = Omit<SessionResponse, 'accessToken'> & {
  phase: 'ready' | 'rotating';
};

export type CurrentAccount = {
  accountId: string;
  employeeId: string;
  displayName: string | null;
  mobileAccessEnabled: boolean;
  webAccessEnabled: boolean;
  client: number | string;
};

export type PermissionGrant = {
  permission: string;
  roleAssignmentId: string;
  scopeKind: number;
  targetId: string | null;
  sensitive: boolean;
};

export type SessionState = {
  status: 'booting' | 'anonymous' | 'authenticated';
  account?: CurrentAccount;
  permissions: readonly PermissionGrant[];
  sessionId?: string;
  message?: string;
};

export type WorkSession = {
  id: string;
  sessionKey: string;
  businessDate: string;
  startsAtUtc: string;
  endsAtUtc: string | null;
  factualWorkedMinutes: number | null;
};

export type AttendanceState = {
  employeeId: string;
  serverTimeUtc: string;
  timeZone: string;
  state: string;
  nextAction: string;
  checkInEnabled: boolean;
  checkOutEnabled: boolean;
  faceStatus: string;
  activeSession: WorkSession | null;
  limitations: string[];
};

export type FaceCapability = {
  available: boolean;
  reasonCode: string;
  enrollmentActive: boolean;
  enrollmentCompatible: boolean;
  checkInFaceReady: boolean;
};

export type FaceChallenge = {
  id: string;
  challenge: string;
  expiresAtUtc: string;
  version: string;
};

export type FaceVerification = {
  id: string;
  outcome: string;
  reasonCode: string;
  expiresAtUtc: string;
  canSubmitCheckIn: boolean;
  version: string;
};

export type PunchBody = {
  isOffline: false;
  deviceCapturedAtUtc: string;
  latitude: number;
  longitude: number;
  accuracyMeters: number;
  faceAttemptId?: string;
  proof?: string;
};

export type PunchReceipt = {
  id: string;
  action: string;
  receivedAtUtc: string;
  businessDate: string;
  outcome: string;
  reasonCode: string;
  isOffline: boolean;
  siteId?: string | null;
  sessionKey?: string | null;
};

export type Page<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export type PendingPunch = {
  version: 1;
  accountId: string;
  sessionId: string;
  endpoint: 'check-in' | 'check-out';
  idempotencyKey: string;
  body: PunchBody;
  createdAtUtc: string;
};

export type LocationSample = {
  latitude: number;
  longitude: number;
  accuracyMeters: number;
  capturedAtUtc: string;
  mocked: boolean;
};

export type AttendanceStep =
  | 'preparing'
  | 'camera'
  | 'uploading'
  | 'verifying'
  | 'location'
  | 'submitting';

export interface SessionVault {
  readDeviceSecret(): Promise<string | null>;
  writeDeviceSecret(value: string): Promise<void>;
  readSession(): Promise<StoredSession | null>;
  writeSession(value: StoredSession): Promise<void>;
  clearSession(): Promise<void>;
}

export interface PendingPunchStore {
  readPending(): Promise<PendingPunch | null>;
  writePending(value: PendingPunch): Promise<void>;
  clearPending(): Promise<void>;
}
