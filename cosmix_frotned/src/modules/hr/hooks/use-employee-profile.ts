import type { Profile } from '../schemas/hiring';
import { profileResponse } from '../schemas/hiring';
import { employeePage } from '../schemas/contracts';
import { currentUpdateSchema } from '../schemas/updates';
import { ApiError } from '@/services/api/api-error';
import { useHrAccess } from './use-hr-access';
import { useHrQuery } from './use-hr-queries';

export type EmployeeProfileValues = Partial<Omit<Profile, 'identityDocumentType'>> & {
  identityDocumentType?: number | null;
};

// These conflicts describe retained evidence, not a concurrent rowversion change.
export function employeeProfileEvidenceMessage(error: unknown): string | undefined {
  if (!(error instanceof ApiError) || error.status !== 409) return undefined;
  const messages: Record<string, string> = {
    employee_approved_submission_missing:
      'The retained approved hiring submission required to read this section is missing. HR must review the retained onboarding evidence; refreshing cannot recreate it. Available basic details remain accessible.',
    employee_approved_submission_invalid:
      'The retained approved hiring submission failed the server’s consistency checks. HR must review the onboarding evidence before this section can be displayed. Available basic details remain accessible.',
    employee_identity_evidence_unavailable:
      'The retained identity-change evidence is unavailable. HR must review the protected identity history before current identity details can be displayed. Available basic details remain accessible.',
    employee_document_manifest_unavailable:
      'The retained approved document list is unavailable. HR must review the protected document history before this section can be displayed.',
  };
  const message = messages[error.code ?? ''];
  return message
    ? `${message} Code: ${error.code}.${error.correlationId ? ` Reference: ${error.correlationId}` : ''}`
    : undefined;
}

// A missing hiring snapshot does not mean an approved employee is missing.
// Only the explicit lifecycle conflict permits a draft read; never fall back on 404/403.
export function useEmployeeProfile(employeeId: string) {
  const access = useHrAccess();
  const basic = useHrQuery(`employees/${employeeId}/current/basic`, currentUpdateSchema, [
    'hr.employees.read',
  ]);
  const unapproved =
    basic.isError &&
    basic.error instanceof ApiError &&
    basic.error.status === 409 &&
    basic.error.code === 'employee_not_approved';
  const draft = useHrQuery(
    `employees/${employeeId}`,
    profileResponse,
    ['hr.employees.read'],
    unapproved,
  );
  const approved = basic.isSuccess;
  const identity = useHrQuery(
    `employees/${employeeId}/current/identity`,
    currentUpdateSchema,
    ['hr.employees.read', 'hr.employees.identity.read'],
    approved,
  );
  const employment = useHrQuery(
    `employees/${employeeId}/current/employment`,
    currentUpdateSchema,
    ['hr.employees.read'],
    approved,
  );
  // The directory supports name/number search, not an employee-ID filter.
  // Keep this fallback bounded to one page, match the exact ID, and expose only its name.
  // Absence from this page is NOT evidence that the employee does not exist.
  const currentName =
    identity.isSuccess && access.can('hr.employees.read', 'hr.employees.identity.read')
      ? identity.data.current.data.identity?.fullLegalName.trim()
      : undefined;
  const needsDirectoryName =
    approved &&
    (!access.can('hr.employees.identity.read') ||
      (!identity.isFetching && (identity.isSuccess || identity.isError))) &&
    !currentName;
  const directory = useHrQuery(
    'employees?page=1&pageSize=100&includeArchived=true',
    employeePage,
    ['hr.employees.read'],
    needsDirectoryName,
  );
  const directoryName =
    needsDirectoryName && directory.isSuccess && access.can('hr.employees.read')
      ? directory.data.items.find((row) => row.id.toLowerCase() === employeeId.toLowerCase())?.name
      : undefined;
  const profile: EmployeeProfileValues =
    unapproved && draft.isSuccess
      ? draft.data.profile
      : {
          ...(basic.isSuccess ? basic.data.current.data.basic : undefined),
          ...(identity.isSuccess && access.can('hr.employees.read', 'hr.employees.identity.read')
            ? { ...identity.data.current.data.identity, fullLegalName: currentName }
            : undefined),
          ...(employment.isSuccess ? employment.data.current.data.employment : undefined),
        };
  return {
    basic,
    directory,
    directoryName,
    draft,
    identity,
    employment,
    profile,
    approved,
    unapproved,
    query: unapproved ? draft : basic,
    employee: unapproved && draft.isSuccess ? draft.data.employee : undefined,
    refresh: () => {
      void basic.refetch();
      if (unapproved) void draft.refetch();
      if (approved) {
        void employment.refetch();
        if (access.can('hr.employees.identity.read')) void identity.refetch();
      }
    },
  };
}
