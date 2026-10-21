import { z } from 'zod';

const coordinate = z.tuple([z.number().min(-180).max(180), z.number().min(-90).max(90)]);
export const polygonSchema = z
  .object({
    type: z.literal('Polygon'),
    coordinates: z.array(z.array(coordinate).min(4)).min(1),
  })
  .refine(
    (polygon) =>
      polygon.coordinates.every(
        (ring) => ring[0]![0] === ring.at(-1)![0] && ring[0]![1] === ring.at(-1)![1],
      ),
    'Each polygon ring must end at its starting coordinate.',
  );
export type BoundaryPolygon = z.infer<typeof polygonSchema>;

export function readPolygon(text: string): BoundaryPolygon | null {
  if (text.length > 10000) return null;
  try {
    return polygonSchema.parse(JSON.parse(text));
  } catch {
    return null;
  }
}

export function addVertex(polygon: BoundaryPolygon, point: [number, number]): BoundaryPolygon {
  const valid = coordinate.parse(point);
  const ring = polygon.coordinates[0]!;
  return polygonSchema.parse({
    ...polygon,
    coordinates: [[...ring.slice(0, -1), valid, ring[0]!], ...polygon.coordinates.slice(1)],
  });
}

export function removeVertex(polygon: BoundaryPolygon, index: number): BoundaryPolygon {
  const ring = polygon.coordinates[0]!;
  if (ring.length <= 4 || !Number.isInteger(index) || index < 0 || index >= ring.length - 1)
    throw new Error('At least three vertices are required.');
  const points = ring.slice(0, -1).filter((_, i) => i !== index);
  return polygonSchema.parse({
    ...polygon,
    coordinates: [[...points, points[0]!], ...polygon.coordinates.slice(1)],
  });
}
