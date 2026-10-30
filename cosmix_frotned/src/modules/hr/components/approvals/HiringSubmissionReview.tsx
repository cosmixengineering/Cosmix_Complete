import { useEffect, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import type { z } from 'zod';
import {
  Check,
  CornerUpLeft,
  X,
  RefreshCw,
  MapPin,
  KeyRound,
  UserRound,
  CheckCircle2,
  FileText,
  CreditCard,
  HeartPulse,
  History,
  ShieldCheck,
  Briefcase,
  AlertCircle,
  Phone,
  Home,
  UserCheck,
} from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { useHrAccess } from '../../hooks/use-hr-access';
import { useHrAction, useHrQuery } from '../../hooks/use-hr-queries';
import { useDirtyForm } from '../../hooks/use-dirty-form';
import {
  submissionProfileSchema,
  accessSetupSchema,
  type SubmissionSummary,
} from '../../schemas/approvals';
import {
  profileSchema,
  financialSchema,
  medicalSchema,
  hiringDecisionsSchema,
  documentsSchema,
} from '../../schemas/hiring';
import { employeePage, writeResult } from '../../schemas/contracts';
import { masterSchema } from '../../schemas/organization';
import type { EmployeeWriteResult } from '../../types/contracts';
import { businessDate, resolutionSchema } from '../../staff-taxonomy/contracts';
import { queryString } from '../../api/hr-api';
import { labelDate } from '../../utils/messages';
import { QueryState, ActionMessage, Denied } from '../Feedback';
import { ReferenceField } from '../ReferenceField';
import { SubmissionDocuments } from './SubmissionDocuments';
import { SubmittedEmploymentSummary } from './SubmittedEmploymentSummary';
import './hiring-review.css';

type Profile = z.infer<typeof profileSchema>;
type FinancialSection = NonNullable<z.infer<typeof financialSchema>>;
type MedicalSection = NonNullable<z.infer<typeof medicalSchema>>;

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
    { key: 'profile', label: 'Profile Details', icon: <UserRound size={13} /> },
    {
      key: 'documents',
      label: 'Documents',
      icon: <FileText size={13} />,
      badge: query.data?.documents.length ?? 0,
    },
    ...(access.can('hr.employees.financial.read')
      ? [{ key: 'financial', label: 'Financial', icon: <CreditCard size={13} /> }]
      : []),
    ...(access.can('hr.employees.medical.read')
      ? [{ key: 'medical', label: 'Medical', icon: <HeartPulse size={13} /> }]
      : []),
    {
      key: 'history',
      label: 'Decisions',
      icon: <History size={13} />,
      badge: history.data?.length ?? 0,
    },
  ];

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError('');
    if (!employee || !reviewed || action.pending || (decision !== 1 && !reason?.trim())) return;
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
            result.employeeId === submission.employeeId && result.submissionId === submission.id,
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
  }

  return (
    <div className="hiring-horizontal-layout">
      {/* ======================================================================
          LEFT COLUMN: Employee Profile & Submitted Evidence (Scrollable)
          ====================================================================== */}
      <div className="hiring-evidence-pane">
        {/* Top Employment Structure Banner */}
        {query.data && !query.isError && (
          <div className="hiring-emp-summary-strip">
            <SubmittedEmploymentSummary profile={query.data.profile} />
          </div>
        )}

        {/* Tab Navigation Strip */}
        <div
          className="hiring-evidence-tabs"
          role="tablist"
          aria-label="Submission review sections"
        >
          {tabs.map((t) => (
            <button
              key={t.key}
              type="button"
              role="tab"
              aria-selected={tab === t.key}
              className={`hiring-tab-btn ${tab === t.key ? 'active' : ''}`}
              onClick={() => setTab(t.key)}
            >
              {t.icon}
              <span>{t.label}</span>
              {t.badge !== undefined && t.badge > 0 && (
                <span className="hiring-pill-badge" style={{ fontSize: '9px', padding: '0 4px' }}>
                  {t.badge}
                </span>
              )}
            </button>
          ))}
        </div>

        {/* Main Tab Content */}
        <QueryState query={query}>
          {query.data && (
            <>
              {tab === 'profile' && <ProfileEvidenceView profile={query.data.profile} />}

              {tab === 'documents' && (
                <SubmissionDocuments
                  employeeId={submission.employeeId}
                  documents={query.data.documents}
                />
              )}

              {tab === 'financial' && (
                <QueryState query={financial}>
                  {financial.data ? (
                    <FinancialEvidenceView data={financial.data} />
                  ) : (
                    <p className="muted" style={{ padding: '16px 0' }}>
                      No financial details were submitted for this employee.
                    </p>
                  )}
                </QueryState>
              )}

              {tab === 'medical' && (
                <QueryState query={medical}>
                  {medical.data ? (
                    <MedicalEvidenceView data={medical.data} employeeId={submission.employeeId} />
                  ) : (
                    <p className="muted" style={{ padding: '16px 0' }}>
                      No medical disclosure was submitted for this employee.
                    </p>
                  )}
                </QueryState>
              )}

              {tab === 'history' && (
                <QueryState query={history}>
                  {!history.data?.length ? (
                    <p className="muted" style={{ padding: '16px 0' }}>
                      No prior hiring decisions recorded for this submission.
                    </p>
                  ) : (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                      {history.data.map((d) => (
                        <article className="hiring-info-card" key={d.id}>
                          <div
                            style={{
                              display: 'flex',
                              justifyContent: 'space-between',
                              alignItems: 'center',
                              marginBottom: '6px',
                            }}
                          >
                            <span
                              className={`hiring-pill-badge ${
                                d.decision === 1
                                  ? 'approval-status--latest'
                                  : d.decision === 2
                                    ? 'approval-status--history'
                                    : 'approval-status--broken'
                              }`}
                            >
                              {decisions[d.decision - 1]}
                            </span>
                            <span style={{ fontSize: '10.5px', color: '#64748b' }}>
                              {labelDate(d.decidedAt)}
                            </span>
                          </div>
                          <p style={{ fontSize: '11.5px', color: '#0f172a', margin: '0 0 4px' }}>
                            {d.reason || 'No comments recorded'}
                          </p>
                          <span style={{ fontSize: '10px', color: '#94a3b8' }}>
                            Decision by Account: {d.actorAccountId}
                          </span>
                        </article>
                      ))}
                    </div>
                  )}
                </QueryState>
              )}
            </>
          )}
        </QueryState>

        {query.isError && (
          <p className="hr-notice">
            The retained submission could not be loaded. No hiring decision can be recorded from
            missing evidence.
          </p>
        )}
      </div>

      {/* ======================================================================
          RIGHT COLUMN: Decision & Placement Dock (Sticky / Persistent)
          ====================================================================== */}
      <div className="hiring-decision-pane">
        <div className="hiring-dock-header">
          <h3 className="hiring-dock-title">
            <ShieldCheck size={16} style={{ color: '#2563eb' }} />
            Hiring Authorization
          </h3>
          <p className="hiring-dock-sub">Review evidence and record onboarding decision.</p>
        </div>

        {/* 1. Readiness Verification Strip */}
        <div className="hiring-readiness-strip">
          <div className="hiring-readiness-row">
            <span>Documents:</span>
            <span
              className={`hiring-readiness-status ${documentsReady ? 'is-ready' : 'is-warning'}`}
            >
              {documentsReady ? <CheckCircle2 size={12} /> : <AlertCircle size={12} />}
              {documentsReady ? 'Verified Ready' : 'Incomplete'}
            </span>
          </div>

          <div className="hiring-readiness-row">
            <span>Placement:</span>
            <span
              className={`hiring-readiness-status ${placementReady ? 'is-ready' : 'is-warning'}`}
            >
              {placementReady ? <CheckCircle2 size={12} /> : <AlertCircle size={12} />}
              {placementReady ? (siteStaff ? 'Site Staff' : 'Office Staff') : 'Pending Class'}
            </span>
          </div>

          {siteStaff && (
            <div className="hiring-readiness-row">
              <span>Site Assigned:</span>
              <span
                className={`hiring-readiness-status ${initialSiteId ? 'is-ready' : 'is-warning'}`}
              >
                {initialSiteId ? <CheckCircle2 size={12} /> : <AlertCircle size={12} />}
                {initialSiteId ? 'Selected' : siteRequired ? 'Mandatory' : 'Optional'}
              </span>
            </div>
          )}
        </div>

        {/* 2. Self Review Warning */}
        {pending && self && (
          <div className="approval-alert-banner">
            <AlertCircle size={13} />
            <span>You cannot decide your own submission or employment.</span>
          </div>
        )}

        {/* 3. Already Approved View */}
        {employee?.onboardingStatus === 2 && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
            <div className="approval-badge approval-badge--mfa" style={{ alignSelf: 'flex-start' }}>
              <CheckCircle2 size={12} />
              Approved Employee
            </div>
            <p style={{ fontSize: '11px', color: '#475569', margin: 0 }}>
              This employee is already approved. Proceed with onboarding configuration below:
            </p>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '6px', marginTop: '4px' }}>
              <Link
                className="button button--secondary approval-btn-sm"
                to={`${basePath}/employees/${submission.employeeId}`}
              >
                <UserRound size={13} /> View Employee Details
              </Link>
              {siteStaff && access.can('hr.sites.read') && (
                <Link
                  className="button button--secondary approval-btn-sm"
                  to={`${basePath}/employees/${submission.employeeId}?section=assignments`}
                >
                  <MapPin size={13} /> Manage Site Assignments
                </Link>
              )}
              {access.can('access.accounts.read') && (
                <Link
                  className="button button--secondary approval-btn-sm"
                  to={`${basePath}/employees/${submission.employeeId}?section=access`}
                >
                  <KeyRound size={13} /> System Roles &amp; Access
                </Link>
              )}
            </div>

            {setup.data && (
              <div className="hiring-info-card" style={{ marginTop: '8px' }}>
                <div className="hiring-info-card-header">Account Setup Status</div>
                <div
                  style={{
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '4px',
                    fontSize: '10.5px',
                  }}
                >
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <span style={{ color: '#64748b' }}>Account state:</span>
                    <span style={{ fontWeight: 600 }}>
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
                    </span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <span style={{ color: '#64748b' }}>Initial access:</span>
                    <span>{setup.data.initialAccessAssigned ? 'Assigned' : 'Not assigned'}</span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <span style={{ color: '#64748b' }}>Mobile enrolled:</span>
                    <span>{setup.data.mobilePhoneEnrolled ? 'Yes' : 'No'}</span>
                  </div>
                </div>
              </div>
            )}
          </div>
        )}

        {/* 4. Active Decision Form (Pending Submission) */}
        {pending && !self && query.data && (
          <form
            onSubmit={handleSubmit}
            style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}
          >
            {/* Visual 3-Button Decision Selector */}
            <label
              style={{
                fontSize: '10.5px',
                fontWeight: 600,
                color: '#475569',
                marginBottom: '-2px',
              }}
            >
              Decision Action *
            </label>
            <div
              className="hiring-decision-selector"
              role="radiogroup"
              aria-label="Hiring decision"
            >
              <button
                type="button"
                className={`hiring-decision-btn ${decision === 1 ? 'is-active-approve' : ''}`}
                onClick={() => {
                  setDecision(1);
                  setError('');
                }}
              >
                <Check size={14} />
                <span>Approve</span>
              </button>
              <button
                type="button"
                className={`hiring-decision-btn ${decision === 2 ? 'is-active-return' : ''}`}
                onClick={() => {
                  setDecision(2);
                  setInitialSiteId('');
                  setError('');
                }}
              >
                <CornerUpLeft size={14} />
                <span>Return</span>
              </button>
              <button
                type="button"
                className={`hiring-decision-btn ${decision === 3 ? 'is-active-reject' : ''}`}
                onClick={() => {
                  setDecision(3);
                  setInitialSiteId('');
                  setError('');
                }}
              >
                <X size={14} />
                <span>Reject</span>
              </button>
            </div>

            {/* Initial Site Assignment (if siteStaff and approving) */}
            {decision === 1 && siteStaff && access.can('hr.sites.read') && (
              <div style={{ marginTop: '2px' }}>
                <ReferenceField
                  label="Initial site assignment"
                  kind="sites"
                  required={siteRequired}
                  enableFind
                  value={initialSiteId || null}
                  onChange={(id) => setInitialSiteId(id ?? '')}
                />
              </div>
            )}

            {decision === 1 && siteStaff && !access.can('hr.sites.read') && (
              <div className="approval-alert-banner">
                <AlertCircle size={12} />
                <span>Site selection requires site-read permission.</span>
              </div>
            )}

            {/* Decision Reason Textarea */}
            <div className="field" style={{ marginTop: '2px' }}>
              <label htmlFor="hiring-decision-reason">
                {decision === 1 ? 'Review Notes (Optional)' : 'Correction / Rejection Reason *'}
              </label>
              <textarea
                id="hiring-decision-reason"
                rows={2}
                maxLength={2000}
                placeholder={
                  decision === 1
                    ? 'Optional authorization remarks...'
                    : 'Specify corrections required or rejection reason...'
                }
                value={reason ?? ''}
                required={decision !== 1}
                onChange={(e) => setReason(e.target.value)}
              />
            </div>

            {/* Compliance Attestation Checkbox */}
            <label className="hiring-attest-box">
              <input
                type="checkbox"
                checked={reviewed}
                required
                onChange={(e) => setReviewed(e.target.checked)}
              />
              <span>
                I have reviewed this submission, verified supporting documents, and confirm the
                hiring decision.
              </span>
            </label>

            {/* Error Feedback */}
            {error && (
              <p role="alert" className="hr-message hr-message--error" style={{ margin: '2px 0' }}>
                {error}
              </p>
            )}

            {!documentsReady && decision === 1 && (
              <p role="alert" className="hr-message hr-message--error" style={{ margin: '2px 0' }}>
                Submitted documents are unavailable. Approval cannot proceed.
              </p>
            )}

            {siteRequired && !initialSiteId && decision === 1 && (
              <p role="alert" className="hr-message hr-message--error" style={{ margin: '2px 0' }}>
                Select an initial site required by this staff category.
              </p>
            )}

            {/* Submit Button */}
            <button
              type="submit"
              disabled={!reviewed || (decision === 1 && !canApprove) || action.pending}
              className={`hiring-submit-action-btn ${
                decision === 1 ? 'btn-approve' : decision === 2 ? 'btn-return' : 'btn-reject'
              }`}
            >
              {decision === 1 ? (
                <Check size={15} />
              ) : decision === 2 ? (
                <CornerUpLeft size={15} />
              ) : (
                <X size={15} />
              )}
              {action.pending
                ? 'Processing...'
                : decision === 1
                  ? 'Confirm & Approve Hiring'
                  : decision === 2
                    ? 'Return for Correction'
                    : 'Confirm Rejection'}
            </button>

            {action.errorStatus !== null &&
              action.errorStatus >= 400 &&
              action.errorStatus < 500 && (
                <Button
                  variant="secondary"
                  type="button"
                  className="approval-btn-sm"
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
                  <RefreshCw size={13} />
                  Refresh Evidence
                </Button>
              )}
          </form>
        )}

        <ActionMessage action={action} />
      </div>
    </div>
  );
}

/* ============================================================================
   Profile Evidence View (Structured Card Presentation)
   ============================================================================ */
function ProfileEvidenceView({ profile }: { profile: Profile }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
      {/* 1. Identity & Personal Details */}
      <div className="hiring-info-card">
        <div className="hiring-info-card-header">
          <UserCheck size={13} style={{ color: '#2563eb' }} />
          Identity &amp; Personal Demographics
        </div>
        <div className="hiring-key-val-grid">
          <div className="hiring-field-item">
            <span className="hiring-field-label">Full Legal Name</span>
            <span className="hiring-field-value">{profile.fullLegalName || 'Not set'}</span>
          </div>
          <div className="hiring-field-item">
            <span className="hiring-field-label">Father / Spouse Name</span>
            <span className="hiring-field-value">{profile.fatherOrSpouseName || 'Not set'}</span>
          </div>
          <div className="hiring-field-item">
            <span className="hiring-field-label">Identity Document</span>
            <span className="hiring-field-value">
              {profile.identityDocumentType === 1
                ? 'CNIC'
                : profile.identityDocumentType === 2
                  ? 'Passport'
                  : 'Document'}{' '}
              {profile.identityDocumentNumber ? `· ${profile.identityDocumentNumber}` : 'Not set'}
            </span>
          </div>
          <div className="hiring-field-item">
            <span className="hiring-field-label">Date of Birth</span>
            <span className="hiring-field-value">{profile.dateOfBirth || 'Not set'}</span>
          </div>
          <div className="hiring-field-item">
            <span className="hiring-field-label">Gender &amp; Marital Status</span>
            <span className="hiring-field-value">
              {profile.gender || 'Not set'} &middot; {profile.maritalStatus || 'Not set'}
            </span>
          </div>
          <div className="hiring-field-item">
            <span className="hiring-field-label">Document Expiry</span>
            <span className="hiring-field-value">
              {profile.identityDocumentExpiresOn || 'Not set'}
            </span>
          </div>
        </div>
      </div>

      {/* 2. Communication & Contact */}
      <div className="hiring-info-card">
        <div className="hiring-info-card-header">
          <Phone size={13} style={{ color: '#2563eb' }} />
          Contact &amp; Telephony
        </div>
        <div className="hiring-key-val-grid">
          <div className="hiring-field-item">
            <span className="hiring-field-label">Personal Phone</span>
            <span className="hiring-field-value">{profile.personalPhone || 'Not set'}</span>
          </div>
          <div className="hiring-field-item">
            <span className="hiring-field-label">Personal Email</span>
            <span className="hiring-field-value">{profile.personalEmail || 'Not set'}</span>
          </div>
          <div className="hiring-field-item">
            <span className="hiring-field-label">Login Phone Number</span>
            <span className="hiring-field-value">{profile.loginPhoneNumber || 'Not set'}</span>
          </div>
          <div className="hiring-field-item">
            <span className="hiring-field-label">Mobile App Access</span>
            <span className="hiring-field-value">
              {profile.mobileAccessRequested ? 'Requested (Enabled)' : 'Not requested'}
            </span>
          </div>
        </div>
      </div>

      {/* 3. Residential Addresses */}
      <div className="hiring-info-card">
        <div className="hiring-info-card-header">
          <Home size={13} style={{ color: '#2563eb' }} />
          Residential Addresses
        </div>
        <div className="hiring-key-val-grid">
          <div className="hiring-field-item">
            <span className="hiring-field-label">Current Address</span>
            <span className="hiring-field-value">
              {profile.currentAddress
                ? `${[profile.currentAddress.line1, profile.currentAddress.line2, profile.currentAddress.city, profile.currentAddress.province, profile.currentAddress.postalCode].filter(Boolean).join(', ')}` ||
                  'Not set'
                : 'Not set'}
            </span>
          </div>
          <div className="hiring-field-item">
            <span className="hiring-field-label">Permanent Address</span>
            <span className="hiring-field-value">
              {profile.permanentAddress
                ? `${[profile.permanentAddress.line1, profile.permanentAddress.line2, profile.permanentAddress.city, profile.permanentAddress.province, profile.permanentAddress.postalCode].filter(Boolean).join(', ')}` ||
                  'Not set'
                : 'Not set'}
            </span>
          </div>
        </div>
      </div>

      {/* 4. Emergency Contact */}
      <div className="hiring-info-card">
        <div className="hiring-info-card-header">
          <HeartPulse size={13} style={{ color: '#dc2626' }} />
          Emergency Contact
        </div>
        <div className="hiring-key-val-grid">
          <div className="hiring-field-item">
            <span className="hiring-field-label">Contact Name &amp; Relationship</span>
            <span className="hiring-field-value">
              {profile.emergencyContact?.name || 'Not set'}{' '}
              {profile.emergencyContact?.relationship
                ? `(${profile.emergencyContact.relationship})`
                : ''}
            </span>
          </div>
          <div className="hiring-field-item">
            <span className="hiring-field-label">Primary Phone</span>
            <span className="hiring-field-value">
              {profile.emergencyContact?.phone || 'Not set'}
            </span>
          </div>
          <div className="hiring-field-item">
            <span className="hiring-field-label">Alternate Phone</span>
            <span className="hiring-field-value">
              {profile.emergencyContact?.alternatePhone || 'Not set'}
            </span>
          </div>
        </div>
      </div>

      {/* 5. Previous Employment, Certifications & Assets */}
      <div className="hiring-info-card">
        <div className="hiring-info-card-header">
          <Briefcase size={13} style={{ color: '#2563eb' }} />
          Career &amp; Professional History
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
          <div>
            <span className="hiring-field-label" style={{ display: 'block', marginBottom: '2px' }}>
              Previous Employment
            </span>
            {profile.previousEmployment.length === 0 ? (
              <span className="hiring-field-value is-muted">No previous employment records</span>
            ) : (
              <ul style={{ margin: 0, paddingLeft: '16px', fontSize: '11px', color: '#0f172a' }}>
                {profile.previousEmployment.map((emp) => (
                  <li key={emp.entryId}>
                    <strong>{emp.employerName}</strong> &mdash; {emp.roleTitle} ({emp.startedOn} to{' '}
                    {emp.endedOn || 'Present'})
                  </li>
                ))}
              </ul>
            )}
          </div>

          <div>
            <span className="hiring-field-label" style={{ display: 'block', marginBottom: '2px' }}>
              Certifications
            </span>
            {profile.certifications.length === 0 ? (
              <span className="hiring-field-value is-muted">No certifications recorded</span>
            ) : (
              <ul style={{ margin: 0, paddingLeft: '16px', fontSize: '11px', color: '#0f172a' }}>
                {profile.certifications.map((cert, i) => (
                  <li key={i}>
                    <strong>{cert.type}</strong> #{cert.number} &mdash; Issued by {cert.issuer} (
                    {cert.issuedOn})
                  </li>
                ))}
              </ul>
            )}
          </div>

          <div>
            <span className="hiring-field-label" style={{ display: 'block', marginBottom: '2px' }}>
              Company Assets
            </span>
            {profile.assets.length === 0 ? (
              <span className="hiring-field-value is-muted">No assets allocated</span>
            ) : (
              <ul style={{ margin: 0, paddingLeft: '16px', fontSize: '11px', color: '#0f172a' }}>
                {profile.assets.map((asset, i) => (
                  <li key={i}>
                    <strong>{asset.assetReference}</strong> (Condition:{' '}
                    {asset.issueCondition || 'Not recorded'}) &mdash; Issued: {asset.issuedOn}
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

/* ============================================================================
   Financial Evidence View
   ============================================================================ */
function FinancialEvidenceView({ data }: { data: FinancialSection }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
      <div className="hiring-info-card">
        <div className="hiring-info-card-header">
          <CreditCard size={13} style={{ color: '#2563eb' }} />
          Banking &amp; Remittance Information
        </div>
        {data.bank ? (
          <div className="hiring-key-val-grid">
            <div className="hiring-field-item">
              <span className="hiring-field-label">Bank Name</span>
              <span className="hiring-field-value">{data.bank.bankName}</span>
            </div>
            <div className="hiring-field-item">
              <span className="hiring-field-label">Account Title</span>
              <span className="hiring-field-value">{data.bank.accountTitle}</span>
            </div>
            <div className="hiring-field-item">
              <span className="hiring-field-label">Account Number</span>
              <span className="hiring-field-value">{data.bank.accountNumber}</span>
            </div>
            <div className="hiring-field-item">
              <span className="hiring-field-label">IBAN</span>
              <span className="hiring-field-value">{data.bank.iban || 'Not provided'}</span>
            </div>
          </div>
        ) : (
          <p className="muted" style={{ margin: 0, fontSize: '11px' }}>
            No bank details recorded. Payment method: Cash or pending cheque.
          </p>
        )}
      </div>

      <div className="hiring-info-card">
        <div className="hiring-info-card-header">Compensation &amp; Wage Basis</div>
        <div className="hiring-key-val-grid">
          <div className="hiring-field-item">
            <span className="hiring-field-label">Base Salary</span>
            <span className="hiring-field-value">
              {data.baseSalary != null
                ? `${data.currency} ${data.baseSalary.toLocaleString()}`
                : 'Not set'}
            </span>
          </div>
          <div className="hiring-field-item">
            <span className="hiring-field-label">Gross Salary</span>
            <span className="hiring-field-value">
              {data.grossSalary != null
                ? `${data.currency} ${data.grossSalary.toLocaleString()}`
                : 'Not set'}
            </span>
          </div>
          <div className="hiring-field-item">
            <span className="hiring-field-label">Effective From</span>
            <span className="hiring-field-value">{data.effectiveFrom || 'Joining date'}</span>
          </div>
        </div>
      </div>
    </div>
  );
}

/* ============================================================================
   Medical Evidence View
   ============================================================================ */
function MedicalEvidenceView({ data, employeeId }: { data: MedicalSection; employeeId: string }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
      <div className="hiring-info-card">
        <div className="hiring-info-card-header">
          <HeartPulse size={13} style={{ color: '#dc2626' }} />
          Medical Disclosures &amp; Fit-for-Duty Record
        </div>
        <div
          style={{ fontSize: '11.5px', color: '#0f172a', whiteSpace: 'pre-wrap', lineHeight: 1.4 }}
        >
          {data.details || 'No specific health conditions or medical restrictions reported.'}
        </div>
      </div>

      {data.supportingDocumentId && (
        <SubmissionDocuments
          employeeId={employeeId}
          medical
          documentIds={[data.supportingDocumentId]}
        />
      )}
    </div>
  );
}
