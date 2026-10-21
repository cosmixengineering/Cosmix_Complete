export type TaxonomyIntent = {
  readonly path: string;
  readonly body: unknown;
  readonly fingerprint: string;
  readonly key: string;
};
function freeze(value: unknown): unknown {
  if (value && typeof value === 'object') {
    Object.values(value).forEach(freeze);
    Object.freeze(value);
  }
  return value;
}
export function captureTaxonomyIntent(
  path: string,
  body: unknown,
  previous: TaxonomyIntent | null,
): TaxonomyIntent {
  const fingerprint = JSON.stringify([path, body]);
  if (previous) {
    if (previous.fingerprint !== fingerprint)
      throw new Error('Retry the unchanged action or reload and review current state.');
    return previous;
  }
  return Object.freeze({
    path,
    body: freeze(structuredClone(body)),
    fingerprint,
    key: crypto.randomUUID(),
  });
}
