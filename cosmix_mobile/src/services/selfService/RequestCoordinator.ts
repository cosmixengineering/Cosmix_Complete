import type { BackendApi } from '../api/ApiClient';
import type { SecureRequestStore } from '../storage/SecureRequestStore';
import type {
  RequestDraft,
  ServiceKey,
  StoredRequest,
  DataRecord,
} from '../../types/selfService';
import { AppError } from '../../utils/errors';
import { isGuid, isRecord } from '../../utils/validation';
import { requestKey, randomSecret } from '../../utils/random';

const id = '[a-fA-F0-9-]{36}';
const allowed = new RegExp(
  `^/api/v1/(?:leaves/mine(?:/encashment|/${id}/cancellations)?|me/(?:advances-loans/requests(?:/${id}/cancel)?|resignation(?:/${id}/cancel)?|allowances/claims(?:/${id}/cancel)?|overtime/requests(?:/${id}/cancel)?|vehicles/(?:trips|fuel-claims|maintenance|incidents)|attendance/corrections(?:/${id}/cancel)?))$`,
);

export class RequestCoordinator {
  private busy = false;
  constructor(
    private readonly api: BackendApi,
    private readonly store: SecureRequestStore,
    private readonly identity: () => { accountId: string; sessionId: string },
  ) {}
  async pending(): Promise<RequestDraft | null> {
    const value = await this.store.read();
    if (!value) {
      return null;
    }
    const identity = this.identity();
    if (
      value.accountId !== identity.accountId ||
      value.sessionId !== identity.sessionId
    ) {
      await this.store.clear();
      return null;
    }
    if (!allowed.test(value.route)) {
      throw new AppError('request_reconciliation_required');
    }
    return value;
  }
  submit(
    service: ServiceKey,
    label: string,
    route: string,
    body: unknown,
  ): Promise<DataRecord> {
    return this.exclusive(async () => {
      if (!allowed.test(route)) {
        throw new AppError('invalid_server_response');
      }
      if (await this.pending()) {
        throw new AppError('pending_service_request');
      }
      const identity = this.identity();
      const value: StoredRequest = {
        version: 1,
        ...identity,
        service,
        label,
        route,
        body,
        key: requestKey('ess'),
        fileName: `ess_request_${randomSecret()}.json`,
        createdAtUtc: new Date().toISOString(),
      };
      this.requireIdentity(value);
      await this.store.write(value);
      this.requireIdentity(value);
      return this.send(value);
    });
  }
  retry(): Promise<DataRecord> {
    return this.exclusive(async () => {
      const value = await this.pending();
      if (!value) {
        throw new AppError('no_pending_service_request');
      }
      const stored = await this.store.load(value);
      this.requireIdentity(stored);
      return this.send(stored);
    });
  }
  private async send(value: StoredRequest): Promise<DataRecord> {
    try {
      const result = await this.api.request<unknown>(value.route, {
        method: 'POST',
        body: value.body,
        idempotencyKey: value.key,
        timeoutMs: 60000,
      });
      if (!isRecord(result) || !isGuid(result.id)) {
        throw new AppError('invalid_server_response');
      }
      this.requireIdentity(value);
      await this.store.clear();
      return result;
    } catch (error) {
      if (
        error instanceof AppError &&
        [
          'idempotency_key_expired',
          'idempotency_result_unavailable',
          'idempotency_key_reused',
        ].includes(error.code)
      ) {
        throw new AppError('request_reconciliation_required');
      }
      if (
        error instanceof AppError &&
        error.status >= 400 &&
        error.status < 500 &&
        ![408, 429].includes(error.status)
      ) {
        await this.store.clear();
        throw error;
      }
      throw new AppError('service_request_unknown');
    }
  }
  private requireIdentity(expected: RequestDraft): void {
    const actual = this.identity();
    if (
      expected.accountId !== actual.accountId ||
      expected.sessionId !== actual.sessionId
    ) {
      throw new AppError('session_changed', 401);
    }
  }
  private async exclusive<T>(action: () => Promise<T>): Promise<T> {
    if (this.busy) {
      throw new AppError('operation_busy');
    }
    this.busy = true;
    try {
      return await action();
    } finally {
      this.busy = false;
    }
  }
}
