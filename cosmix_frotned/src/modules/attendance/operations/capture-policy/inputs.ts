import {
  canonicalSettings,
  historySchema,
  policySchema,
  previewSchema,
  publicationInput,
  utcTimestamp,
  utcTicks,
  type CaptureHistory,
  type CapturePolicy,
  type CapturePreview,
  type CaptureSettings,
} from './contracts';
import { formatLimit, parseLimit } from './decimals';
export { parseLimit } from './decimals';

export const capturePolicyEndpoint = '/attendance/capture-validation/policy';
export type GroupMode = '' | 'enabled' | 'disabled';
export type MappingDraft = {
  entryId: string;
  observationCode: string;
  riskKind: '' | 'device_integrity' | 'mock_location';
};
export type CaptureDraft = {
  duplicate: { mode: GroupMode; toleranceSeconds: string };
  activeDuration: { mode: GroupMode; maximumMinutes: string };
  travel: {
    mode: GroupMode;
    lookbackSeconds: string;
    maximumMetersPerSecond: string;
    maximumAccuracyMeters: string;
  };
  device: { mode: GroupMode; approvedCodeSetReference: string; riskMappings: MappingDraft[] };
};
export function blankDraft(): CaptureDraft {
  return {
    duplicate: { mode: '', toleranceSeconds: '' },
    activeDuration: { mode: '', maximumMinutes: '' },
    travel: {
      mode: '',
      lookbackSeconds: '',
      maximumMetersPerSecond: '',
      maximumAccuracyMeters: '',
    },
    device: { mode: '', approvedCodeSetReference: '', riskMappings: [] },
  };
}
export function changeMode(
  draft: CaptureDraft,
  group: keyof CaptureDraft,
  mode: GroupMode,
): CaptureDraft {
  return {
    ...draft,
    [group]: { ...(mode === 'enabled' ? draft[group] : blankDraft()[group]), mode },
  };
}
export function settingsFromDraft(draft: CaptureDraft): CaptureSettings {
  function enabled(mode: GroupMode) {
    if (!mode) throw new Error('Choose Enabled or Disabled for every capture rule group.');
    return mode === 'enabled';
  }
  const duplicate = enabled(draft.duplicate.mode);
  const activeDuration = enabled(draft.activeDuration.mode);
  const travel = enabled(draft.travel.mode);
  const device = enabled(draft.device.mode);
  return canonicalSettings({
    duplicate: {
      enabled: duplicate,
      toleranceSeconds: duplicate ? parseLimit(draft.duplicate.toleranceSeconds) : null,
    },
    activeDuration: {
      enabled: activeDuration,
      maximumMinutes: activeDuration ? parseLimit(draft.activeDuration.maximumMinutes) : null,
    },
    travel: {
      enabled: travel,
      lookbackSeconds: travel ? parseLimit(draft.travel.lookbackSeconds) : null,
      maximumMetersPerSecond: travel ? parseLimit(draft.travel.maximumMetersPerSecond) : null,
      maximumAccuracyMeters: travel ? parseLimit(draft.travel.maximumAccuracyMeters) : null,
    },
    device: {
      enabled: device,
      approvedCodeSetReference: device ? draft.device.approvedCodeSetReference : null,
      riskMappings: device
        ? draft.device.riskMappings.map(({ observationCode, riskKind }) => ({
            observationCode,
            riskKind: riskKind as 'device_integrity' | 'mock_location',
          }))
        : null,
    },
  });
}
export function draftFromSettings(settings: CaptureSettings): CaptureDraft {
  const checked = canonicalSettings(settings);
  const mode = (value: boolean): GroupMode => (value ? 'enabled' : 'disabled');
  const value = (number: number | null) => (number === null ? '' : formatLimit(number));
  return {
    duplicate: {
      mode: mode(checked.duplicate.enabled),
      toleranceSeconds: value(checked.duplicate.toleranceSeconds),
    },
    activeDuration: {
      mode: mode(checked.activeDuration.enabled),
      maximumMinutes: value(checked.activeDuration.maximumMinutes),
    },
    travel: {
      mode: mode(checked.travel.enabled),
      lookbackSeconds: value(checked.travel.lookbackSeconds),
      maximumMetersPerSecond: value(checked.travel.maximumMetersPerSecond),
      maximumAccuracyMeters: value(checked.travel.maximumAccuracyMeters),
    },
    device: {
      mode: mode(checked.device.enabled),
      approvedCodeSetReference: checked.device.approvedCodeSetReference ?? '',
      riskMappings: (checked.device.riskMappings ?? []).map((row) => ({
        ...row,
        entryId: crypto.randomUUID(),
      })),
    },
  };
}
export function utcFromInput(value: string) {
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2})?$/.test(value))
    throw new Error('Enter a valid date and time in UTC.');
  return utcTimestamp.parse(value + (value.length === 16 ? ':00Z' : 'Z'));
}
export type PublicationSource = { preview: CapturePreview; latestVersion: CapturePolicy | null };
function policySignature(row: CapturePolicy) {
  const checked = policySchema.parse(row);
  return JSON.stringify({
    ...checked,
    id: checked.id.toLowerCase(),
    supersedesVersionId: checked.supersedesVersionId?.toLowerCase() ?? null,
    publishedBy: checked.publishedBy.toLowerCase(),
    sourceFingerprint: checked.sourceFingerprint.toUpperCase(),
    effectiveFromUtc: String(utcTicks(checked.effectiveFromUtc)),
    publishedAtUtc: String(utcTicks(checked.publishedAtUtc)),
    settings: canonicalSettings(checked.settings),
  });
}
export function reviewSource(
  preview: CapturePreview,
  history: CaptureHistory,
  visibleHistory?: CaptureHistory,
): PublicationSource {
  const checkedPreview = previewSchema(preview.asOfUtc).parse(preview);
  const checkedHistory = historySchema(1).parse(history);
  if (checkedPreview.latestRevision !== checkedHistory.totalCount)
    throw new Error('The policy timeline changed between reads. Reload and review it again.');
  if (
    visibleHistory &&
    historySchema(visibleHistory.page, visibleHistory.pageSize).parse(visibleHistory).totalCount !==
      checkedHistory.totalCount
  )
    throw new Error('The policy timeline changed between reads. Reload and review it again.');
  const effective = checkedPreview.effective;
  const matching =
    effective && checkedHistory.items.find((row) => row.revision === effective.revision);
  if (effective && matching && policySignature(effective) !== policySignature(matching))
    throw new Error(
      'The effective configuration does not match immutable history. Reload the policy.',
    );
  return { preview: checkedPreview, latestVersion: checkedHistory.items[0] ?? null };
}
export function buildPublication(
  draft: CaptureDraft,
  source: PublicationSource,
  fields: { effectiveFromUtc: string; approvalReference: string; reason: string },
  now = Date.now(),
) {
  const request = publicationInput.parse({
    ...fields,
    expectedRevision: source.preview.latestRevision,
    expectedTimelineFingerprint: source.preview.timelineFingerprint,
    settings: settingsFromDraft(draft),
  });
  if (request.expectedRevision >= 512)
    throw new Error('The server policy history limit has been reached.');
  if (utcTicks(request.effectiveFromUtc) <= BigInt(now) * 10000n)
    throw new Error(
      'The effective UTC time must be in the future; the server clock is authoritative.',
    );
  if (
    source.latestVersion &&
    utcTicks(request.effectiveFromUtc) <= utcTicks(source.latestVersion.effectiveFromUtc)
  )
    throw new Error(
      'The effective UTC time must follow the latest published version, including scheduled versions.',
    );
  if (new TextEncoder().encode(JSON.stringify(request)).byteLength > 32768)
    throw new Error('The publication exceeds the server request-size limit.');
  return request;
}
