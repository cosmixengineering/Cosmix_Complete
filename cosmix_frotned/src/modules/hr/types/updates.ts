import type { z } from 'zod';
import type { currentUpdateSchema, updateDataSchema } from '../schemas/updates';
export type CurrentUpdate = z.infer<typeof currentUpdateSchema>;
export type UpdateData = z.infer<typeof updateDataSchema>;
export type SectionName = 'basic' | 'identity' | 'employment' | 'bank' | 'medical' | 'documents';
export const sections: {
  name: SectionName;
  number: number;
  label: string;
  read: string[];
  write: string[];
  dated: boolean;
}[] = [
  {
    name: 'basic',
    number: 1,
    label: 'Basic details',
    read: [],
    write: ['hr.employees.basic.manage'],
    dated: false,
  },
  {
    name: 'identity',
    number: 2,
    label: 'Identity',
    read: ['hr.employees.identity.read'],
    write: [
      'hr.employees.changes.request',
      'hr.employees.identity.request',
      'hr.employees.documents.read',
    ],
    dated: false,
  },
  {
    name: 'employment',
    number: 3,
    label: 'Employment',
    read: [],
    write: ['hr.employees.changes.request'],
    dated: true,
  },
  {
    name: 'bank',
    number: 4,
    label: 'Bank',
    read: ['hr.employees.bank.read'],
    write: ['hr.employees.changes.request', 'hr.employees.bank.request'],
    dated: true,
  },
  {
    name: 'medical',
    number: 5,
    label: 'Medical',
    read: ['hr.employees.medical.read'],
    write: [
      'hr.employees.changes.request',
      'hr.employees.medical.manage',
      'hr.employees.documents.read',
    ],
    dated: true,
  },
  {
    name: 'documents',
    number: 6,
    label: 'Documents',
    read: ['hr.employees.documents.read'],
    write: ['hr.employees.changes.request', 'hr.employees.documents.manage'],
    dated: false,
  },
];
export type Section = (typeof sections)[number];
