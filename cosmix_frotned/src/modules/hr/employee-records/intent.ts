export type RecordIntent = { path: string; body: unknown; fingerprint: string; key: string };
export function captureRecordIntent(
  path: string,
  body: unknown,
  previous: RecordIntent | null,
): RecordIntent {
  const fingerprint = JSON.stringify([path, body]);
  if (previous) {
    if (previous.fingerprint !== fingerprint)
      throw new Error('Retry the unchanged action or reload and check the record before editing.');
    return previous;
  }
  return { path, body: structuredClone(body), fingerprint, key: crypto.randomUUID() };
}
