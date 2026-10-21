import type { BackendApi, RequestOptions } from '../services/api/ApiClient';
import type {
  CurrentAccount,
  OtpChallenge,
  PermissionGrant,
  SessionResponse,
  SessionState,
  SessionVault,
  StoredSession,
} from '../types/contracts';
import { AppError } from '../utils/errors';
import {
  isGuid,
  isRecord,
  isTimestamp,
  normalizePhoneNumber,
  parseSession,
} from '../utils/validation';

type DeviceInfo = { platform: 'android' | 'ios'; deviceName: string };

export class SessionManager implements BackendApi {
  private state: SessionState = { status: 'booting', permissions: [] };
  private session: SessionResponse | null = null;
  private epoch = 0;
  private readonly listeners = new Set<() => void>();
  private initialization: Promise<void> | null = null;
  private devicePromise: Promise<string> | null = null;
  private refreshFlight: Promise<void> | null = null;
  private vaultQueue: Promise<unknown> = Promise.resolve();
  private verifying = false;

  constructor(
    private readonly api: BackendApi,
    private readonly vault: SessionVault,
    private readonly createSecret: () => string,
    private readonly device: DeviceInfo,
    private readonly now: () => number = Date.now,
  ) {}

  getSnapshot = (): SessionState => this.state;

  subscribe = (listener: () => void): (() => void) => {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  };

  initialize(): Promise<void> {
    this.initialization ??= this.restore();
    return this.initialization;
  }

  private async restore(): Promise<void> {
    try {
      const stored = await this.vault.readSession();
      if (!stored) {
        this.publish({ status: 'anonymous', permissions: [] });
        return;
      }
      if (
        stored.phase !== 'ready' ||
        !isTimestamp(stored.refreshTokenExpiresAt) ||
        Date.parse(stored.refreshTokenExpiresAt) <= this.now()
      ) {
        await this.endSession('session_refresh_uncertain');
        return;
      }
      await this.refresh();
    } catch {
      if (this.state.status === 'booting') {
        await this.endSession('secure_storage_unavailable');
      }
    }
  }

  async requestOtp(
    phone: string,
    previousChallengeId?: string,
  ): Promise<OtpChallenge> {
    const phoneNumber = normalizePhoneNumber(phone);
    const request = {
      phoneNumber,
      deviceSecret: await this.deviceSecret(),
      ...this.device,
    };
    const response = await this.api.request<unknown>(
      previousChallengeId
        ? '/api/v1/auth/mobile/otp/resend'
        : '/api/v1/auth/mobile/otp/request',
      {
        method: 'POST',
        body: previousChallengeId
          ? { challengeId: previousChallengeId, request }
          : request,
      },
    );
    if (
      !isRecord(response) ||
      !isGuid(response.challengeId) ||
      !isTimestamp(response.expiresAt) ||
      !isTimestamp(response.resendAvailableAt) ||
      response.nextStep !== 'enter_otp'
    ) {
      throw new AppError('invalid_server_response');
    }
    return response as OtpChallenge;
  }

  async verifyOtp(challengeId: string, code: string): Promise<void> {
    if (!isGuid(challengeId) || !/^[0-9]{6}$/.test(code)) {
      throw new AppError('invalid_otp');
    }
    if (this.verifying) {
      throw new AppError('operation_busy');
    }
    this.verifying = true;
    const epoch = this.epoch;
    let response: SessionResponse | null = null;
    try {
      const deviceSecret = await this.deviceSecret();
      this.requireEpoch(epoch);
      response = parseSession(
        await this.api.request('/api/v1/auth/mobile/otp/verify', {
          method: 'POST',
          body: { challengeId, code, deviceSecret },
        }),
      );
      this.requireEpoch(epoch);
      await this.acceptSession(response, epoch);
    } catch (error) {
      if (response && this.session !== response) {
        await this.revokeBestEffort(response.accessToken);
      }
      throw error;
    } finally {
      this.verifying = false;
    }
  }

  async request<T>(route: string, options: RequestOptions = {}): Promise<T> {
    if (this.state.status !== 'authenticated') {
      throw new AppError('authentication_required', 401);
    }
    const epoch = this.epoch;
    if (
      !this.session ||
      Date.parse(this.session.accessTokenExpiresAt) - this.now() < 30000
    ) {
      await this.refresh();
    }
    this.requireEpoch(epoch);
    const attemptedToken = this.session!.accessToken;
    try {
      const result = await this.api.request<T>(route, {
        ...options,
        accessToken: attemptedToken,
      });
      this.requireEpoch(epoch);
      return result;
    } catch (error) {
      if (!(error instanceof AppError) || error.status !== 401) {
        throw error;
      }
      // Only an explicit401 is retried after a single rotation. No retry on
      // timeouts/5xx; attendance retains its own exact mutation key and body.
      this.requireEpoch(epoch);
      if (this.session?.accessToken === attemptedToken) {
        await this.refresh();
      }
      this.requireEpoch(epoch);
      try {
        const result = await this.api.request<T>(route, {
          ...options,
          accessToken: this.session!.accessToken,
        });
        this.requireEpoch(epoch);
        return result;
      } catch (retryError) {
        if (retryError instanceof AppError && retryError.status === 401) {
          await this.endSession('session_expired', epoch);
        }
        throw retryError;
      }
    }
  }

  async revalidate(): Promise<void> {
    if (this.state.status !== 'authenticated') {
      return;
    }
    const epoch = this.epoch;
    try {
      const [account, permissions] = await Promise.all([
        this.request<unknown>('/api/v1/me'),
        this.request<unknown>('/api/v1/me/permissions'),
      ]);
      this.requireEpoch(epoch);
      this.publish({
        ...this.state,
        account: this.parseAccount(account),
        permissions: this.parsePermissions(permissions),
      });
    } catch (error) {
      if (epoch === this.epoch && this.state.status === 'authenticated') {
        this.publish({ ...this.state, permissions: [] });
      }
      throw error;
    }
  }

  async signOut(): Promise<void> {
    const token = this.session?.accessToken;
    this.epoch++;
    this.session = null;
    this.publish({ status: 'anonymous', permissions: [] });
    let erased = true;
    try {
      await this.withVault(() => this.vault.clearSession());
    } catch {
      erased = false;
    }
    const revoked = !token || (await this.revokeBestEffort(token));
    if (!erased) {
      this.publish({ ...this.state, message: 'secure_storage_unavailable' });
    } else if (!revoked) {
      this.publish({ ...this.state, message: 'signout_unconfirmed' });
    }
  }

  private refresh(): Promise<void> {
    if (this.refreshFlight) {
      return this.refreshFlight;
    }
    this.refreshFlight = this.rotate().finally(() => {
      this.refreshFlight = null;
    });
    return this.refreshFlight;
  }

  private async rotate(): Promise<void> {
    const epoch = this.epoch;
    try {
      const stored = await this.withVault(async () => {
        this.requireEpoch(epoch);
        const value = await this.vault.readSession();
        if (
          !value ||
          value.phase !== 'ready' ||
          !isTimestamp(value.refreshTokenExpiresAt) ||
          Date.parse(value.refreshTokenExpiresAt) <= this.now()
        ) {
          throw new AppError('session_expired', 401);
        }
        // A crash or uncertain response after this marker requires OTP sign-in;
        // never resend a potentially consumed rotating refresh secret.
        await this.vault.writeSession({ ...value, phase: 'rotating' });
        return value;
      });
      const deviceSecret = await this.vault.readDeviceSecret();
      if (!deviceSecret) {
        throw new AppError('session_expired', 401);
      }
      this.requireEpoch(epoch);
      const response = parseSession(
        await this.api.request('/api/v1/auth/refresh', {
          method: 'POST',
          body: { refreshToken: stored.refreshToken, deviceSecret },
        }),
      );
      this.requireEpoch(epoch);
      if (response.sessionId !== stored.sessionId) {
        throw new AppError('invalid_server_response');
      }
      await this.acceptSession(response, epoch);
    } catch (error) {
      await this.endSession('session_refresh_uncertain', epoch);
      throw error;
    }
  }

  private async acceptSession(
    response: SessionResponse,
    epoch: number,
  ): Promise<void> {
    const [rawAccount, rawPermissions] = await Promise.all([
      this.api.request<unknown>('/api/v1/me', {
        accessToken: response.accessToken,
      }),
      this.api.request<unknown>('/api/v1/me/permissions', {
        accessToken: response.accessToken,
      }),
    ]);
    const account = this.parseAccount(rawAccount);
    const permissions = this.parsePermissions(rawPermissions);
    await this.withVault(async () => {
      this.requireEpoch(epoch);
      const value: StoredSession = {
        sessionId: response.sessionId,
        refreshToken: response.refreshToken,
        refreshTokenExpiresAt: response.refreshTokenExpiresAt,
        accessTokenExpiresAt: response.accessTokenExpiresAt,
        tokenType: response.tokenType,
        nextStep: response.nextStep,
        phase: 'ready',
      };
      await this.vault.writeSession(value);
      this.requireEpoch(epoch);
      this.session = response;
      this.publish({
        status: 'authenticated',
        account,
        permissions,
        sessionId: response.sessionId,
      });
    });
  }

  private deviceSecret(): Promise<string> {
    this.devicePromise ??= this.withVault(async () => {
      const stored = await this.vault.readDeviceSecret();
      if (stored) {
        if (!/^[a-f0-9]{64}$/.test(stored)) {
          throw new AppError('secure_storage_unavailable');
        }
        return stored;
      }
      const value = this.createSecret();
      if (!/^[a-f0-9]{64}$/.test(value)) {
        throw new AppError('secure_storage_unavailable');
      }
      await this.vault.writeDeviceSecret(value);
      return value;
    }).catch(error => {
      this.devicePromise = null;
      throw error;
    });
    return this.devicePromise;
  }

  private async endSession(
    message: string,
    expectedEpoch = this.epoch,
  ): Promise<void> {
    if (expectedEpoch !== this.epoch) {
      return;
    }
    this.epoch++;
    this.session = null;
    this.publish({ status: 'anonymous', permissions: [], message });
    try {
      await this.withVault(() => this.vault.clearSession());
    } catch {
      this.publish({
        status: 'anonymous',
        permissions: [],
        message: 'secure_storage_unavailable',
      });
    }
  }

  private parseAccount(value: unknown): CurrentAccount {
    if (
      !isRecord(value) ||
      !isGuid(value.accountId) ||
      !isGuid(value.employeeId) ||
      value.mobileAccessEnabled !== true ||
      ![2, 'Mobile', 'mobile'].includes(value.client as string | number) ||
      !(typeof value.displayName === 'string' || value.displayName === null)
    ) {
      throw new AppError('invalid_server_response');
    }
    return value as CurrentAccount;
  }

  private parsePermissions(value: unknown): PermissionGrant[] {
    if (
      !Array.isArray(value) ||
      value.length > 5000 ||
      !value.every(
        grant =>
          isRecord(grant) &&
          typeof grant.permission === 'string' &&
          /^[a-z][a-z0-9_.]{0,149}$/.test(grant.permission) &&
          isGuid(grant.roleAssignmentId) &&
          Number.isInteger(grant.scopeKind) &&
          typeof grant.sensitive === 'boolean',
      )
    ) {
      throw new AppError('invalid_server_response');
    }
    return value as PermissionGrant[];
  }

  private requireEpoch(epoch: number): void {
    if (epoch !== this.epoch) {
      throw new AppError('session_changed');
    }
  }

  private withVault<T>(operation: () => Promise<T>): Promise<T> {
    const result = this.vaultQueue.then(operation);
    this.vaultQueue = result.catch(() => undefined);
    return result;
  }

  private publish(state: SessionState): void {
    this.state = Object.freeze({
      ...state,
      permissions: Object.freeze([...state.permissions]),
    });
    this.listeners.forEach(listener => listener());
  }

  private async revokeBestEffort(accessToken: string): Promise<boolean> {
    try {
      await this.api.request('/api/v1/auth/logout', {
        method: 'POST',
        accessToken,
      });
      return true;
    } catch {
      return false;
    }
  }
}
