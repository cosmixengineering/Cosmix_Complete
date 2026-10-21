import { z } from 'zod';
import { env } from '@/config/env';
import { SESSION_EXPIRED_EVENT, SESSION_UNAVAILABLE_EVENT } from '@/constants/events';
import { ApiError } from './api-error';

export interface RequestOptions extends Omit<RequestInit, 'body' | 'credentials' | 'headers'> {
  body?: unknown;
  anonymous?: boolean;
  idempotencyKey?: string;
  responseType?: 'json' | 'blob';
}
const csrfSchema = z.object({
  requestToken: z.string().min(1),
  headerName: z.literal('X-CSRF-TOKEN'),
});
let csrfPending: Promise<z.infer<typeof csrfSchema>> | undefined;
let refreshPending: Promise<void> | undefined;
let refreshBlocked = false;
const refreshBlockKey = 'cosmix.refresh-requires-sign-in';

export function allowSessionRefresh() {
  refreshBlocked = false;
  try {
    localStorage.removeItem(refreshBlockKey);
  } catch {
    refreshBlocked = true;
  }
}
export function blockSessionRefresh() {
  refreshBlocked = true;
  // A non-sensitive stop flag, never a token or proof of authentication. It prevents
  // another tab/reload replaying an uncertain refresh; only fresh sign-in clears it.
  try {
    localStorage.setItem(refreshBlockKey, '1');
  } catch {
    /* Memory still fails closed. */
  }
}
function isRefreshBlocked() {
  try {
    return refreshBlocked || localStorage.getItem(refreshBlockKey) === '1';
  } catch {
    return true;
  }
}

function endpoint(path: string) {
  if (!path.startsWith('/') || path.startsWith('//') || path.includes('\\')) {
    throw new Error('API requests must use a relative endpoint path.');
  }
  const url = new URL(env.apiBaseUrl + path);
  const base = new URL(env.apiBaseUrl);
  if (url.origin !== base.origin || !url.pathname.startsWith(base.pathname + '/')) {
    throw new Error('API request is outside the configured endpoint scope.');
  }
  return url;
}

async function send(
  path: string,
  options: RequestOptions = {},
  csrfToken?: string,
): Promise<unknown> {
  const { body, signal, idempotencyKey, responseType = 'json', ...init } = options;
  delete init.anonymous;
  const headers = new Headers({ Accept: 'application/json' });
  const multipart = body instanceof FormData;
  if (body !== undefined && !multipart) headers.set('Content-Type', 'application/json');
  if (idempotencyKey) {
    if (!/^[A-Za-z0-9_.:-]{8,100}$/.test(idempotencyKey)) throw new Error('Invalid operation key.');
    headers.set('Idempotency-Key', idempotencyKey);
  }
  if (csrfToken) headers.set('X-CSRF-TOKEN', csrfToken);
  const timeout = AbortSignal.timeout(30_000);
  const response = await fetch(endpoint(path), {
    ...init,
    headers,
    credentials: 'include',
    redirect: 'error',
    cache: 'no-store',
    signal: signal ? AbortSignal.any([signal, timeout]) : timeout,
    body: body === undefined ? undefined : multipart ? body : JSON.stringify(body),
  });
  if (!response.ok) {
    const problem = z
      .object({
        title: z.string().optional(),
        code: z.string().optional(),
        traceId: z.string().optional(),
      })
      .safeParse(await response.json().catch(() => null));
    const details = problem.success ? problem.data : undefined;
    const retryHeader = response.headers.get('Retry-After');
    const retryAfter = retryHeader && /^\d+$/.test(retryHeader) ? Number(retryHeader) : undefined;
    throw new ApiError(
      response.status,
      details?.traceId ?? response.headers.get('X-Correlation-ID') ?? undefined,
      details?.code ?? details?.title,
      retryAfter,
    );
  }
  return response.status === 204
    ? undefined
    : responseType === 'blob'
      ? response.blob()
      : response.json();
}

// A fresh identity-bound CSRF token for each write; only in-flight reads are shared.
export function getCsrf() {
  const read = () => send('/auth/web/csrf');
  csrfPending ??= (navigator.locks ? navigator.locks.request('cosmix-web-csrf', read) : read())
    .then((value) => csrfSchema.parse(value))
    .finally(() => {
      csrfPending = undefined;
    });
  return csrfPending;
}

async function renewSession() {
  if (isRefreshBlocked() || !navigator.locks) throw new ApiError(401);
  refreshPending ??= navigator.locks
    .request('cosmix-web-session-refresh', async () => {
      if (isRefreshBlocked()) throw new ApiError(401);
      // Another tab may have rotated cookies while this tab waited for the lock.
      try {
        await send('/me/permissions');
        return;
      } catch (error) {
        if (!(error instanceof ApiError && error.status === 401)) throw error;
      }
      const csrf = await getCsrf();
      try {
        await send('/auth/web/refresh', { method: 'POST' }, csrf.requestToken);
      } catch (error) {
        // Never automatically replay a potentially consumed refresh secret.
        blockSessionRefresh();
        throw error;
      }
    })
    .finally(() => {
      refreshPending = undefined;
    });
  return refreshPending;
}

export async function request(path: string, options: RequestOptions = {}): Promise<unknown> {
  options.signal?.throwIfAborted();
  endpoint(path);
  const read = ['GET', 'HEAD', 'OPTIONS'].includes((options.method ?? 'GET').toUpperCase());
  const csrf = read ? undefined : await getCsrf();
  options.signal?.throwIfAborted();
  try {
    return await send(path, options, csrf?.requestToken);
  } catch (error) {
    options.signal?.throwIfAborted();
    if (!(error instanceof ApiError && error.status === 401) || options.anonymous) throw error;
    if (read) {
      try {
        await renewSession();
        options.signal?.throwIfAborted();
        return await send(path, options);
      } catch (refreshError) {
        // Cancellation belongs to the obsolete caller, not the current authentication state.
        options.signal?.throwIfAborted();
        if (!(refreshError instanceof ApiError && refreshError.status === 401)) {
          window.dispatchEvent(new Event(SESSION_UNAVAILABLE_EVENT));
          throw refreshError;
        }
      }
    }
    options.signal?.throwIfAborted();
    window.dispatchEvent(new Event(SESSION_EXPIRED_EVENT));
    throw error;
  }
}
