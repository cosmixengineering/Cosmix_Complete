import type { z } from 'zod';
import { useHrAccess } from '../../hooks/use-hr-access';
import { useHrQuery } from '../../hooks/use-hr-queries';
import { masterSchema } from '../../schemas/organization';
import { profileSchema } from '../../schemas/hiring';

export const employmentReferenceFields = [
  'departmentId',
  'designationId',
  'staffCategoryId',
  'employmentTypeId',
  'branchId',
];

export function SubmittedEmploymentSummary({
  profile,
}: {
  profile: z.infer<typeof profileSchema>;
}) {
  const references = [
    { label: 'Department', kind: 'departments', id: profile.departmentId },
    { label: 'Designation', kind: 'designations', id: profile.designationId },
    { label: 'Staff category', kind: 'staff-categories', id: profile.staffCategoryId },
    { label: 'Employment type', kind: 'employment-types', id: profile.employmentTypeId },
    { label: 'Office / branch', kind: 'branches', id: profile.branchId },
  ];
  return (
    <dl className="hr-dl" aria-label="Submitted employment details">
      {references.map((reference) => (
        <div key={reference.kind}>
          <dt>{reference.label}</dt>
          <dd>
            {reference.id ? (
              <OrganizationName kind={reference.kind} id={reference.id} />
            ) : (
              'Not selected'
            )}
          </dd>
        </div>
      ))}
      <div>
        <dt>Joining date</dt>
        <dd>{profile.joinedOn ?? 'Not set'}</dd>
      </div>
    </dl>
  );
}

function OrganizationName({ kind, id }: { kind: string; id: string }) {
  const access = useHrAccess();
  const query = useHrQuery(`${kind}/${id}`, masterSchema, ['hr.organization.read']);
  if (!access.can('hr.organization.read')) return <span>Restricted reference ({id})</span>;
  if (query.isPending) return <span>Loading...</span>;
  if (query.isError || query.data?.id !== id) return <span>Reference unavailable ({id})</span>;
  return (
    <span>
      {query.data.name} ({query.data.code})
    </span>
  );
}
