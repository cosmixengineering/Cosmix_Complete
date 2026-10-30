/** Human-facing employee code; never derive a number from a UUID. */
export function formatEmployeeCode(value: number | string | null | undefined): string {
  if (typeof value === 'number' && (!Number.isSafeInteger(value) || value <= 0))
    return 'Unavailable';
  const match = /^(?:EMP-)?([0-9]+)$/i.exec(String(value ?? '').trim());
  if (!match?.[1]) return 'Unavailable';
  const digits = match[1].replace(/^0+/, '');
  return digits ? `EMP-${digits.padStart(4, '0')}` : 'Unavailable';
}

export function employeeSearchTerm(value: string): string {
  const text = value.trim();
  const code = /^EMP-([0-9]+)$/i.exec(text);
  return code?.[1] ? code[1].replace(/^0+(?=\d)/, '') : text;
}
