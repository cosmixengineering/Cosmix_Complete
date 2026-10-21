import { z } from 'zod';
import { request } from '@/services/api/http-client';

export { queryString } from './hr-api';

// Finance endpoints are not all under /hr: self service lives under /me, review under
// /attendance and the payroll handoff under /payroll-handoff, so paths are passed whole.
export async function financeGet<T extends z.ZodType>(
  path: string,
  schema: T,
  signal?: AbortSignal,
): Promise<z.infer<T>> {
  return schema.parse(await request(path, { signal }));
}

export async function financeWrite<T extends z.ZodType>(
  path: string,
  body: unknown,
  method: 'POST' | 'PUT',
  key: string,
  schema: T,
  signal: AbortSignal,
): Promise<z.infer<T>> {
  return schema.parse(await request(path, { method, body, idempotencyKey: key, signal }));
}
