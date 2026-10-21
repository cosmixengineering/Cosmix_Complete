import { z } from 'zod';

export const id = z.uuid();
export const date = z.iso.date();
export const timestamp = z.iso.datetime({ offset: true });
export const rowVersion = z.string().regex(/^[A-Za-z0-9+/]{11}=$/);
export const timeline = z.string().regex(/^[A-Fa-f0-9]{64}$/);
export const reason = (max = 1000) =>
  z
    .string()
    .trim()
    .min(1)
    .max(max)
    .refine(
      (value) =>
        ![...value].some((character) => {
          const code = character.charCodeAt(0);
          return code < 32 || (code >= 127 && code <= 159);
        }),
      'Control characters are not allowed.',
    );
export const pageOf = <T extends z.ZodType>(item: T) =>
  z.object({
    items: z.array(item),
    totalCount: z.number().int().nonnegative(),
    page: z.number().int().positive(),
    pageSize: z.number().int().positive().max(100),
  });
export const reference = z.object({
  id,
  code: z.string(),
  name: z.string(),
  isActive: z.boolean(),
  version: z.string().min(1),
});
export const referencePage = pageOf(reference);
export const calendarReference = z.object({
  id,
  code: z.string(),
  name: z.string(),
  effectiveFrom: date,
  effectiveTo: date.nullable(),
  revision: z.number().int(),
  isActive: z.boolean(),
  supersedesCalendarId: id.nullable(),
  version: z.string(),
});
export const calendarPage = pageOf(calendarReference);
export const organizationChange = z.object({
  id,
  operation: z.string(),
  reason: z.string(),
  occurredAt: timestamp,
  actorUserId: z.string(),
  actorRoleCode: z.string(),
  beforeJson: z.string().nullable(),
  afterJson: z.string(),
});
