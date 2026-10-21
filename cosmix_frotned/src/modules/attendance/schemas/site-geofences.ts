import { z } from 'zod';
import { date, id, pageOf, reason, rowVersion } from './common.ts';

const precise = (places: number) =>
  z
    .number()
    .refine(
      (value) => Number(value.toFixed(places)) === value,
      `Use at most ${places} decimal places.`,
    );
export const boundary = z.object({
  id,
  siteId: id,
  versionNumber: z.number().int().positive(),
  effectiveFrom: date,
  latitude: precise(7).min(-90).max(90),
  longitude: precise(7).min(-180).max(180),
  radiusMeters: precise(2).positive().max(99999999.99),
  maximumAccuracyMeters: precise(2).positive().max(99999999.99),
});
export const geofenceConfiguration = z.object({
  siteId: id,
  asOfDate: date,
  status: z.enum(['configured', 'not_configured']),
  siteVersion: rowVersion,
  latestVersionNumber: z.number().int().nonnegative(),
  boundary: boundary.nullable(),
});
export const boundaryPage = pageOf(boundary);
export const createGeofenceSchema = z.strictObject({
  effectiveFrom: date,
  latitude: precise(7).min(-90).max(90),
  longitude: precise(7).min(-180).max(180),
  radiusMeters: precise(2).positive().max(99999999.99),
  maximumAccuracyMeters: precise(2).positive().max(99999999.99),
  expectedSiteVersion: rowVersion,
  expectedLatestVersionNumber: z.number().int().nonnegative(),
  managementApproved: z.literal(true),
  approvalReference: reason(500),
  reason: reason(500),
});
export const geofenceWrite = z.object({
  boundary,
  approvalReference: z.string(),
  managementApproved: z.literal(true),
});
