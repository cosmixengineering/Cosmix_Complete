import { AppError } from '../../utils/errors';
import { isRecord, validateApiBaseUrl } from '../../utils/validation';

export type RequestOptions = {
  method?: 'GET' | 'POST' | 'DELETE';
  body?: unknown;
  accessToken?: string;
  idempotencyKey?: string;
  timeoutMs?: number;
};

export type TransportRequest = {
  url: string;
  method: 'GET' | 'POST' | 'DELETE';
  headers: Record<string, string>;
  body?: string;
  timeoutMs: number;
  followRedirect: false;
};

export type TransportResponse = {
  status: number;
  contentType: string;
  body: string;
};
export type Transport = (
  request: TransportRequest,
) => Promise<TransportResponse>;
export interface BackendApi {
  request<T>(route: string, options?: RequestOptions): Promise<T>;
}

export class ApiClient implements BackendApi {
  private readonly baseUrl: string;

  constructor(baseUrl: string, private readonly transport: Transport) {
    this.baseUrl = validateApiBaseUrl(baseUrl);
  }

  async request<T>(route: string, options: RequestOptions = {}): Promise<T> {
    if (
      !/^\/api\/v1\/[A-Za-z0-9/?=&_.:%-]+$/.test(route) ||
      route.includes('..') ||
      route.includes('//')
    ) {
      throw new AppError('invalid_server_response');
    }
    const headers: Record<string, string> = {
      Accept: 'application/json',
      'Cache-Control': 'no-store',
    };
    if (options.body !== undefined) {
      headers['Content-Type'] = 'application/json';
    }
    if (options.accessToken) {
      headers.Authorization = `Bearer ${options.accessToken}`;
    }
    if (options.idempotencyKey) {
      if (!/^[A-Za-z0-9_.:-]{8,100}$/.test(options.idempotencyKey)) {
        throw new AppError('invalid_server_response');
      }
      headers['Idempotency-Key'] = options.idempotencyKey;
    }
    let response: TransportResponse;
    try {
      response = await this.transport({
        url: this.baseUrl + route,
        method: options.method ?? 'GET',
        headers,
        body:
          options.body === undefined ? undefined : JSON.stringify(options.body),
        timeoutMs: options.timeoutMs ?? 20000,
        followRedirect: false,
      });
    } catch (error) {
      if (error instanceof AppError) {
        throw error;
      }
      throw new AppError('network_error');
    }
    if (response.status >= 300 && response.status < 400) {
      throw new AppError('invalid_server_response', response.status);
    }
    if (response.status === 204) {
      return undefined as T;
    }
    if (
      response.body.length > 2 * 1024 * 1024 ||
      !/^application\/(?:problem\+)?json(?:;|$)/i.test(response.contentType)
    ) {
      throw new AppError('invalid_server_response', response.status);
    }
    let data: unknown;
    try {
      data = JSON.parse(response.body);
    } catch {
      throw new AppError('invalid_server_response', response.status);
    }
    if (response.status < 200 || response.status >= 300) {
      const defaultCode =
        response.status === 401
          ? 'invalid_authentication'
          : response.status === 403
          ? 'access_denied'
          : 'service_unavailable';
      // Existing API failures carry the machine code in ProblemDetails.title.
      // Accept only code-shaped strings; presentation uses our own message map.
      const candidate = isRecord(data) ? data.code ?? data.title : undefined;
      const code =
        typeof candidate === 'string' &&
        /^[A-Za-z0-9_.-]{1,120}$/.test(candidate)
          ? candidate
          : defaultCode;
      const reference =
        isRecord(data) &&
        typeof data.correlationId === 'string' &&
        /^[A-Za-z0-9_.:-]{1,100}$/.test(data.correlationId)
          ? data.correlationId
          : undefined;
      throw new AppError(code, response.status, reference);
    }
    return data as T;
  }
}
