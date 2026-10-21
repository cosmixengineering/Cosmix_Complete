import { z } from 'zod';
import { date, reason, timeline } from '../schemas/common';
import { temporaryPreview } from '../schemas/site-rosters';

export function validateInterval(from: string, to: string | null, today: string) {
  if (!date.safeParse(from).success || (to && !date.safeParse(to).success))
    return 'Enter valid business dates.';
  if (from <= today) return 'The change must start after today in Asia/Karachi.';
  if (to && to <= from) return 'The exclusive end must follow the start date.';
  return null;
}
export function previewMatches(
  preview: z.infer<typeof temporaryPreview>,
  siteId: string,
  businessDate: string,
  replacementShiftVersionId: string,
  currentTimeline: string,
) {
  return (
    preview.siteId === siteId &&
    preview.businessDate === businessDate &&
    preview.replacementShiftVersionId === replacementShiftVersionId &&
    preview.timeline === currentTimeline
  );
}
export function temporaryBody(
  preview: z.infer<typeof temporaryPreview>,
  writtenEvidence: string,
  inputReason: string,
) {
  return {
    businessDate: preview.businessDate,
    replacementShiftVersionId: preview.replacementShiftVersionId,
    ...amendmentBody(preview, writtenEvidence, inputReason),
  };
}
export function amendmentBody(
  preview: { timeline: string; staffToken: string },
  writtenEvidence: string,
  inputReason: string,
) {
  return {
    expectedTimeline: timeline.parse(preview.timeline),
    expectedStaff: timeline.parse(preview.staffToken),
    writtenEvidence: reason(500).parse(writtenEvidence),
    reason: reason().parse(inputReason),
  };
}
export function freezeBody<T>(body: T): T {
  const copy = structuredClone(body);
  function freeze(value: unknown) {
    if (value && typeof value === 'object') {
      Object.values(value).forEach(freeze);
      Object.freeze(value);
    }
  }
  freeze(copy);
  return copy;
}
