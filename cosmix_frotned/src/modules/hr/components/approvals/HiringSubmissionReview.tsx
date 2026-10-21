import { useEffect, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { Check, CornerUpLeft, X, RefreshCw, MapPin, KeyRound, UserRound } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { SelectInput } from '@/components/forms/SelectInput';
import { useHrAccess } from '../../hooks/use-hr-access';
import { useHrAction, useHrQuery } from '../../hooks/use-hr-queries';
import { useDirtyForm } from '../../hooks/use-dirty-form';
import {
  submissionProfileSchema,
  accessSetupSchema,
  type SubmissionSummary,
} from '../../schemas/approvals';
import {
  financialSchema,
  medicalSchema,
  hiringDecisionsSchema,
  documentsSchema,
} from '../../schemas/hiring';
import { employeePage, writeResult } from '../../schemas/contracts';
import { masterSchema } from '../../schemas/organization';
import { onboardingLabels } from '../../types/contracts';
import type { EmployeeWriteResult } from '../../types/contracts';
import { businessDate, resolutionSchema } from '../../staff-taxonomy/contracts';
import { queryString } from '../../api/hr-api';
import { labelDate } from '../../utils/messages';
import { QueryState, ActionMessage, Denied } from '../Feedback';
import { RecordView } from '../RecordView';
import { LongField } from '../hiring/FormFields';
import { ReferenceField } from '../ReferenceField';
import { SubmissionDocuments } from './SubmissionDocuments';
import {
  SubmittedEmploymentSummary,
  employmentReferenceFields,
} from './SubmittedEmploymentSummary';

const permissions = [
  'hr.employees.read',
  'hr.employees.submissions.review',
  'hr.employees.documents.read',
];
const decisions = ['Approve', 'Return for correction', 'Reject'];

export function HiringSubmissionReview({
  submission,
  onSaved,
  onDirty,
  onBusy,
}: {
  submission: SubmissionSummary;
  onSaved: (decision: string, result: EmployeeWriteResult) => void;
  onDirty?: (dirty: boolean) => void;
  onBusy?: (busy: boolean) => void;
}) {
  const access = useHrAccess();
  const location = useLocation();
  const basePath = location.pathname.startsWith('/admin') ? '/admin' : '/hr';
  const [tab, setTab] = useState('profile');
  const [decision, setDecision] = useState(1);
  const [reason, setReason] = useState<string | null>(null);
  const [initialSiteId, setInitialSiteId] = useState('');
  const [reviewed, setReviewed] = useState(false);
  const [error, setError] = useState('');
  const [payload, setPayload] = useState<{
    expectedVersion: string;
    decision: number;
    reason: string | null;
    initialSiteId: string | null;
  } | null>(null);
  const query = useHrQuery(
    `employees/${submission.employeeId}/submissions/${submission.id}`,
    submissionProfileSchema.refine(
      (data) =>
        data.submissionId === submission.id &&
        data.employeeId === submission.employeeId &&
        data.version === submission.version,
    ),
    permissions,
  );
  // This directory read supplies the current employee rowversion without reading a mutable hiring snapshot.
  const current = useHrQuery(
    'employees' + queryString({ search: String(submission.employeeNumber), pageSize: 1 }),
    employeePage,
    ['hr.employees.read'],
  );
  const employee = current.data?.items.find((e) => e.id === submission.employeeId);
  const history = useHrQuery(
    `employees/${submission.employeeId}/decisions`,
    hiringDecisionsSchema,
    ['hr.employees.read'],
  );
  const documents = useHrQuery(
    `employees/${submission.employeeId}/documents`,
    documentsSchema,
    permissions,
  );
  const categoryId = query.data?.profile.staffCategoryId;
  const category = useHrQuery(
    `staff-categories/${categoryId}`,
    masterSchema,
    ['hr.organization.read'],
    !!categoryId,
  );
  const categoryCode = category.data?.code ?? '';
  const today = businessDate();
  const canReadTaxonomy = access.can('hr.staff-taxonomy.read');
  const taxonomy = useHrQuery(
    'staff-taxonomy/effective' + queryString({ categoryCode, on: today }),
    resolutionSchema(categoryCode, today),
    ['hr.staff-taxonomy.read'],
    !!categoryCode,
  );
  const baselineLocation = categoryCode === 'SITE' ? 2 : categoryCode === 'OFFICE' ? 1 : null;
  const workLocation = canReadTaxonomy
    ? taxonomy.data?.available && !taxonomy.isError && !taxonomy.isFetching
      ? taxonomy.data.mapping?.workLocation
      : null
    : baselineLocation;
  const categoryReady =
    access.can('hr.organization.read') &&
    !!category.data &&
    !category.isError &&
    !category.isFetching &&
    category.data.id === categoryId;
  const placementReady = categoryReady && !!workLocation;
  const siteStaff = placementReady && workLocation === 2;
  const siteRequired = siteStaff && taxonomy.data?.mapping?.requireSiteBeforeActivation === true;
  const financial = useHrQuery(
    `employees/${submission.employeeId}/submissions/${submission.id}/financial`,
    financialSchema.nullable(),
    [...permissions, 'hr.employees.financial.read'],
    tab === 'financial',
  );
  const medical = useHrQuery(
    `employees/${submission.employeeId}/submissions/${submission.id}/medical`,
    medicalSchema.nullable(),
    [...permissions, 'hr.employees.medical.read'],
    tab === 'medical',
  );
  const setup = useHrQuery(
    `employees/${submission.employeeId}/access-setup`,
    accessSetupSchema.refine(
      (data) =>
        data.employeeId === submission.employeeId &&
        (!data.account || data.account.employeeId === submission.employeeId),
    ),
    ['hr.employees.submissions.review', 'access.accounts.read'],
    employee?.onboardingStatus === 2,
  );
  const action = useHrAction();
  const dirty = !!reason || !!initialSiteId || reviewed || decision !== 1 || !!payload;
  useDirtyForm(dirty || action.pending);
  useEffect(() => {
    onDirty?.(dirty);
  }, [dirty, onDirty]);
  useEffect(() => {
    onBusy?.(action.pending);
  }, [action.pending, onBusy]);
  if (!access.can(...permissions)) return <Denied />;
  const self =
    submission.submittedBy === access.user?.account?.accountId ||
    submission.employeeId === access.user?.account?.employeeId;
  const pending =
    submission.status === 1 && employee?.onboardingStatus === 1 && employee.status === 1;
  const documentsReady =
    !!query.data &&
    !query.isError &&
    !query.isFetching &&
    !documents.isFetching &&
    !documents.isError &&
    !!documents.data &&
    query.data.documents.every((d) => {
      const live = documents.data?.find((r) => r.id === d.id);
      return d.isAvailable && live?.isAvailable && live.version === d.version;
    });
  const canApprove =
    documentsReady &&
    placementReady &&
    (!siteRequired || !!initialSiteId) &&
    !current.isFetching &&
    !current.isError;
  const tabs = [
    { key: 'profile', label: 'Submitted profile' },
    { key: 'documents', label: 'Documents' },
    ...(access.can('hr.employees.financial.read')
      ? [{ key: 'financial', label: 'Financial' }]
      : []),
    ...(access.can('hr.employees.medical.read') ? [{ key: 'medical', label: 'Medical' }] : []),
    { key: 'history', label: 'Decisions' },
  ];
  return (
    <>
      <p className="muted">
        Submission revision {submission.version} &middot; {labelDate(submission.submittedAt)}
      </p>
      {query.data && !query.isError && <SubmittedEmploymentSummary profile={query.data.profile} />}
      <nav className="hr-tabs" aria-label="Submission review sections">
        {tabs.map((t) => (
          <button
            key={t.key}
            type="button"
            className={tab === t.key ? 'active' : ''}
            onClick={() => setTab(t.key)}
          >
            {t.label}
          </button>
        ))}
      </nav>
      <QueryState query={query}>
        {query.data && (
          <>
            {tab === 'profile' && (
              <RecordView
                value={query.data.profile}
                omit={[...employmentReferenceFields, 'joinedOn']}
              />
            )}
            {tab === 'documents' && (
              <SubmissionDocuments
                employeeId={submission.employeeId}
                documents={query.data.documents}
              />
            )}
            {tab === 'financial' && (
              <QueryState query={financial}>
                {financial.data ? (
                  <RecordView value={financial.data} />
                ) : (
                  <p className="muted">No financial section was submitted.</p>
                )}
              </QueryState>
            )}
            {tab === 'medical' && (
              <QueryState query={medical}>
                {medical.data ? (
                  <>
                    <RecordView value={medical.data} />
                    {medical.data.supportingDocumentId && (
                      <SubmissionDocuments
                        employeeId={submission.employeeId}
                        medical
                        documentIds={[medical.data.supportingDocumentId]}
                      />
                    )}
                  </>
                ) : (
                  <p className="muted">No medical section was submitted.</p>
                )}
              </QueryState>
            )}
            {tab === 'history' && (
              <QueryState query={history}>
                {!history.data?.length && <p className="muted">No decisions recorded.</p>}
                {history.data?.map((d) => (
                  <article className="hr-repeat" key={d.id}>
                    <strong>{decisions[d.decision - 1]}</strong>
                    <p>{d.reason}</p>
                    <p className="muted">
                      {labelDate(d.decidedAt)} &middot; {d.actorAccountId}
                    </p>
                  </article>
                ))}
              </QueryState>
            )}
          </>
        )}
      </QueryState>
      {query.isError && (
        <p className="hr-notice">
          The retained submission could not be loaded. No hiring decision can be recorded from
          missing evidence. Current employee details and access setup are separate from the original
          submission.
        </p>
      )}
      <QueryState query={current}>
        {!employee && current.data && (
          <p role="alert">The employee record is no longer available.</p>
        )}
        {employee && !pending && (
          <p className="hr-notice">
            Current hiring state: {onboardingLabels[employee.onboardingStatus]}.
          </p>
        )}
        {employee?.onboardingStatus === 2 && (
          <div className="hr-actions" aria-label="Approved employee setup">
            <Link
              className="button button--secondary"
              to={`${basePath}/employees/${submission.employeeId}`}
            >
              <UserRound size={16} /> Employee details
            </Link>
            {siteStaff && access.can('hr.sites.read') && (
              <Link
                className="button button--secondary"
                to={`${basePath}/employees/${submission.employeeId}?section=assignments`}
              >
                <MapPin size={16} /> Site assignment
              </Link>
            )}
            {access.can('access.accounts.read') && (
              <Link
                className="button button--secondary"
                to={`${basePath}/employees/${submission.employeeId}?section=access`}
              >
                <KeyRound size={16} /> Roles &amp; software access
              </Link>
            )}
          </div>
        )}
        {employee?.onboardingStatus === 2 && access.can('access.accounts.read') && (
          <QueryState query={setup}>
            {setup.data && (
              <section className="hr-form-section">
                <h3>Access setup</h3>
                <dl className="hr-dl">
                  <div>
                    <dt>Account state</dt>
                    <dd>
                      {setup.data.account
                        ? [
                            'Pending access',
                            'Ready for invitation',
                            'Invited',
                            'Active',
                            'Suspended',
                            'Disabled',
                          ][setup.data.account.status]
                        : 'No account'}
                    </dd>
                  </div>
                  <div>
                    <dt>Initial access assigned</dt>
                    <dd>{setup.data.initialAccessAssigned ? 'Yes' : 'No'}</dd>
                  </div>
                  <div>
                    <dt>Mobile phone enrolled</dt>
                    <dd>{setup.data.mobilePhoneEnrolled ? 'Yes' : 'No'}</dd>
                  </div>
                  <div>
                    <dt>Web access enabled</dt>
                    <dd>{setup.data.account?.webAccessEnabled ? 'Yes' : 'No'}</dd>
                  </div>
                </dl>
              </section>
            )}
          </QueryState>
        )}
      </QueryState>
      {pending && self && (
        <p className="hr-notice">You cannot decide your own submission or employment.</p>
      )}
      {pending && !self && query.data && (
        <form
          className="hr-form-section"
          onSubmit={(e) => {
            e.preventDefault();
            setError('');
            if (!employee || !reviewed || action.pending || (decision !== 1 && !reason?.trim()))
              return;
            if (decision === 1 && !canApprove) {
              setError(
                'Review current documents, staff classification and required site assignment before approval.',
              );
              return;
            }
            if (
              query.data?.submissionId !== submission.id ||
              query.data.employeeId !== submission.employeeId
            )
              return;
            if (
              reason &&
              [...reason].some(
                (c) => c.charCodeAt(0) < 32 || (c.charCodeAt(0) >= 127 && c.charCodeAt(0) <= 159),
              )
            ) {
              setError('Use a single-line reason without control characters.');
              return;
            }
            if (!window.confirm(`${decisions[decision - 1]} this hiring submission?`)) return;
            const body = payload ?? {
              expectedVersion: employee.version,
              decision,
              reason: reason?.trim() || null,
              initialSiteId: decision === 1 && siteStaff ? initialSiteId || null : null,
            };
            setPayload(body);
            void action
              .run(
                `employees/${submission.employeeId}/submissions/${submission.id}/decision`,
                body,
                'POST',
                writeResult.refine(
                  (result) =>
                    result.employeeId === submission.employeeId &&
                    result.submissionId === submission.id,
                ),
                () => setPayload(null),
              )
              .then((result) => {
                if (result) {
                  setReason(null);
                  setReviewed(false);
                  setInitialSiteId('');
                  setPayload(null);
                  setDecision(1);
                  onSaved(decisions[decision - 1] ?? 'Recorded', result);
                  void current.refetch();
                  void history.refetch();
                }
              });
          }}
        >
          <h3>Hiring decision</h3>
          <fieldset disabled={action.pending}>
            <fieldset disabled={!!payload}>
              <SelectInput
                label="Hiring decision"
                value={decision}
                options={decisions.map((label, i) => ({ value: String(i + 1), label }))}
                onChange={(e) => {
                  setDecision(Number(e.target.value));
                  setInitialSiteId('');
                }}
              />
              <LongField
                label="Hiring decision reason"
                max={2000}
                required={decision !== 1}
                value={reason}
                onChange={setReason}
              />
              {decision === 1 && siteStaff && access.can('hr.sites.read') && (
                <ReferenceField
                  label="Initial site assignment"
                  kind="sites"
                  required={siteRequired}
                  enableFind
                  value={initialSiteId || null}
                  onChange={(id) => setInitialSiteId(id ?? '')}
                />
              )}
              {decision === 1 && siteStaff && !access.can('hr.sites.read') && (
                <p className="hr-notice">
                  Site selection requires site-read permission.{' '}
                  {siteRequired
                    ? 'This staff category requires a site before approval.'
                    : 'Site assignment remains pending until an authorized Admin assigns it.'}
                </p>
              )}
              {decision === 1 && placementReady && workLocation === 1 && (
                <p className="hr-notice">
                  Office placement: the submitted office / branch. Roles and software access are
                  assigned separately after hiring approval.
                </p>
              )}
              <label className="hr-check">
                <input
                  type="checkbox"
                  checked={reviewed}
                  required
                  onChange={(e) => setReviewed(e.target.checked)}
                />
                I have reviewed this submission and its supporting evidence.
              </label>
            </fieldset>
            {!documentsReady && decision === 1 && (
              <p role="alert">Submitted documents are unavailable. Approval cannot proceed.</p>
            )}
            {!placementReady && decision === 1 && (
              <p role="alert">
                Staff classification is unavailable. Organization access and an effective category
                mapping are required before choosing the placement.
              </p>
            )}
            {siteRequired && !initialSiteId && decision === 1 && (
              <p role="alert">Select an initial site required by this staff category.</p>
            )}
            <div className="hr-actions">
              <Button type="submit" disabled={!reviewed || (decision === 1 && !canApprove)}>
                {decision === 1 ? (
                  <Check size={16} />
                ) : decision === 2 ? (
                  <CornerUpLeft size={16} />
                ) : (
                  <X size={16} />
                )}
                {payload && action.error ? 'Retry hiring decision' : 'Record hiring decision'}
              </Button>
            </div>
          </fieldset>
          {error && (
            <p role="alert" className="hr-message hr-message--error">
              {error}
            </p>
          )}
          {action.errorStatus !== null && action.errorStatus >= 400 && action.errorStatus < 500 && (
            <Button
              variant="secondary"
              type="button"
              disabled={action.pending}
              onClick={() => {
                setPayload(null);
                void current.refetch();
                void documents.refetch();
                void query.refetch();
                void category.refetch();
                if (canReadTaxonomy && categoryCode) void taxonomy.refetch();
              }}
            >
              <RefreshCw size={16} />
              Refresh review evidence
            </Button>
          )}
        </form>
      )}
      <ActionMessage action={action} />
    </>
  );
}
