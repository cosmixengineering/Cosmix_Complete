import type { BackendApi } from '../api/ApiClient';
import type {
  AttendanceState,
  AttendanceStep,
  FaceCapability,
  FaceChallenge,
  FaceVerification,
  LocationSample,
  PendingPunch,
  PendingPunchStore,
  PunchBody,
  PunchReceipt,
} from '../../types/contracts';
import { AppError } from '../../utils/errors';
import {
  isGuid,
  isRecord,
  isTimestamp,
  validateCaptureBase64,
  validateLocation,
} from '../../utils/validation';

type Identity = { accountId: string; sessionId: string };
export type AttendancePorts = {
  camera: { capture(): Promise<string> };
  location: {
    ensurePermission(): Promise<void>;
    read(): Promise<LocationSample>;
  };
  pending: PendingPunchStore;
  identity(): Identity;
  createKey(prefix: string): string;
  now(): number;
};
type Progress = (step: AttendanceStep) => void;

export class AttendanceWorkflow {
  private busy = false;
  private checkInKey: string | null = null;

  constructor(
    private readonly api: BackendApi,
    private readonly ports: AttendancePorts,
  ) {}

  checkIn(progress: Progress): Promise<PunchReceipt> {
    return this.exclusive(async () => {
      await this.requireNoPending();
      const identity = this.ports.identity();
      const started = this.ports.now();
      progress('preparing');
      const state = await this.api.request<AttendanceState>(
        '/api/v1/me/attendance/state',
      );
      this.requireState(state, 'check-in');
      const capability = await this.api.request<FaceCapability>(
        '/api/v1/attendance/face/capability',
      );
      if (!capability.enrollmentActive || !capability.enrollmentCompatible) {
        throw new AppError('face_enrollment_required');
      }
      if (
        capability.available !== true ||
        capability.checkInFaceReady !== true
      ) {
        throw new AppError('face_provider_unavailable');
      }
      await this.ports.location.ensurePermission();
      this.checkInKey ??= this.ports.createKey('punch-in');
      const challenge = await this.api.request<FaceChallenge>(
        '/api/v1/attendance/face/challenges',
        {
          method: 'POST',
          body: { punchIdempotencyKey: this.checkInKey },
          idempotencyKey: this.ports.createKey('challenge'),
        },
      );
      this.requireChallenge(challenge);
      const serverNow = () =>
        Date.parse(state.serverTimeUtc) + this.ports.now() - started;
      const ensureCurrent = () => {
        if (serverNow() >= Date.parse(challenge.expiresAtUtc)) {
          throw new AppError('face_challenge_expired');
        }
        this.requireIdentity(identity);
      };
      ensureCurrent();
      progress('camera');
      let image: string | null = await this.ports.camera.capture();
      try {
        validateCaptureBase64(image);
        ensureCurrent();
        progress('uploading');
        const capture = await this.api.request<{
          providerEvidenceToken: string;
          expiresAtUtc: string;
        }>(`/api/v1/attendance/face/challenges/${challenge.id}/captures`, {
          method: 'POST',
          idempotencyKey: this.ports.createKey('capture'),
          body: {
            challenge: challenge.challenge,
            expectedVersion: challenge.version,
            imageBase64: image,
          },
        });
        image = null;
        if (
          !capture ||
          typeof capture.providerEvidenceToken !== 'string' ||
          capture.providerEvidenceToken.length < 1 ||
          capture.providerEvidenceToken.length > 4096 ||
          !isTimestamp(capture.expiresAtUtc) ||
          Date.parse(capture.expiresAtUtc) > Date.parse(challenge.expiresAtUtc)
        ) {
          throw new AppError('invalid_server_response');
        }
        ensureCurrent();
        progress('verifying');
        const result = await this.api.request<FaceVerification>(
          `/api/v1/attendance/face/challenges/${challenge.id}/verify`,
          {
            method: 'POST',
            idempotencyKey: this.ports.createKey('verify'),
            timeoutMs: 60000,
            body: {
              challenge: challenge.challenge,
              expectedVersion: challenge.version,
              providerEvidenceToken: capture.providerEvidenceToken,
            },
          },
        );
        if (
          !result ||
          result.id !== challenge.id ||
          !isTimestamp(result.expiresAtUtc) ||
          Date.parse(result.expiresAtUtc) > Date.parse(challenge.expiresAtUtc)
        ) {
          throw new AppError('invalid_server_response');
        }
        if (result.outcome !== 'Passed' || result.canSubmitCheckIn !== true) {
          throw new AppError(
            result.reasonCode === 'PROVIDER_UNAVAILABLE'
              ? 'face_provider_unavailable'
              : 'face_verification_failed',
          );
        }
        ensureCurrent();
        if (serverNow() >= Date.parse(result.expiresAtUtc)) {
          throw new AppError('face_challenge_expired');
        }
        progress('location');
        const location = await this.ports.location.read();
        validateLocation(location, this.ports.now());
        ensureCurrent();
        const body: PunchBody = {
          ...this.locationBody(location),
          faceAttemptId: challenge.id,
          proof: challenge.challenge,
        };
        return await this.prepareAndSubmit(
          'check-in',
          this.checkInKey,
          body,
          identity,
          progress,
        );
      } finally {
        // Strings are not securely erasable in JS. Release references promptly;
        // native temporary image files are handled by the camera adapter.
        image = null;
      }
    });
  }

  checkOut(progress: Progress): Promise<PunchReceipt> {
    return this.exclusive(async () => {
      await this.requireNoPending();
      const identity = this.ports.identity();
      progress('preparing');
      const state = await this.api.request<AttendanceState>(
        '/api/v1/me/attendance/state',
      );
      this.requireState(state, 'check-out');
      await this.ports.location.ensurePermission();
      progress('location');
      const location = await this.ports.location.read();
      validateLocation(location, this.ports.now());
      this.requireIdentity(identity);
      return this.prepareAndSubmit(
        'check-out',
        this.ports.createKey('punch-out'),
        this.locationBody(location),
        identity,
        progress,
      );
    });
  }

  retryPending(progress: Progress): Promise<PunchReceipt> {
    return this.exclusive(async () => {
      const pending = await this.getPending();
      if (!pending) {
        throw new AppError('no_pending_attendance');
      }
      return this.submit(pending, progress);
    });
  }

  async getPending(): Promise<PendingPunch | null> {
    const pending = await this.ports.pending.readPending();
    if (!pending) {
      return null;
    }
    const identity = this.ports.identity();
    if (
      pending.accountId !== identity.accountId ||
      pending.sessionId !== identity.sessionId
    ) {
      await this.ports.pending.clearPending();
      return null;
    }
    return pending;
  }

  private async prepareAndSubmit(
    endpoint: PendingPunch['endpoint'],
    key: string,
    body: PunchBody,
    identity: Identity,
    progress: Progress,
  ): Promise<PunchReceipt> {
    this.requireIdentity(identity);
    const pending: PendingPunch = {
      version: 1,
      ...identity,
      endpoint,
      idempotencyKey: key,
      body,
      createdAtUtc: new Date(this.ports.now()).toISOString(),
    };
    await this.ports.pending.writePending(pending);
    this.requireIdentity(identity);
    return this.submit(pending, progress);
  }

  private async submit(
    pending: PendingPunch,
    progress: Progress,
  ): Promise<PunchReceipt> {
    this.requireIdentity(pending);
    progress('submitting');
    try {
      const receipt = await this.api.request<PunchReceipt>(
        `/api/v1/me/attendance/${pending.endpoint}`,
        {
          method: 'POST',
          idempotencyKey: pending.idempotencyKey,
          body: pending.body,
        },
      );
      const expectedAction =
        pending.endpoint === 'check-in' ? 'CheckIn' : 'CheckOut';
      if (
        !isRecord(receipt) ||
        !isGuid(receipt.id) ||
        receipt.action !== expectedAction ||
        !isTimestamp(receipt.receivedAtUtc) ||
        ![
          'Accepted',
          'AcceptedWithException',
          'QueuedForReview',
          'Rejected',
          'Unverified',
        ].includes(receipt.outcome) ||
        typeof receipt.reasonCode !== 'string' ||
        typeof receipt.businessDate !== 'string' ||
        !/^\d{4}-\d{2}-\d{2}$/.test(receipt.businessDate) ||
        receipt.isOffline !== false
      ) {
        throw new AppError('invalid_server_response');
      }
      this.requireIdentity(pending);
      await this.ports.pending.clearPending();
      this.checkInKey = null;
      return receipt;
    } catch (error) {
      if (
        error instanceof AppError &&
        error.status >= 400 &&
        error.status < 500 &&
        ![408, 429].includes(error.status)
      ) {
        await this.ports.pending.clearPending();
        throw error;
      }
      // The server may already have committed. Keep the exact key and payload,
      // including its original location/proof, for explicit receipt reconciliation.
      throw new AppError('attendance_result_unknown');
    }
  }

  private locationBody(value: LocationSample): PunchBody {
    return {
      isOffline: false,
      deviceCapturedAtUtc: value.capturedAtUtc,
      latitude: value.latitude,
      longitude: value.longitude,
      accuracyMeters: value.accuracyMeters,
    };
  }

  private requireChallenge(value: FaceChallenge): void {
    if (
      !value ||
      !isGuid(value.id) ||
      typeof value.challenge !== 'string' ||
      !/^[a-f0-9]{64}$/i.test(value.challenge) ||
      !isTimestamp(value.expiresAtUtc) ||
      typeof value.version !== 'string' ||
      !/^[A-Za-z0-9+/]{11}=$/.test(value.version)
    ) {
      throw new AppError('invalid_server_response');
    }
  }

  private requireState(
    value: AttendanceState,
    action: PendingPunch['endpoint'],
  ): void {
    if (
      !value ||
      !isGuid(value.employeeId) ||
      !isTimestamp(value.serverTimeUtc) ||
      typeof value.checkInEnabled !== 'boolean' ||
      typeof value.checkOutEnabled !== 'boolean'
    ) {
      throw new AppError('invalid_server_response');
    }
    if (
      !(action === 'check-in' ? value.checkInEnabled : value.checkOutEnabled)
    ) {
      throw new AppError('attendance_not_available');
    }
  }

  private async requireNoPending(): Promise<void> {
    if (await this.getPending()) {
      throw new AppError('pending_attendance');
    }
  }

  private requireIdentity(expected: Identity): void {
    const actual = this.ports.identity();
    if (
      actual.accountId !== expected.accountId ||
      actual.sessionId !== expected.sessionId
    ) {
      throw new AppError('session_changed', 401);
    }
  }

  private async exclusive<T>(operation: () => Promise<T>): Promise<T> {
    if (this.busy) {
      throw new AppError('attendance_busy');
    }
    this.busy = true;
    try {
      return await operation();
    } finally {
      this.busy = false;
    }
  }
}
