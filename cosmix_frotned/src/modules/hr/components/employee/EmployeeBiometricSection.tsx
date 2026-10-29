import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Fingerprint, RefreshCw } from 'lucide-react';
import { z } from 'zod';
import { Button } from '@/components/ui/Button';
import { SelectInput } from '@/components/forms/SelectInput';
import { Pagination } from '@/components/data-display/Pagination';
import { hrGet } from '../../api/hr-api';
import { useHrAccess } from '../../hooks/use-hr-access';
import { id, pageOf, time, version } from '../../schemas/contracts';
import { QueryState, Denied } from '../Feedback';
import { labelDate } from '../../utils/messages';
import '@/styles/biometrics.css';

export interface EmployeeBiometricSectionProps {
  employeeId: string;
  compact?: boolean;
}

const enrollmentSchema = z.object({
  id,
  employeeId: id,
  provider: z.string(),
  providerVersion: z.string(),
  enrolledAtUtc: time,
  isActive: z.boolean(),
  revokedAtUtc: time.nullable(),
  replacesEnrollmentId: id.nullable(),
  version,
});
const attemptSchema = z.object({
  id,
  enrollmentId: id,
  outcome: z.string(),
  issuedAtUtc: time,
  expiresAtUtc: time,
  completedAtUtc: time.nullable(),
  consumedAtUtc: time.nullable(),
  provider: z.string(),
  providerVersion: z.string(),
  reasonCode: z.string().nullable(),
});

export function EmployeeBiometricSection({
  employeeId,
  compact = false,
}: EmployeeBiometricSectionProps) {
  const access = useHrAccess();
  const authority = (access.user?.grants ?? [])
    .map((grant) =>
      [grant.roleAssignmentId, grant.permission, grant.scopeKind, grant.targetId].join(':'),
    )
    .sort()
    .join('|');
  if (!id.safeParse(employeeId).success) return <p role="alert">Invalid employee reference.</p>;
  if (!access.ready) return <Denied />;
  const subject = `${access.subject}:${authority}`;
  return (
    <BiometricRecords
      key={`${subject}:${employeeId}`}
      employeeId={employeeId}
      compact={compact}
      subject={subject}
    />
  );
}

function BiometricRecords({
  employeeId,
  compact,
  subject,
}: {
  employeeId: string;
  compact: boolean;
  subject: string;
}) {
  const access = useHrAccess();
  const [siteId, setSiteId] = useState('');
  const grants = (access.user?.grants ?? []).filter(
    (grant) => grant.permission === 'attendance.face.read',
  );
  const direct = grants.some(
    (grant) =>
      (grant.scopeKind === 7 && grant.targetId === null) ||
      (grant.scopeKind === 2 && grant.targetId === employeeId),
  );
  const sites = [
    ...new Set(
      grants
        .filter((grant) => grant.scopeKind === 5 && id.safeParse(grant.targetId).success)
        .map((grant) => grant.targetId!),
    ),
  ];
  const allowed = access.ready && (direct || sites.includes(siteId));

  return (
    <section className="bio-card" aria-label="Employee face enrollment">
      <div className="bio-header">
        <div className="bio-title-group">
          <Fingerprint size={18} aria-hidden="true" />
          <h2 className="bio-title">Face enrollment</h2>
        </div>
      </div>
      {!direct && sites.length > 0 && (
        <SelectInput
          label="Face-read site scope"
          value={siteId}
          options={[
            { value: '', label: 'Select granted site scope' },
            ...sites.map((site) => ({ value: site, label: site })),
          ]}
          onChange={(event) => setSiteId(event.target.value)}
        />
      )}
      {!allowed ? (
        <p className="hr-notice" role="status">
          Face records require matching employee, site or All-scoped face-read access.
        </p>
      ) : (
        <div key={siteId}>
          <EnrollmentHistory
            employeeId={employeeId}
            subject={subject}
            scope={siteId}
            allowed={allowed}
          />
          {!compact && (
            <AttemptHistory
              employeeId={employeeId}
              subject={subject}
              scope={siteId}
              allowed={allowed}
            />
          )}
        </div>
      )}
      <p className="hr-notice" role="status">
        Use <Link to="/hr/biometrics">Biometric Registration</Link> for camera enrollment. Active
        enrollment metadata does not confirm check-in eligibility or a successful attendance punch.
      </p>
    </section>
  );
}

function useFacePage<T extends z.ZodType>(
  employeeId: string,
  subject: string,
  scope: string,
  suffix: string,
  schema: T,
  allowed: boolean,
) {
  return useQuery({
    queryKey: ['hr', subject, 'face', employeeId, scope, suffix],
    queryFn: ({ signal }) =>
      hrGet(`employees/${employeeId}/face-enrollments${suffix}`, schema, signal),
    enabled: allowed,
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });
}

function EnrollmentHistory({
  employeeId,
  subject,
  scope,
  allowed,
}: {
  employeeId: string;
  subject: string;
  scope: string;
  allowed: boolean;
}) {
  const [page, setPage] = useState(1);
  const schema = pageOf(enrollmentSchema).refine(
    (value) => value.items.every((item) => item.employeeId === employeeId),
    'Enrollment employee does not match the requested employee.',
  );
  const query = useFacePage(
    employeeId,
    subject,
    scope,
    `?page=${page}&pageSize=10`,
    schema,
    allowed,
  );
  return (
    <section aria-label="Enrollment history">
      <Button
        variant="secondary"
        disabled={!allowed || query.isFetching}
        onClick={() => {
          if (allowed) void query.refetch();
        }}
      >
        <RefreshCw size={14} aria-hidden="true" /> Reload enrollments
      </Button>
      <QueryState query={query}>
        {query.isFetching ? (
          <p role="status">Refreshing enrollment records...</p>
        ) : query.data ? (
          <>
            {query.data.items.length === 0 && <p>No enrollment records returned.</p>}
            <div className="hr-table-scroll">
              <table className="bio-history-table">
                <thead>
                  <tr>
                    <th>Enrollment</th>
                    <th>Enrolled (Karachi)</th>
                    <th>Provider</th>
                    <th>Recorded state</th>
                    <th>Revoked (Karachi)</th>
                  </tr>
                </thead>
                <tbody>
                  {query.data.items.map((entry) => (
                    <tr key={entry.id}>
                      <td>{entry.id}</td>
                      <td>{labelDate(entry.enrolledAtUtc)}</td>
                      <td>
                        {entry.provider} / {entry.providerVersion}
                      </td>
                      <td>{entry.isActive ? 'Active' : 'Inactive'}</td>
                      <td>{entry.revokedAtUtc ? labelDate(entry.revokedAtUtc) : 'Not recorded'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination
              page={page}
              pageSize={10}
              total={query.data.totalCount}
              onChange={setPage}
            />
          </>
        ) : null}
      </QueryState>
    </section>
  );
}

function AttemptHistory({
  employeeId,
  subject,
  scope,
  allowed,
}: {
  employeeId: string;
  subject: string;
  scope: string;
  allowed: boolean;
}) {
  const [page, setPage] = useState(1);
  const query = useFacePage(
    employeeId,
    subject,
    scope,
    `/attempts?page=${page}&pageSize=10`,
    pageOf(attemptSchema),
    allowed,
  );
  return (
    <section aria-label="Face verification attempts">
      <h3>Face verification attempts</h3>
      <Button
        variant="secondary"
        disabled={!allowed || query.isFetching}
        onClick={() => {
          if (allowed) void query.refetch();
        }}
      >
        <RefreshCw size={14} aria-hidden="true" /> Reload attempts
      </Button>
      <QueryState query={query}>
        {query.isFetching ? (
          <p role="status">Refreshing verification attempts...</p>
        ) : query.data ? (
          <>
            {query.data.items.length === 0 && <p>No verification attempts returned.</p>}
            <div className="hr-table-scroll">
              <table className="bio-history-table">
                <thead>
                  <tr>
                    <th>Issued (Karachi)</th>
                    <th>Enrollment</th>
                    <th>Provider</th>
                    <th>Outcome</th>
                    <th>Reason</th>
                    <th>Consumed (Karachi)</th>
                  </tr>
                </thead>
                <tbody>
                  {query.data.items.map((entry) => (
                    <tr key={entry.id}>
                      <td>{labelDate(entry.issuedAtUtc)}</td>
                      <td>{entry.enrollmentId}</td>
                      <td>
                        {entry.provider} / {entry.providerVersion}
                      </td>
                      <td>{entry.outcome}</td>
                      <td>{entry.reasonCode ?? 'Not recorded'}</td>
                      <td>
                        {entry.consumedAtUtc ? labelDate(entry.consumedAtUtc) : 'Not recorded'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination
              page={page}
              pageSize={10}
              total={query.data.totalCount}
              onChange={setPage}
            />
          </>
        ) : null}
      </QueryState>
    </section>
  );
}
