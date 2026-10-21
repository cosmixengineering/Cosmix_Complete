import { createGeofenceSchema } from '../schemas/site-geofences';

export function validateGeofence(input: unknown, today: string, latestDate?: string) {
  const parsed = createGeofenceSchema.safeParse(input);
  if (!parsed.success) return parsed.error.issues[0]?.message ?? 'Check the boundary values.';
  if (parsed.data.effectiveFrom <= today || (latestDate && parsed.data.effectiveFrom <= latestDate))
    return 'The boundary must start after today and the latest scheduled version.';
  return null;
}
