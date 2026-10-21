import type { z } from 'zod';
import type { compensationStateSchema, statusStateSchema, termsSchema } from '../schemas/requests';
export type CompensationState = z.infer<typeof compensationStateSchema>;
export type StatusState = z.infer<typeof statusStateSchema>;
export type CompensationTerms = z.infer<typeof termsSchema>;
export type RequestKind = 'compensation' | 'lifecycle';
