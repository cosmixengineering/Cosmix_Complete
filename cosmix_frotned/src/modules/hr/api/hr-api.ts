import { z } from 'zod';
import { request } from '@/services/api/http-client';

export function queryString(values: Record<string, string | number | boolean | undefined>) {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(values))
    if (value !== undefined && value !== '') query.set(key, String(value));
  return query.size ? '?' + query.toString() : '';
}
export async function hrGet<T extends z.ZodType>(
  path: string,
  schema: T,
  signal?: AbortSignal,
): Promise<z.infer<T>> {
  return schema.parse(await request('/hr/' + path, { signal }));
}
export async function hrWrite(
  path: string,
  body: unknown,
  method: 'POST' | 'PUT',
  key: string,
  signal: AbortSignal,
) {
  return request('/hr/' + path, { method, body, idempotencyKey: key, signal });
}
export async function downloadFile(path: string, name: string, signal: AbortSignal) {
  const blob = await request('/hr/' + path, { responseType: 'blob', signal });
  signal.throwIfAborted();
  if (!(blob instanceof Blob)) throw new Error('Invalid document response.');
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = [...name]
    .map((c) => (c.charCodeAt(0) < 32 || c === '/' || c === '\\' ? '_' : c))
    .join('');
  link.rel = 'noopener';
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
