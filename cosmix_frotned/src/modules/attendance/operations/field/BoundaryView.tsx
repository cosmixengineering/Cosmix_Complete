import { z } from 'zod';
import { boundary } from './contracts';

const polygon = z.object({
  type: z.literal('Polygon'),
  coordinates: z.tuple([
    z
      .array(z.tuple([z.number().min(-180).max(180), z.number().min(-85).max(85)]))
      .min(4)
      .max(129),
  ]),
});

export function BoundaryView({ value }: { value: z.infer<typeof boundary> }) {
  if (
    value.kind === 1 &&
    value.latitude !== null &&
    value.longitude !== null &&
    value.radiusMeters !== null &&
    value.radiusMeters > 0 &&
    value.radiusMeters <= 100000 &&
    Math.abs(value.latitude) <= 90 &&
    Math.abs(value.longitude) <= 180 &&
    value.polygonGeoJson === null
  )
    return (
      <figure style={{ margin: 0, maxWidth: 360 }}>
        <svg
          viewBox="0 0 240 180"
          role="img"
          aria-label="Server circle boundary"
          style={{ width: '100%', aspectRatio: '4 / 3' }}
        >
          <circle cx="120" cy="90" r="72" fill="#e3f2ec" stroke="#177c68" strokeWidth="2" />
          <circle cx="120" cy="90" r="3" fill="#252d37" />
        </svg>
        <figcaption>
          Centre {value.latitude}, {value.longitude}; radius {value.radiusMeters} m
        </figcaption>
      </figure>
    );
  if (
    value.kind === 2 &&
    value.polygonGeoJson &&
    value.polygonGeoJson.length <= 32000 &&
    value.latitude === null &&
    value.longitude === null &&
    value.radiusMeters === null
  ) {
    let preview:
      { points: string; left: number; bottom: number; right: number; top: number } | undefined;
    try {
      const ring = polygon.parse(JSON.parse(value.polygonGeoJson)).coordinates[0];
      const xs = ring.map((p) => p[0]),
        ys = ring.map((p) => p[1]);
      const left = Math.min(...xs),
        bottom = Math.min(...ys),
        width = Math.max(...xs) - left,
        height = Math.max(...ys) - bottom;
      const first = ring[0],
        last = ring.at(-1);
      if (
        first?.[0] !== last?.[0] ||
        first?.[1] !== last?.[1] ||
        width <= 0 ||
        height <= 0 ||
        width > 1 ||
        height > 1
      )
        throw new Error('Unsupported boundary');
      const scale = Math.min(200 / width, 140 / height);
      const points = ring
        .map(
          ([x, y]) =>
            `${120 + (x - left - width / 2) * scale},${90 - (y - bottom - height / 2) * scale}`,
        )
        .join(' ');
      preview = { points, left, bottom, right: Math.max(...xs), top: Math.max(...ys) };
    } catch {
      /* Unsupported geometry is never projected as a valid map. */
    }
    if (preview)
      return (
        <figure style={{ margin: 0, maxWidth: 360 }}>
          <svg
            viewBox="0 0 240 180"
            role="img"
            aria-label="Server polygon boundary in longitude and latitude"
            style={{ width: '100%', aspectRatio: '4 / 3' }}
          >
            <polygon points={preview.points} fill="#e3f2ec" stroke="#177c68" strokeWidth="2" />
          </svg>
          <figcaption>
            Longitude {preview.left} to {preview.right}; latitude {preview.bottom} to {preview.top}
          </figcaption>
        </figure>
      );
  }
  return <p role="status">Boundary rendering unavailable for this server geometry.</p>;
}
