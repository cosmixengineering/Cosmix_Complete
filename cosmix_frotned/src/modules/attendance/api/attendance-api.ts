import { z } from 'zod';
import { request } from '@/services/api/http-client';

export async function attendanceGet<T extends z.ZodType>(
  path: string,
  schema: T,
  signal?: AbortSignal,
): Promise<z.infer<T>> {
  return schema.parse(await request(path, { signal }));
}
export async function attendanceWrite<T extends z.ZodType>(
  path: string,
  body: unknown,
  method: 'POST' | 'PUT',
  schema: T,
  key: string,
  signal: AbortSignal,
): Promise<z.infer<T>> {
  return schema.parse(await request(path, { method, body, idempotencyKey: key, signal }));
}
export function queryString(values: Record<string, string | number | boolean | undefined>) {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(values))
    if (value !== undefined && value !== '') query.set(key, String(value));
  return query.size ? '?' + query.toString() : '';
}
