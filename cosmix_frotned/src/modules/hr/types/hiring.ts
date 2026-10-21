import type { z } from 'zod';
import type {
  addressSchema,
  emergencySchema,
  profileSchema,
  financialSchema,
  medicalSchema,
  documentSchema,
  bankSchema,
} from '../schemas/hiring';
export type Profile = z.infer<typeof profileSchema>;
export type Address = z.infer<typeof addressSchema>;
export type Emergency = z.infer<typeof emergencySchema>;
export type Financial = z.infer<typeof financialSchema>;
export type Medical = z.infer<typeof medicalSchema>;
export type EmployeeDocument = z.infer<typeof documentSchema>;
export type Bank = z.infer<typeof bankSchema>;
export const documentTypes = [
  'Offer',
  'Contract',
  'Identity',
  'Degree',
  'Certificate',
  'License',
  'Signature',
  'Thumb impression',
  'Profile photo',
  'Other',
];
export const wageBases = ['Monthly', 'Daily', 'Hourly'];
export const paymentMethods = ['Bank', 'Cheque', 'Cash', 'Other approved'];
