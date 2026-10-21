import { useMemo, useState, type FormEvent } from 'react';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { Button } from '@/components/ui/Button';
import { Denied } from '../../hr/components/Feedback';
import { RecordView } from '../../hr/components/RecordView';
import {
  FinanceNotice,
  FinanceTabs,
  TextAreaField,
} from '../../hr/components/finance/FinanceChrome';
import { useFinanceAction, useFinanceQuery } from '../../hr/hooks/use-finance-queries';
import { labelDate } from '../../hr/utils/messages';
import { ApprovalQueryState, ApprovalActionMessage } from '../components/ApprovalFeedback';
import { useApprovalAccess } from '../hooks/use-approval-access';
import {
  approvalRequestTypeOptions,
  approvalRoute,
  approvalRouteList,
  approvalDelegation,
  approvalDelegationList,
  stageOptions,
  stagePermission,
  type ApprovalRoute,
  type ApprovalDelegation,
} from '../api/approval-schemas';

const scopeKindOptions = [
  { value: '1', label: 'Self' },
  { value: '2', label: 'Employee' },
  { value: '3', label: 'Department' },
  { value: '4', label: 'Team' },
  { value: '5', label: 'Site' },
  { value: '6', label: 'Project' },
  { value: '7', label: 'All' },
];

const codePattern = /^[A-Za-z0-9][A-Za-z0-9_.-]{0,29}$/;
const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function effectiveLatestPerCode(rows: ApprovalRoute[]): Map<string, ApprovalRoute> {
  const current = new Map<string, ApprovalRoute>();
  for (const row of rows) {
    if (row.effectiveTo !== null && row.effectiveTo < new Date().toISOString().slice(0, 10))
      continue;
    const found = current.get(row.code);
    if (
      !found ||
      row.version > found.version ||
      (row.version === found.version && row.effectiveFrom > found.effectiveFrom)
    )
      current.set(row.code, row);
  }
  return current;
}

export function ApprovalRoutingPage() {
  const access = useApprovalAccess();
  const tabs = [
    ...(access.canRead ? [{ id: 'routes', label: 'Approval routes' }] : []),
    ...(access.canDelegate ? [{ id: 'delegations', label: 'Delegations' }] : []),
  ];
  const [tab, setTab] = useState(tabs[0]?.id ?? '');
  if (!tabs.length) return <Denied />;
  const active = tabs.some((item) => item.id === tab) ? tab : (tabs[0]?.id ?? '');
  return (
    <section aria-label="Approval routing">
      <div className="hr-page-heading">
        <p className="hr-kicker">HUMAN RESOURCES &bull; Governance</p>
        <h1>Approval Routing</h1>
      </div>
      <FinanceTabs tabs={tabs} active={active} onChange={setTab} label="Approval routing views" />
      {active === 'routes' && <ApprovalRoutes canManage={access.canManage} />}
      {active === 'delegations' && <ApprovalDelegations />}
    </section>
  );
}

function ApprovalRoutes({ canManage }: { canManage: boolean }) {
  const [requestType, setRequestType] = useState('allowance');
  const list = useFinanceQuery(
    `/approvals/routes?requestType=${encodeURIComponent(requestType)}`,
    approvalRouteList,
    true,
  );
  const latest = useMemo(() => effectiveLatestPerCode(list.data ?? []), [list.data]);
  return (
    <>
      <div className="hr-form-section">
        <h2>Configured routes</h2>
        <div className="hr-form-grid">
          <SelectInput
            label="Request type"
            value={requestType}
            onChange={(event) => setRequestType(event.target.value)}
            options={approvalRequestTypeOptions}
          />
        </div>
        <p className="muted">
          Each stage is published as one immutable version row. A request type becomes routable only
          when its stages form a contiguous 1..N sequence for the latest version.
        </p>
        <ApprovalQueryState query={list}>
          {!list.data?.length && (
            <FinanceNotice>
              No route rows exist for this request type. Routed requests fail with
              approval_route_unconfigured until every stage is published.
            </FinanceNotice>
          )}
          {list.data?.map((row) => (
            <article className="hr-repeat" key={row.id}>
              <div className="hr-repeat-heading">
                <h3>
                  {row.code} &middot; stage {row.stage} &middot; version {row.version}
                </h3>
                {latest.get(row.code)?.id === row.id && (
                  <span className="hr-status hr-status--ok">Latest</span>
                )}
              </div>
              <RecordView
                value={{
                  routeReference: row.id,
                  code: row.code,
                  version: row.version,
                  requestType: row.requestType,
                  approverPermission: row.approverPermission,
                  stage: row.stage,
                  effectiveFrom: row.effectiveFrom,
                  effectiveTo: row.effectiveTo,
                  staffCategoryId: row.staffCategoryId,
                  departmentId: row.departmentId,
                  siteId: row.siteId,
                  minimumAmount: row.minimumAmount,
                  maximumAmount: row.maximumAmount,
                  currency: row.currency,
                  minimumDurationMinutes: row.minimumDurationMinutes,
                  maximumDurationMinutes: row.maximumDurationMinutes,
                  riskCode: row.riskCode,
                }}
              />
            </article>
          ))}
        </ApprovalQueryState>
      </div>
      {canManage && <PublishRouteForm requestType={requestType} />}
    </>
  );
}

function PublishRouteForm({ requestType }: { requestType: string }) {
  const action = useFinanceAction();
  const [form, setForm] = useState({
    code: '',
    expectedLatestVersion: '0',
    stage: '1',
    effectiveFrom: '',
    effectiveTo: '',
    reason: '',
    staffCategoryId: '',
    departmentId: '',
    siteId: '',
    minimumAmount: '',
    maximumAmount: '',
    currency: '',
    minimumDurationMinutes: '',
    maximumDurationMinutes: '',
    riskCode: '',
  });
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  function set(key: keyof typeof form) {
    return (event: { target: { value: string } }) =>
      setForm((previous) => ({ ...previous, [key]: event.target.value }));
  }
  async function submit(event: FormEvent) {
    event.preventDefault();
    const errors: Record<string, string> = {};
    if (!codePattern.test(form.code))
      errors.code = 'Use 1-30 letters, digits, dot, dash or underscore.';
    const version = Number(form.expectedLatestVersion);
    if (!Number.isInteger(version) || version < 0)
      errors.expectedLatestVersion =
        'Enter the current latest version as a whole number (0 for a new code).';
    const stage = Number(form.stage);
    if (!Number.isInteger(stage) || stage < 1 || stage > 20)
      errors.stage = 'Choose a stage from 1 to 20.';
    if (!form.effectiveFrom) errors.effectiveFrom = 'An effective from date is required.';
    if (form.effectiveTo && form.effectiveFrom && form.effectiveTo < form.effectiveFrom)
      errors.effectiveTo = 'The effective to date cannot be before the effective from date.';
    if (!form.reason.trim()) errors.reason = 'A reason is required.';
    else if (form.reason.length > 1000)
      errors.reason = 'The reason must be at most 1000 characters.';
    for (const key of ['staffCategoryId', 'departmentId', 'siteId'] as const) {
      if (form[key] && !uuidPattern.test(form[key]))
        errors[key] = 'Enter a valid identifier or leave the field empty.';
    }
    const minimum = form.minimumAmount ? Number(form.minimumAmount) : null;
    const maximum = form.maximumAmount ? Number(form.maximumAmount) : null;
    if (
      (minimum !== null && (!Number.isFinite(minimum) || minimum < 0)) ||
      (maximum !== null && (!Number.isFinite(maximum) || maximum < 0))
    )
      errors.minimumAmount = 'Amount bounds must be non-negative numbers.';
    else if (minimum !== null && maximum !== null && maximum < minimum)
      errors.maximumAmount = 'The maximum amount cannot be less than the minimum amount.';
    if (form.currency && !/^[A-Za-z]{3}$/.test(form.currency))
      errors.currency = 'Use a three-letter currency code or leave the field empty.';
    for (const key of ['minimumDurationMinutes', 'maximumDurationMinutes'] as const) {
      if (form[key] && (!Number.isInteger(Number(form[key])) || Number(form[key]) < 0))
        errors[key] = 'Enter a whole number of minutes or leave the field empty.';
    }
    setFieldErrors(errors);
    if (Object.keys(errors).length) return;
    await action.run(
      '/approvals/routes',
      {
        code: form.code.trim(),
        expectedLatestVersion: version,
        requestType,
        approverPermission: stagePermission(stage),
        stage,
        effectiveFrom: form.effectiveFrom,
        effectiveTo: form.effectiveTo || null,
        reason: form.reason.trim(),
        staffCategoryId: form.staffCategoryId || null,
        departmentId: form.departmentId || null,
        siteId: form.siteId || null,
        minimumAmount: minimum,
        maximumAmount: maximum,
        currency: form.currency ? form.currency.toUpperCase() : null,
        minimumDurationMinutes: form.minimumDurationMinutes
          ? Number(form.minimumDurationMinutes)
          : null,
        maximumDurationMinutes: form.maximumDurationMinutes
          ? Number(form.maximumDurationMinutes)
          : null,
        riskCode: form.riskCode || null,
      },
      'POST',
      approvalRoute,
    );
  }
  return (
    <div className="hr-form-section">
      <h2>Publish a route stage</h2>
      <p className="muted">
        Publishing requires the fixed SuperAdmin role holding approvals.routes.manage. The approver
        permission is derived from the stage number. Enter the current latest version of the code so
        a concurrent publish fails instead of overwriting.
      </p>
      <form onSubmit={(event) => void submit(event)}>
        <div className="hr-form-grid">
          <TextInput
            label="Code"
            required
            maxLength={30}
            value={form.code}
            error={fieldErrors.code}
            onChange={set('code')}
          />
          <TextInput
            label="Expected latest version"
            required
            type="number"
            min={0}
            value={form.expectedLatestVersion}
            error={fieldErrors.expectedLatestVersion}
            onChange={set('expectedLatestVersion')}
          />
          <SelectInput
            label="Stage"
            required
            value={form.stage}
            error={fieldErrors.stage}
            onChange={set('stage')}
            options={stageOptions}
          />
          <TextInput
            label="Effective from"
            required
            type="date"
            value={form.effectiveFrom}
            error={fieldErrors.effectiveFrom}
            onChange={set('effectiveFrom')}
          />
          <TextInput
            label="Effective to"
            type="date"
            value={form.effectiveTo}
            error={fieldErrors.effectiveTo}
            onChange={set('effectiveTo')}
          />
          <TextInput
            label="Staff category id"
            value={form.staffCategoryId}
            error={fieldErrors.staffCategoryId}
            onChange={set('staffCategoryId')}
          />
          <TextInput
            label="Department id"
            value={form.departmentId}
            error={fieldErrors.departmentId}
            onChange={set('departmentId')}
          />
          <TextInput
            label="Site id"
            value={form.siteId}
            error={fieldErrors.siteId}
            onChange={set('siteId')}
          />
          <TextInput
            label="Minimum amount"
            type="number"
            min={0}
            step="0.01"
            value={form.minimumAmount}
            error={fieldErrors.minimumAmount}
            onChange={set('minimumAmount')}
          />
          <TextInput
            label="Maximum amount"
            type="number"
            min={0}
            step="0.01"
            value={form.maximumAmount}
            error={fieldErrors.maximumAmount}
            onChange={set('maximumAmount')}
          />
          <TextInput
            label="Currency"
            maxLength={3}
            value={form.currency}
            error={fieldErrors.currency}
            onChange={set('currency')}
          />
          <TextInput
            label="Minimum duration (minutes)"
            type="number"
            min={0}
            value={form.minimumDurationMinutes}
            error={fieldErrors.minimumDurationMinutes}
            onChange={set('minimumDurationMinutes')}
          />
          <TextInput
            label="Maximum duration (minutes)"
            type="number"
            min={0}
            value={form.maximumDurationMinutes}
            error={fieldErrors.maximumDurationMinutes}
            onChange={set('maximumDurationMinutes')}
          />
          <TextInput label="Risk code" value={form.riskCode} onChange={set('riskCode')} />
        </div>
        <TextAreaField
          label="Reason"
          required
          rows={3}
          value={form.reason}
          error={fieldErrors.reason}
          onChange={(value) => setForm((previous) => ({ ...previous, reason: value }))}
        />
        <ApprovalActionMessage action={action} operation="publishRoute" />
        <div className="hr-actions">
          <Button type="submit" disabled={action.pending}>
            {action.pending ? 'Publishing...' : 'Publish stage'}
          </Button>
        </div>
      </form>
    </div>
  );
}

function ApprovalDelegations() {
  const action = useFinanceAction();
  const list = useFinanceQuery('/approvals/delegations', approvalDelegationList, true);
  const [form, setForm] = useState({
    delegateAccountId: '',
    requestType: 'allowance',
    stage: '1',
    scopeKind: '7',
    targetId: '',
    startsAt: '',
    endsAt: '',
    reason: '',
  });
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [revoking, setRevoking] = useState<string | null>(null);
  const [revokeReason, setRevokeReason] = useState('');
  function set(key: keyof typeof form) {
    return (event: { target: { value: string } }) =>
      setForm((previous) => ({ ...previous, [key]: event.target.value }));
  }
  async function grant(event: FormEvent) {
    event.preventDefault();
    const errors: Record<string, string> = {};
    if (!uuidPattern.test(form.delegateAccountId))
      errors.delegateAccountId = 'Enter the delegate account identifier.';
    if (form.targetId && !uuidPattern.test(form.targetId))
      errors.targetId = 'Enter a valid scope target or leave the field empty.';
    if (!form.startsAt) errors.startsAt = 'A start time is required.';
    if (!form.endsAt) errors.endsAt = 'An end time is required.';
    if (form.startsAt && form.endsAt && new Date(form.endsAt) <= new Date(form.startsAt))
      errors.endsAt = 'The end time must be after the start time.';
    if (!form.reason.trim()) errors.reason = 'A reason is required.';
    else if (form.reason.length > 1000)
      errors.reason = 'The reason must be at most 1000 characters.';
    setFieldErrors(errors);
    if (Object.keys(errors).length) return;
    await action.run(
      '/approvals/delegations',
      {
        delegateAccountId: form.delegateAccountId,
        requestType: form.requestType,
        permission: stagePermission(Number(form.stage)),
        scopeKind: Number(form.scopeKind),
        targetId: form.targetId || null,
        startsAt: new Date(form.startsAt).toISOString(),
        endsAt: new Date(form.endsAt).toISOString(),
        reason: form.reason.trim(),
      },
      'POST',
      approvalDelegation,
    );
  }
  async function revoke(delegation: ApprovalDelegation) {
    if (!revokeReason.trim() || revokeReason.length > 1000) {
      setFieldErrors({ revokeReason: 'Enter a reason up to 1000 characters before revoking.' });
      return;
    }
    setFieldErrors({});
    await action.run(
      `/approvals/delegations/${delegation.id}/revoke`,
      { expectedVersion: delegation.version, reason: revokeReason.trim() },
      'POST',
      approvalDelegation,
      () => setRevoking(null),
    );
    setRevokeReason('');
  }
  return (
    <>
      <div className="hr-form-section">
        <h2>My delegations</h2>
        <ApprovalQueryState query={list}>
          {!list.data?.length && (
            <FinanceNotice>No delegations have been granted from this account.</FinanceNotice>
          )}
          {list.data?.map((delegation) => (
            <article className="hr-repeat" key={delegation.id}>
              <div className="hr-repeat-heading">
                <h3>
                  {delegation.requestType} &middot; {delegation.permission}
                </h3>
                {delegation.revokedAt === null ? (
                  <span className="hr-status hr-status--ok">Active</span>
                ) : (
                  <span className="hr-status hr-status--neutral">Revoked</span>
                )}
              </div>
              <RecordView
                value={{
                  delegationReference: delegation.id,
                  delegatorAccountId: delegation.delegatorAccountId,
                  delegateAccountId: delegation.delegateAccountId,
                  requestType: delegation.requestType,
                  permission: delegation.permission,
                  scopeKind:
                    scopeKindOptions.find((s) => Number(s.value) === delegation.scopeKind)?.label ??
                    delegation.scopeKind,
                  targetId: delegation.targetId,
                  startsAt: labelDate(delegation.startsAt),
                  endsAt: labelDate(delegation.endsAt),
                  revokedAt: delegation.revokedAt ? labelDate(delegation.revokedAt) : null,
                }}
              />
              {delegation.revokedAt === null && (
                <div className="hr-actions">
                  {revoking === delegation.id ? (
                    <>
                      <TextAreaField
                        label="Revoke reason"
                        rows={2}
                        value={revokeReason}
                        error={fieldErrors.revokeReason}
                        onChange={setRevokeReason}
                      />
                      <Button
                        variant="secondary"
                        onClick={() => void revoke(delegation)}
                        disabled={action.pending}
                      >
                        Confirm revoke
                      </Button>
                      <Button
                        variant="secondary"
                        onClick={() => {
                          setRevoking(null);
                          setRevokeReason('');
                        }}
                      >
                        Cancel
                      </Button>
                    </>
                  ) : (
                    <Button variant="secondary" onClick={() => setRevoking(delegation.id)}>
                      Revoke
                    </Button>
                  )}
                </div>
              )}
            </article>
          ))}
        </ApprovalQueryState>
        <ApprovalActionMessage action={action} operation="revokeDelegation" />
      </div>
      <div className="hr-form-section">
        <h2>Grant a delegation</h2>
        <p className="muted">
          You can delegate only the stage-review authority you currently hold. The delegate must be
          a different account with active, approved employment.
        </p>
        <form onSubmit={(event) => void grant(event)}>
          <div className="hr-form-grid">
            <TextInput
              label="Delegate account id"
              required
              value={form.delegateAccountId}
              error={fieldErrors.delegateAccountId}
              onChange={set('delegateAccountId')}
            />
            <SelectInput
              label="Request type"
              value={form.requestType}
              onChange={set('requestType')}
              options={approvalRequestTypeOptions}
            />
            <SelectInput
              label="Stage"
              value={form.stage}
              onChange={set('stage')}
              options={stageOptions}
            />
            <SelectInput
              label="Scope kind"
              value={form.scopeKind}
              onChange={set('scopeKind')}
              options={scopeKindOptions}
            />
            <TextInput
              label="Scope target id"
              value={form.targetId}
              error={fieldErrors.targetId}
              onChange={set('targetId')}
            />
            <TextInput
              label="Starts at"
              required
              type="datetime-local"
              value={form.startsAt}
              error={fieldErrors.startsAt}
              onChange={set('startsAt')}
            />
            <TextInput
              label="Ends at"
              required
              type="datetime-local"
              value={form.endsAt}
              error={fieldErrors.endsAt}
              onChange={set('endsAt')}
            />
          </div>
          <TextAreaField
            label="Reason"
            required
            rows={3}
            value={form.reason}
            error={fieldErrors.reason}
            onChange={(value) => setForm((previous) => ({ ...previous, reason: value }))}
          />
          <ApprovalActionMessage action={action} operation="grantDelegation" />
          <div className="hr-actions">
            <Button type="submit" disabled={action.pending}>
              {action.pending ? 'Granting...' : 'Grant delegation'}
            </Button>
          </div>
        </form>
      </div>
    </>
  );
}
