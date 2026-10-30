const SECTION_NAMES = new Set(['basic', 'identity', 'employment', 'bank', 'medical', 'documents']);

/** Evidence comes from validated JSON, but nested values can still be null or scalar. */
export function isEvidenceRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

export function evidenceRecord(value: unknown): Record<string, unknown> {
  return isEvidenceRecord(value) ? value : {};
}

export function sectionEvidence(value: unknown, section: string): Record<string, unknown> {
  const record = evidenceRecord(value);
  if (isEvidenceRecord(record[section])) {
    return record[section];
  }

  // If this record contains other section wrapper keys (e.g. 'basic', 'identity', etc.),
  // then it is a full employee profile / update data wrapper.
  // If the target section is null, missing, or empty in this wrapper,
  // return {} so other sections are never leaked as diff fields.
  const hasOtherSectionKey = Object.keys(record).some((k) => k !== section && SECTION_NAMES.has(k));
  if (hasOtherSectionKey) {
    return {};
  }

  return record;
}
