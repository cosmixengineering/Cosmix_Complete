import { useMemo, useState, type FormEvent } from 'react';
import { z } from 'zod';
import { useAuth } from '@/auth/use-auth';
import { OutstationWorkspace } from '../outstation/OutstationWorkspace';
import { canDeployment } from '../outstation/contracts';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { Button } from '@/components/ui/Button';
import { Denied, ActionMessage } from '../components/Feedback';
import { FinanceQueryState as QueryState } from '../components/finance/FinanceQueryState';
import '../components/finance/allowances.css';
import { RecordView } from '../components/RecordView';
import {
  FinanceNotice,
  FinanceStatus,
  FinanceTabs,
  HasMorePager,
  TextAreaField,
} from '../components/finance/FinanceChrome';
import {
  allowanceKindOptions,
  approvalStateOptions,
  dayRange,
  decisionOptions,
  money,
  policyTargetOptions,
} from '../components/finance/labels';
import { useFinanceAccess } from '../hooks/use-finance-access';
import { useFinanceAction, useFinanceQuery } from '../hooks/use-finance-queries';
import { queryString } from '../api/hr-finance-api';
import { labelDate } from '../utils/messages';
import {
  allowanceClaim,
  allowanceDetail,
  allowancePage,
  policyAssignmentPage,
  policyAssignmentResult,
  policyPage,
  policyPublicationPreview,
  policyPublicationResult,
  policyView,
  policyVersionPage,
  type PolicyView,
} from '../schemas/finance';

const permissions = {
  review: 'attendance.allowances.review',
  decide: 'attendance.allowances.decide',
  settlement: 'attendance.allowances.settlement',
  policy: 'attendance.allowances.policy',
};

export function AllowancesPage() {
  const { user, status } = useAuth();
  const epoch = useMemo(() => crypto.randomUUID(), [user, status]);
  if (status !== 'authenticated') return <Denied />;
  return <AllowancesContent key={epoch} />;
}

function AllowancesContent() {
  const [outstationBusy, setOutstationBusy] = useState(false);
  const access = useFinanceAccess();
  const tabs = [
    ...(canDeployment(access.user, 'read')
      ? [{ id: 'outstation', label: 'Outstation management' }]
      : []),
    ...(access.can(permissions.review) ? [{ id: 'review', label: 'Review queue' }] : []),
    ...(access.can(permissions.policy) ? [{ id: 'policies', label: 'Allowance policies' }] : []),
  ];
  const [tab, setTab] = useState(tabs[0]?.id ?? '');
  if (!tabs.length) return <Denied />;
  const active = tabs.some((item) => item.id === tab) ? tab : (tabs[0]?.id ?? '');
  return (
    <section className="allowances-page" aria-label="Allowances">
      <header className="allowances-heading">
        <p>Human resources / Attendance</p>
        <h1>Allowances</h1>
      </header>
      <fieldset disabled={outstationBusy} style={{ border: 0, padding: 0, margin: 0, minWidth: 0 }}>
        <FinanceTabs tabs={tabs} active={active} onChange={setTab} label="Allowance views" />
      </fieldset>
      {active === 'outstation' && <OutstationWorkspace onBusyChange={setOutstationBusy} />}
      {active === 'review' && (
        <AllowanceReview
          canDecide={access.can(permissions.decide)}
          canSettle={access.can(permissions.settlement)}
        />
      )}
      {active === 'policies' && <AllowancePolicies />}
    </section>
  );
}

function AllowanceReview({ canDecide, canSettle }: { canDecide: boolean; canSettle: boolean }) {
  const initial = useMemo(() => dayRange(30), []);
  const [from, setFrom] = useState(initial.from);
  const [to, setTo] = useState(initial.to);
  const [state, setState] = useState('0');
  const [employeeId, setEmployeeId] = useState('');
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<{ employeeId: string; id: string } | null>(null);
  const [decision, setDecision] = useState('1');
  const [decisionReason, setDecisionReason] = useState('');
  const [paymentReference, setPaymentReference] = useState('');
  const [settlementReason, setSettlementReason] = useState('');
  const action = useFinanceAction();
  const employeeFilterValid = !employeeId.trim() || z.uuid().safeParse(employeeId.trim()).success;

  const queue = useFinanceQuery(
    '/attendance/allowances/review' +
      queryString({
        from,
        to,
        employeeId: employeeId.trim() || undefined,
        state: state === '' ? undefined : Number(state),
        page,
        pageSize: 25,
      }),
    allowancePage,
    true,
    dayRangeValid(from, to) && employeeFilterValid,
  );
  const detail = useFinanceQuery(
    selected
      ? `/attendance/allowances/employees/${selected.employeeId}/claims/${selected.id}`
      : '/attendance/allowances/review',
    allowanceDetail,
    Boolean(selected),
    Boolean(selected),
  );

  async function decide(event: FormEvent) {
    event.preventDefault();
    if (!selected || detail.data?.claim.kind === 5) return;
    const result = await action.run(
      `/attendance/allowances/employees/${selected.employeeId}/claims/${selected.id}/decide`,
      {
        expectedVersion: detail.data?.claim.version,
        decision: Number(decision),
        reason: decisionReason,
      },
      'POST',
      allowanceClaim,
    );
    if (result) {
      setDecisionReason('');
      void queue.refetch();
      void detail.refetch();
    }
  }

  async function settle(event: FormEvent) {
    event.preventDefault();
    if (!selected || detail.data?.claim.kind === 5) return;
    const result = await action.run(
      `/attendance/allowances/employees/${selected.employeeId}/claims/${selected.id}/settlement`,
      {
        expectedVersion: detail.data?.claim.version,
        externalPaymentReference: paymentReference,
        reason: settlementReason,
      },
      'POST',
      allowanceClaim,
    );
    if (result) {
      setPaymentReference('');
      setSettlementReason('');
      void queue.refetch();
      void detail.refetch();
    }
  }

  return (
    <>
      <section className="hr-form-section">
        <h2>Review queue</h2>
        <div className="hr-toolbar">
          <TextInput
            label="From"
            type="date"
            value={from}
            onChange={(event) => {
              setFrom(event.target.value);
              setPage(1);
            }}
          />
          <TextInput
            label="To"
            type="date"
            value={to}
            onChange={(event) => {
              setTo(event.target.value);
              setPage(1);
            }}
          />
          <SelectInput
            label="State"
            value={state}
            options={approvalStateOptions}
            onChange={(event) => {
              setState(event.target.value);
              setPage(1);
            }}
          />
          <TextInput
            label="Employee id"
            value={employeeId}
            placeholder="All reviewed employees"
            error={
              employeeFilterValid ? undefined : 'Enter a complete employee ID or leave it blank.'
            }
            onChange={(event) => {
              setEmployeeId(event.target.value);
              setPage(1);
            }}
          />
        </div>
        {!dayRangeValid(from, to) && (
          <p className="hr-message hr-message--error" role="alert">
            Choose a range ending after the start date and no longer than 366 days.
          </p>
        )}
        {dayRangeValid(from, to) && employeeFilterValid && (
          <QueryState query={queue}>
            {!queue.data?.items.length && (
              <FinanceNotice>No claims match this filter.</FinanceNotice>
            )}
            {queue.data?.items.map((claim) => (
              <article className="hr-repeat" key={claim.id}>
                <div className="hr-repeat-heading">
                  <h3>
                    {claim.employeeId} &middot;{' '}
                    {allowanceKindOptions[claim.kind - 1]?.label ?? claim.kind} &middot;{' '}
                    <FinanceStatus value={claim.state} />
                  </h3>
                  <div className="hr-actions">
                    <Button
                      variant="secondary"
                      onClick={() =>
                        setSelected(
                          selected?.id === claim.id
                            ? null
                            : { employeeId: claim.employeeId, id: claim.id },
                        )
                      }
                    >
                      {selected?.id === claim.id ? 'Close claim' : 'Review claim'}
                    </Button>
                  </div>
                </div>
                <RecordView
                  value={{
                    claimReference: claim.id,
                    payableDate: claim.payableDate,
                    claimedAmount: money(claim.claimedAmount, claim.currency),
                    settlement: claim.settlementState,
                    createdAt: labelDate(claim.createdAt),
                    decidedAt: labelDate(claim.decidedAt),
                  }}
                />
                {selected?.id === claim.id && (
                  <QueryState query={detail}>
                    {detail.data && (
                      <>
                        <RecordView
                          value={{
                            serverReason: detail.data.reason,
                            visibility: detail.data.visibility,
                            eligibility: detail.data.eligibility.state,
                            eligibilityReason: detail.data.eligibility.reason,
                            longestCycleHours: detail.data.eligibility.continuousHours,
                            requiredHours: detail.data.eligibility.requiredHours,
                          }}
                        />
                        {detail.data.claim.kind === 5 && (
                          <FinanceNotice>
                            Outstation claims require retained deployment evidence and independent
                            deployment review. No outstation settlement is available here.
                          </FinanceNotice>
                        )}
                        {canDecide &&
                          detail.data.claim.kind !== 5 &&
                          detail.data.claim.state === 'Pending' && (
                            <div className="hr-form-section">
                              <h3>Decision</h3>
                              <form
                                className="hr-form-grid"
                                onSubmit={(event) => void decide(event)}
                              >
                                <SelectInput
                                  label="Decision"
                                  value={decision}
                                  options={decisionOptions}
                                  onChange={(event) => setDecision(event.target.value)}
                                />
                                <TextAreaField
                                  label="Decision reason"
                                  required
                                  value={decisionReason}
                                  onChange={setDecisionReason}
                                  disabled={action.pending}
                                />
                                <div className="hr-actions wide">
                                  <Button
                                    type="submit"
                                    disabled={action.pending || !decisionReason.trim()}
                                  >
                                    Record decision
                                  </Button>
                                </div>
                              </form>
                            </div>
                          )}
                        {canSettle &&
                          detail.data.claim.kind !== 5 &&
                          detail.data.claim.settlementState === 'InPayrollHandoff' && (
                            <div className="hr-form-section">
                              <h3>Record payroll payment</h3>
                              <p className="muted">
                                Only an approved claim already scheduled by a locked payroll handoff
                                can be recorded as paid.
                              </p>
                              <form
                                className="hr-form-grid"
                                onSubmit={(event) => void settle(event)}
                              >
                                <TextInput
                                  label="External payment reference"
                                  required
                                  maxLength={150}
                                  value={paymentReference}
                                  onChange={(event) => setPaymentReference(event.target.value)}
                                />
                                <TextAreaField
                                  label="Reason"
                                  required
                                  value={settlementReason}
                                  onChange={setSettlementReason}
                                  disabled={action.pending}
                                />
                                <div className="hr-actions wide">
                                  <Button
                                    type="submit"
                                    disabled={
                                      action.pending ||
                                      !paymentReference.trim() ||
                                      !settlementReason.trim()
                                    }
                                  >
                                    Record payment
                                  </Button>
                                </div>
                              </form>
                            </div>
                          )}
                      </>
                    )}
                  </QueryState>
                )}
              </article>
            ))}
            {queue.data && (
              <HasMorePager
                page={page}
                hasMore={queue.data.hasMore}
                busy={queue.isFetching}
                onPrevious={() => setPage((value) => Math.max(1, value - 1))}
                onNext={() => setPage((value) => value + 1)}
              />
            )}
          </QueryState>
        )}
        <ActionMessage action={action} />
      </section>
    </>
  );
}

function AllowancePolicies() {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [term, setTerm] = useState('');
  const [selected, setSelected] = useState<PolicyView | null>(null);
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [createReason, setCreateReason] = useState('');
  const create = useFinanceAction();
  const publish = useFinanceAction();
  const assign = useFinanceAction();

  const [rules, setRules] = useState({
    currency: 'PKR',
    officeDutyStartLocalTime: '17:00',
    continuityGapToleranceMinutes: '30',
    officeMealHours: '8',
    officeMealAmount: '',
    siteDinnerHours: '8',
    siteDinnerAmount: '',
    siteBreakfastHours: '2',
    siteBreakfastAmount: '',
    siteLunchHours: '1',
    siteLunchAmount: '',
    outstationDailyAmount: '',
    workTravelAmount: '',
    effectiveFrom: '',
    effectiveTo: '',
    reason: '',
  });
  const [preview, setPreview] = useState<z.infer<typeof policyPublicationPreview> | null>(null);
  const [assignForm, setAssignForm] = useState({
    policyVersionId: '',
    target: '1',
    targetId: '',
    effectiveFrom: '',
    effectiveTo: '',
    reason: '',
  });

  const list = useFinanceQuery(
    '/hr/allowance-policies' + queryString({ page, pageSize: 25, search: term || undefined }),
    policyPage,
    true,
  );
  const versions = useFinanceQuery(
    selected
      ? `/hr/allowance-policies/${selected.id}/versions` + queryString({ page: 1, pageSize: 50 })
      : '/hr/allowance-policies',
    policyVersionPage,
    Boolean(selected),
    Boolean(selected),
  );
  const assignments = useFinanceQuery(
    selected
      ? `/hr/allowance-policies/${selected.id}/assignments` + queryString({ page: 1, pageSize: 50 })
      : '/hr/allowance-policies',
    policyAssignmentPage,
    Boolean(selected),
    Boolean(selected),
  );

  function rulesBody() {
    const meal = (hours: string, amount: string) => ({
      requiredContinuousDutyHours: hours === '' ? null : Number(hours),
      amount: amount === '' ? null : Number(amount),
    });
    return {
      schemaVersion: 1,
      currency: rules.currency.trim().toUpperCase(),
      officeDutyStartLocalTime: rules.officeDutyStartLocalTime,
      continuityGapToleranceMinutes: Number(rules.continuityGapToleranceMinutes),
      officeMeal: meal(rules.officeMealHours, rules.officeMealAmount),
      siteDinner: meal(rules.siteDinnerHours, rules.siteDinnerAmount),
      siteBreakfast: meal(rules.siteBreakfastHours, rules.siteBreakfastAmount),
      siteLunch: meal(rules.siteLunchHours, rules.siteLunchAmount),
      ...(rules.outstationDailyAmount
        ? {
            outstationDaily: {
              requiredContinuousDutyHours: null,
              amount: Number(rules.outstationDailyAmount),
            },
          }
        : {}),
      ...(rules.workTravelAmount
        ? {
            workTravel: {
              requiredContinuousDutyHours: null,
              amount: Number(rules.workTravelAmount),
            },
          }
        : {}),
    };
  }

  function publishBody() {
    return {
      expectedVersion: selected?.version,
      expectedRevision: selected?.revision,
      effectiveFrom: rules.effectiveFrom,
      effectiveTo: rules.effectiveTo || null,
      rules: rulesBody(),
    };
  }

  async function createPolicy(event: FormEvent) {
    event.preventDefault();
    const result = await create.run(
      '/hr/allowance-policies',
      { code, name, reason: createReason },
      'POST',
      policyView,
    );
    if (result) {
      setCode('');
      setName('');
      setCreateReason('');
      setSelected(result);
      void list.refetch();
    }
  }

  async function previewRules() {
    if (!selected) return;
    const result = await publish.run(
      `/hr/allowance-policies/${selected.id}/publication-preview`,
      publishBody(),
      'POST',
      policyPublicationPreview,
    );
    setPreview(result ?? null);
  }

  async function publishRules(event: FormEvent) {
    event.preventDefault();
    if (!selected || !preview?.canPublish) return;
    const result = await publish.run(
      `/hr/allowance-policies/${selected.id}/versions`,
      {
        ...publishBody(),
        expectedPreviewFingerprint: preview.previewFingerprint,
        reason: rules.reason,
      },
      'POST',
      policyPublicationResult,
    );
    if (result) {
      setPreview(null);
      void versions.refetch();
      void list.refetch();
    }
  }

  async function assignVersion(event: FormEvent) {
    event.preventDefault();
    if (!selected) return;
    const result = await assign.run(
      `/hr/allowance-policies/${selected.id}/assignments`,
      {
        expectedVersion: selected.version,
        expectedRevision: selected.revision,
        policyVersionId: assignForm.policyVersionId,
        target: Number(assignForm.target),
        targetId: assignForm.targetId,
        effectiveFrom: assignForm.effectiveFrom,
        effectiveTo: assignForm.effectiveTo || null,
        reason: assignForm.reason,
      },
      'POST',
      policyAssignmentResult,
    );
    if (result) {
      setAssignForm({
        policyVersionId: '',
        target: '1',
        targetId: '',
        effectiveFrom: '',
        effectiveTo: '',
        reason: '',
      });
      void assignments.refetch();
    }
  }

  return (
    <>
      <section className="hr-form-section">
        <h2>Create a policy definition</h2>
        <p className="muted">
          A definition carries no money value. Publish an explicit rules version and assign it
          before claims can use it.
        </p>
        <form className="hr-form-grid" onSubmit={(event) => void createPolicy(event)}>
          <TextInput
            label="Code"
            required
            maxLength={40}
            value={code}
            onChange={(event) => setCode(event.target.value)}
          />
          <TextInput
            label="Name"
            required
            maxLength={120}
            value={name}
            onChange={(event) => setName(event.target.value)}
          />
          <TextAreaField
            label="Reason"
            required
            value={createReason}
            onChange={setCreateReason}
            disabled={create.pending}
          />
          <div className="hr-actions wide">
            <Button
              type="submit"
              disabled={create.pending || !code.trim() || !name.trim() || !createReason.trim()}
            >
              Create policy
            </Button>
          </div>
        </form>
        <ActionMessage action={create} />
      </section>

      <section className="hr-form-section">
        <h2>Policies</h2>
        <div className="hr-toolbar">
          <TextInput
            label="Search"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
          <Button
            variant="secondary"
            onClick={() => {
              setTerm(search.trim());
              setPage(1);
            }}
          >
            Search
          </Button>
        </div>
        <QueryState query={list}>
          {!list.data?.items.length && <FinanceNotice>No allowance policies found.</FinanceNotice>}
          {list.data?.items.map((policy) => (
            <article className="hr-repeat" key={policy.id}>
              <div className="hr-repeat-heading">
                <h3>
                  {policy.code} &middot; {policy.name}
                </h3>
                <div className="hr-actions">
                  <Button
                    variant="secondary"
                    onClick={() => {
                      setPreview(null);
                      setSelected(selected?.id === policy.id ? null : policy);
                    }}
                  >
                    {selected?.id === policy.id ? 'Close policy' : 'Open policy'}
                  </Button>
                </div>
              </div>
              <RecordView
                value={{
                  policyReference: policy.id,
                  code: policy.code,
                  name: policy.name,
                  revision: policy.revision,
                  version: policy.version,
                }}
              />
              {selected?.id === policy.id && (
                <>
                  <QueryState query={versions}>
                    <h4>Published versions</h4>
                    {!versions.data?.items.length && (
                      <p className="muted">No version has been published yet.</p>
                    )}
                    {versions.data?.items.map((version) => (
                      <RecordView
                        key={version.id}
                        value={{
                          versionReference: version.id,
                          number: version.number,
                          effectiveFrom: version.effectiveFrom,
                          effectiveTo: version.effectiveTo,
                          configuration: version.configurationSha256,
                          authority: version.authority,
                          publishedAt: labelDate(version.publishedAt),
                          canonicalJson: version.canonicalJson,
                        }}
                      />
                    ))}
                  </QueryState>
                  <QueryState query={assignments}>
                    <h4>Assignments</h4>
                    {!assignments.data?.items.length && (
                      <p className="muted">No population has been assigned yet.</p>
                    )}
                    {assignments.data?.items.map((assignment) => (
                      <RecordView
                        key={assignment.id}
                        value={{
                          assignmentReference: assignment.id,
                          target:
                            policyTargetOptions[assignment.target - 1]?.label ?? assignment.target,
                          targetId: assignment.targetId,
                          effectiveFrom: assignment.effectiveFrom,
                          effectiveTo: assignment.effectiveTo,
                          reason: assignment.reason,
                          authority: assignment.authority,
                          recordedAt: labelDate(assignment.recordedAt),
                        }}
                      />
                    ))}
                  </QueryState>

                  <div className="hr-form-section">
                    <h3>Publish a rules version</h3>
                    <div className="hr-form-grid">
                      <TextInput
                        label="Currency"
                        required
                        maxLength={3}
                        value={rules.currency}
                        onChange={(event) => setRules({ ...rules, currency: event.target.value })}
                      />
                      <TextInput
                        label="Office duty start (HH:mm)"
                        required
                        value={rules.officeDutyStartLocalTime}
                        onChange={(event) =>
                          setRules({ ...rules, officeDutyStartLocalTime: event.target.value })
                        }
                      />
                      <TextInput
                        label="Continuity gap tolerance (minutes)"
                        required
                        type="number"
                        min={0}
                        max={180}
                        value={rules.continuityGapToleranceMinutes}
                        onChange={(event) =>
                          setRules({ ...rules, continuityGapToleranceMinutes: event.target.value })
                        }
                      />
                      <TextInput
                        label="Effective from"
                        required
                        type="date"
                        value={rules.effectiveFrom}
                        onChange={(event) =>
                          setRules({ ...rules, effectiveFrom: event.target.value })
                        }
                      />
                      <TextInput
                        label="Effective to"
                        type="date"
                        value={rules.effectiveTo}
                        onChange={(event) =>
                          setRules({ ...rules, effectiveTo: event.target.value })
                        }
                      />
                      <TextInput
                        label="Office meal hours"
                        required
                        type="number"
                        value={rules.officeMealHours}
                        onChange={(event) =>
                          setRules({ ...rules, officeMealHours: event.target.value })
                        }
                      />
                      <TextInput
                        label="Office meal amount"
                        required
                        type="number"
                        step="0.01"
                        value={rules.officeMealAmount}
                        onChange={(event) =>
                          setRules({ ...rules, officeMealAmount: event.target.value })
                        }
                      />
                      <TextInput
                        label="Site dinner hours"
                        required
                        type="number"
                        value={rules.siteDinnerHours}
                        onChange={(event) =>
                          setRules({ ...rules, siteDinnerHours: event.target.value })
                        }
                      />
                      <TextInput
                        label="Site dinner amount"
                        type="number"
                        step="0.01"
                        value={rules.siteDinnerAmount}
                        onChange={(event) =>
                          setRules({ ...rules, siteDinnerAmount: event.target.value })
                        }
                      />
                      <TextInput
                        label="Site breakfast hours (after dinner)"
                        required
                        type="number"
                        value={rules.siteBreakfastHours}
                        onChange={(event) =>
                          setRules({ ...rules, siteBreakfastHours: event.target.value })
                        }
                      />
                      <TextInput
                        label="Site breakfast amount"
                        required
                        type="number"
                        step="0.01"
                        value={rules.siteBreakfastAmount}
                        onChange={(event) =>
                          setRules({ ...rules, siteBreakfastAmount: event.target.value })
                        }
                      />
                      <TextInput
                        label="Site lunch hours (after breakfast)"
                        required
                        type="number"
                        value={rules.siteLunchHours}
                        onChange={(event) =>
                          setRules({ ...rules, siteLunchHours: event.target.value })
                        }
                      />
                      <TextInput
                        label="Site lunch amount"
                        required
                        type="number"
                        step="0.01"
                        value={rules.siteLunchAmount}
                        onChange={(event) =>
                          setRules({ ...rules, siteLunchAmount: event.target.value })
                        }
                      />
                      <TextInput
                        label="Outstation daily amount (optional)"
                        type="number"
                        step="0.01"
                        value={rules.outstationDailyAmount}
                        onChange={(event) =>
                          setRules({ ...rules, outstationDailyAmount: event.target.value })
                        }
                      />
                      <TextInput
                        label="Work travel amount (optional)"
                        type="number"
                        step="0.01"
                        value={rules.workTravelAmount}
                        onChange={(event) =>
                          setRules({ ...rules, workTravelAmount: event.target.value })
                        }
                      />
                    </div>
                    <div className="hr-actions">
                      <Button
                        variant="secondary"
                        disabled={publish.pending}
                        onClick={() => void previewRules()}
                      >
                        Validate and preview
                      </Button>
                    </div>
                    <ActionMessage action={publish} />
                    {preview && !preview.canPublish && (
                      <div className="hr-message hr-message--error" role="alert">
                        <p>The rules are not valid yet:</p>
                        <ul>
                          {preview.errors.map((error) => (
                            <li key={error.path + error.code}>
                              {error.path}: {error.message}
                            </li>
                          ))}
                        </ul>
                      </div>
                    )}
                    {preview?.canPublish && (
                      <form className="hr-form-grid" onSubmit={(event) => void publishRules(event)}>
                        <p className="muted wide">
                          Preview fingerprint {preview.previewFingerprint} &middot; revision{' '}
                          {preview.revision}
                        </p>
                        <TextAreaField
                          label="Publication reason"
                          required
                          value={rules.reason}
                          onChange={(value) => setRules({ ...rules, reason: value })}
                          disabled={publish.pending}
                        />
                        <div className="hr-actions wide">
                          <Button
                            type="submit"
                            variant="success"
                            disabled={publish.pending || !rules.reason.trim()}
                          >
                            Publish version
                          </Button>
                        </div>
                      </form>
                    )}
                  </div>

                  <div className="hr-form-section">
                    <h3>Assign to a population</h3>
                    <form className="hr-form-grid" onSubmit={(event) => void assignVersion(event)}>
                      <SelectInput
                        label="Policy version"
                        required
                        value={assignForm.policyVersionId}
                        options={[
                          { value: '', label: 'Choose a published version' },
                          ...(versions.data?.items ?? []).map((version) => ({
                            value: version.id,
                            label: `Version ${version.number} (${version.effectiveFrom})`,
                          })),
                        ]}
                        onChange={(event) =>
                          setAssignForm({ ...assignForm, policyVersionId: event.target.value })
                        }
                      />
                      <SelectInput
                        label="Target"
                        value={assignForm.target}
                        options={policyTargetOptions}
                        onChange={(event) =>
                          setAssignForm({ ...assignForm, target: event.target.value })
                        }
                      />
                      <TextInput
                        label="Target id"
                        required
                        value={assignForm.targetId}
                        onChange={(event) =>
                          setAssignForm({ ...assignForm, targetId: event.target.value })
                        }
                      />
                      <TextInput
                        label="Effective from"
                        required
                        type="date"
                        value={assignForm.effectiveFrom}
                        onChange={(event) =>
                          setAssignForm({ ...assignForm, effectiveFrom: event.target.value })
                        }
                      />
                      <TextInput
                        label="Effective to"
                        type="date"
                        value={assignForm.effectiveTo}
                        onChange={(event) =>
                          setAssignForm({ ...assignForm, effectiveTo: event.target.value })
                        }
                      />
                      <TextAreaField
                        label="Reason"
                        required
                        value={assignForm.reason}
                        onChange={(value) => setAssignForm({ ...assignForm, reason: value })}
                        disabled={assign.pending}
                      />
                      <div className="hr-actions wide">
                        <Button
                          type="submit"
                          disabled={
                            assign.pending ||
                            !assignForm.policyVersionId ||
                            !assignForm.targetId.trim() ||
                            !assignForm.effectiveFrom ||
                            !assignForm.reason.trim()
                          }
                        >
                          Assign policy
                        </Button>
                      </div>
                    </form>
                    <ActionMessage action={assign} />
                  </div>
                </>
              )}
            </article>
          ))}
          {list.data && (
            <HasMorePager
              page={page}
              hasMore={list.data.hasMore}
              busy={list.isFetching}
              onPrevious={() => setPage((value) => Math.max(1, value - 1))}
              onNext={() => setPage((value) => value + 1)}
            />
          )}
        </QueryState>
      </section>
    </>
  );
}

function dayRangeValid(from: string, to: string) {
  if (!z.iso.date().safeParse(from).success || !z.iso.date().safeParse(to).success) return false;
  const start = Date.parse(from);
  const end = Date.parse(to);
  if (Number.isNaN(start) || Number.isNaN(end)) return false;
  return end > start && (end - start) / 86_400_000 <= 366;
}
