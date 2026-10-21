import { z } from 'zod';
import { date, id, pageOf, reason, rowVersion } from './common.ts';

const fields = {
  effectiveFrom: date,
  startTime: z.iso.time(),
  endTime: z.iso.time(),
  endDayOffset: z.number().int().min(0).max(1),
  scheduledMinutes: z.number().int().min(1).max(1440),
  paidBreakMinutes: z.number().int().min(0).max(1440),
  unpaidBreakMinutes: z.number().int().min(0).max(1440),
  applicableWeekdays: z.number().int().min(1).max(127),
  missingCheckoutReminderAfterMinutes: z.number().int().positive().max(2147483647).optional(),
};
export const shiftScheduleSchema = z.strictObject(fields).superRefine((value, ctx) => {
  const seconds = (time: string) => {
    const [hours, minutes, remainder = '0'] = time.split(':');
    return Number(hours) * 3600 + Number(minutes) * 60 + Number(remainder);
  };
  const duration =
    (seconds(value.endTime) - seconds(value.startTime)) / 60 + value.endDayOffset * 1440;
  if (duration <= 0 || duration > 1440)
    ctx.addIssue({
      code: 'custom',
      path: ['endTime'],
      message: 'End must follow start within 24 hours.',
    });
  if (value.scheduledMinutes > duration)
    ctx.addIssue({
      code: 'custom',
      path: ['scheduledMinutes'],
      message: 'Scheduled minutes exceed the shift duration.',
    });
  if (value.paidBreakMinutes + value.unpaidBreakMinutes > duration)
    ctx.addIssue({
      code: 'custom',
      path: ['paidBreakMinutes'],
      message: 'Breaks exceed the shift duration.',
    });
});
export const shiftSummary = z.object({
  id,
  code: z.string(),
  name: z.string(),
  isActive: z.boolean(),
  version: rowVersion,
});
export const shiftVersion = z.object({
  id,
  versionNumber: z.number().int().positive(),
  ...fields,
  effectiveTo: date.nullable(),
});
export const shiftDetail = shiftSummary.extend({ versions: z.array(shiftVersion) });
export const shiftPage = pageOf(shiftSummary);
export const createShiftSchema = z.strictObject({
  code: z.string().regex(/^[A-Za-z0-9][A-Za-z0-9_.-]{0,29}$/),
  name: reason(150),
  initialSchedule: shiftScheduleSchema,
  reason: reason(500),
});
export type Shift = z.infer<typeof shiftDetail>;
export type ShiftSummary = z.infer<typeof shiftSummary>;
export type ShiftSchedule = z.infer<typeof shiftScheduleSchema>;
