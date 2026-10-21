import { z } from 'zod';
import { employeeSchema, pageOf } from '../../schemas/contracts';

export type EmployeeArchiveFilterValue = 'current' | 'all' | 'archived';

export const employeeArchivePageSchema = pageOf(employeeSchema.extend({ isArchived: z.boolean() }));

export function employeeArchiveQuery(filter: EmployeeArchiveFilterValue) {
  return { includeArchived: filter === 'all', archivedOnly: filter === 'archived' };
}
