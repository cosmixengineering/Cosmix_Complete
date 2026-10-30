import { useMemo, useState, type FormEvent } from 'react';
import {
  GitFork,
  ShieldCheck,
  Layers,
  Calendar,
  Info,
  PlusCircle,
  UserCheck,
  Send,
  AlertCircle,
  AlertTriangle,
  CheckCircle2,
  Clock,
  Workflow,
  Users,
} from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { Button } from '@/components/ui/Button';
import { Denied } from '../../hr/components/Feedback';
import { RecordView } from '../../hr/components/RecordView';
import { TextAreaField } from '../../hr/components/finance/FinanceChrome';
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
import '@/styles/approval-routing.css';

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
    ...(access.canRead || access.canManage ? [{ id: 'routes', label: 'Approval routes' }] : []),
    ...(access.canDelegate ? [{ id: 'delegations', label: 'Delegations' }] : []),
  ];
  const [tab, setTab] = useState(tabs[0]?.id ?? '');
  if (!tabs.length) return <Denied />;
  const active = tabs.some((item) => item.id === tab) ? tab : (tabs[0]?.id ?? '');

  return (
    <section aria-label="Approval routing" className="approval-routing-container">
      {/* Elevated Hero Banner */}
      <div className="approval-hero-banner">
        <div className="approval-hero-top">
          <div className="approval-hero-title">
            <span className="approval-kicker">HUMAN RESOURCES &bull; GOVERNANCE & COMPLIANCE</span>
            <h1>
              <GitFork size={16} className="approval-title-icon" />
              Approval Routing
            </h1>
            <p>
              Configure multi-stage approval pipelines, immutable route versions, and temporary
              review authority delegations.
            </p>
          </div>
          <div className="approval-hero-badges">
            <span className="approval-badge approval-badge--mfa">
              <ShieldCheck size={11} />
              MFA Enforced
            </span>
            <span className="approval-badge approval-badge--neutral">All-Scope Governance</span>
          </div>
        </div>

        {/* Tab switcher */}
        <div className="approval-nav-tabs" role="tablist" aria-label="Approval routing views">
          {tabs.map((item) => (
            <button
              type="button"
              key={item.id}
              role="tab"
              aria-selected={active === item.id}
              className={`approval-tab-btn ${active === item.id ? 'active' : ''}`}
              onClick={() => setTab(item.id)}
            >
              {item.id === 'routes' ? <GitFork size={13} /> : <UserCheck size={13} />}
              {item.label}
            </button>
          ))}
        </div>
      </div>

      {active === 'routes' && (
        <ApprovalRoutes canRead={access.canRead} canManage={access.canManage} />
      )}
      {active === 'delegations' && <ApprovalDelegations />}
    </section>
  );
}

function ApprovalRoutes({ canRead, canManage }: { canRead: boolean; canManage: boolean }) {
  const [requestType, setRequestType] = useState('allowance');
  const list = useFinanceQuery(
    `/approvals/routes?requestType=${encodeURIComponent(requestType)}`,
    approvalRouteList,
    canRead,
  );
  const latest = useMemo(() => effectiveLatestPerCode(list.data ?? []), [list.data]);

  // Compute pipeline stage analysis
  const configuredStages = useMemo(() => {
    const map = new Map<number, ApprovalRoute>();
    if (!list.data) return map;
    for (const row of list.data) {
      const existing = map.get(row.stage);
      if (!existing || row.version > existing.version) {
        map.set(row.stage, row);
      }
    }
    return map;
  }, [list.data]);

  const maxStage = useMemo(() => {
    let max = 0;
    for (const stage of configuredStages.keys()) {
      if (stage > max) max = stage;
    }
    return max;
  }, [configuredStages]);

  const isContiguous = useMemo(() => {
    if (maxStage === 0) return false;
    for (let i = 1; i <= maxStage; i++) {
      if (!configuredStages.has(i)) return false;
    }
    return true;
  }, [configuredStages, maxStage]);

  return (
    <>
      <div className="approval-card">
        <div className="approval-card-header">
          <div className="approval-card-title-group">
            <h2 className="approval-card-title">
              <Workflow size={15} />
              Configured Route Pipeline
            </h2>
            <p className="approval-card-sub">
              Active multi-stage pipeline definitions for current request types.
            </p>
          </div>
          <div className="approval-card-toolbar">
            <div className="approval-type-picker">
              <label htmlFor="route-request-type">Request type:</label>
              <SelectInput
                id="route-request-type"
                value={requestType}
                onChange={(event) => setRequestType(event.target.value)}
                options={approvalRequestTypeOptions}
                compact
              />
            </div>
          </div>
        </div>

        {/* Quick Category Selection Pills */}
        <div className="approval-category-pills">
          {approvalRequestTypeOptions.map((opt) => (
            <button
              type="button"
              key={opt.value}
              className={`approval-cat-pill ${requestType === opt.value ? 'active' : ''}`}
              onClick={() => setRequestType(opt.value)}
            >
              {opt.label}
            </button>
          ))}
        </div>

        {/* Visual Pipeline Ribbon */}
        <div className="approval-pipeline-ribbon">
          <div className="approval-pipeline-status">
            <span className="approval-ribbon-label">Pipeline Architecture:</span>
            {maxStage === 0 ? (
              <span className="approval-status-pill approval-status--unconfigured">
                <AlertCircle size={10} /> Unconfigured (0 Stages)
              </span>
            ) : isContiguous ? (
              <span className="approval-status-pill approval-status--active">
                <CheckCircle2 size={10} /> Routable ({maxStage} Contiguous Stages)
              </span>
            ) : (
              <span className="approval-status-pill approval-status--broken">
                <AlertTriangle size={10} /> Incomplete (Gaps in 1..{maxStage})
              </span>
            )}
          </div>

          <div className="approval-pipeline-steps">
            {maxStage === 0 ? (
              <div className="approval-pipeline-step is-placeholder">
                <div className="approval-step-num">1</div>
                <div className="approval-step-info">
                  <span className="approval-step-title">Stage 1 Required</span>
                  <span className="approval-step-desc">Publish below to activate</span>
                </div>
              </div>
            ) : (
              Array.from({ length: maxStage }, (_, i) => i + 1).map((stageNum) => {
                const stageRow = configuredStages.get(stageNum);
                const exists = Boolean(stageRow);
                return (
                  <div
                    key={stageNum}
                    className={`approval-pipeline-step ${exists ? 'is-published' : 'is-missing'}`}
                  >
                    <div className="approval-step-num">{stageNum}</div>
                    <div className="approval-step-info">
                      <span className="approval-step-title">Stage {stageNum}</span>
                      <span className="approval-step-desc">
                        {exists ? `v${stageRow?.version} · ${stageRow?.code}` : 'Missing Stage'}
                      </span>
                    </div>
                  </div>
                );
              })
            )}
          </div>
        </div>

        <div className="approval-info-banner">
          <Info size={13} />
          <span>
            Each stage is published as an immutable version row. A request type becomes routable
            only when its stages form a contiguous 1..N sequence for the latest version.
          </span>
        </div>

        {canRead ? (
          <ApprovalQueryState query={list}>
            {!list.data?.length ? (
              <div className="approval-empty-box">
                <Layers size={22} className="approval-empty-icon" />
                <div className="approval-empty-text">
                  <h4>No route stages configured for &ldquo;{requestType}&rdquo;</h4>
                  <p>
                    Routed requests will fail with <code>approval_route_unconfigured</code> until
                    every stage (1..N) is published in the form below.
                  </p>
                </div>
              </div>
            ) : (
              <div className="approval-routes-list">
                {list.data.map((row) => {
                  const isLatest = latest.get(row.code)?.id === row.id;
                  return (
                    <article
                      className={`approval-route-card ${isLatest ? 'is-latest' : ''}`}
                      key={row.id}
                    >
                      <div className="approval-route-card-header">
                        <div className="approval-route-header-left">
                          <span className="approval-stage-badge">Stage {row.stage}</span>
                          <code className="approval-code-pill">{row.code}</code>
                          <span className="approval-version-pill">v{row.version}</span>
                          {isLatest ? (
                            <span className="approval-status-pill approval-status--latest">
                              Latest
                            </span>
                          ) : (
                            <span className="approval-status-pill approval-status--history">
                              History
                            </span>
                          )}
                        </div>
                        <div className="approval-route-header-right">
                          <span className="approval-date-pill">
                            <Calendar size={11} />
                            {row.effectiveFrom}{' '}
                            {row.effectiveTo ? `→ ${row.effectiveTo}` : '· Active'}
                          </span>
                        </div>
                      </div>

                      <div className="approval-route-props-grid">
                        <div className="approval-prop-item">
                          <span className="approval-prop-label">Approver Permission</span>
                          <span className="approval-perm-tag">{row.approverPermission}</span>
                        </div>

                        <div className="approval-prop-item">
                          <span className="approval-prop-label">Scope Limits</span>
                          <span className="approval-prop-val">
                            {row.siteId
                              ? `Site: ${row.siteId.slice(0, 8)}...`
                              : row.departmentId
                                ? `Dept: ${row.departmentId.slice(0, 8)}...`
                                : row.staffCategoryId
                                  ? `Cat: ${row.staffCategoryId.slice(0, 8)}...`
                                  : 'Global (All Units)'}
                          </span>
                        </div>

                        <div className="approval-prop-item">
                          <span className="approval-prop-label">Amount Threshold</span>
                          <span className="approval-prop-val">
                            {row.minimumAmount != null || row.maximumAmount != null ? (
                              `${row.currency || 'PKR'} ${row.minimumAmount ?? 0} — ${row.maximumAmount ?? '∞'}`
                            ) : (
                              <span className="approval-muted">Any Amount</span>
                            )}
                          </span>
                        </div>

                        <div className="approval-prop-item">
                          <span className="approval-prop-label">Duration / Risk</span>
                          <span className="approval-prop-val">
                            {row.minimumDurationMinutes != null ||
                            row.maximumDurationMinutes != null ? (
                              `${row.minimumDurationMinutes ?? 0}m – ${row.maximumDurationMinutes ?? '∞'}m`
                            ) : row.riskCode ? (
                              `Risk: ${row.riskCode}`
                            ) : (
                              <span className="approval-muted">Standard</span>
                            )}
                          </span>
                        </div>
                      </div>

                      <details className="approval-tech-details">
                        <summary>Audit & Technical Record</summary>
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
                      </details>
                    </article>
                  );
                })}
              </div>
            )}
          </ApprovalQueryState>
        ) : (
          <div className="approval-alert-banner">
            <AlertCircle size={13} />
            <span>Reading route history requires approval route read permission.</span>
          </div>
        )}
      </div>

      {canManage && <PublishRouteForm requestType={requestType} />}
    </>
  );
}

function PublishRouteForm({ requestType }: { requestType: string }) {
  const access = useApprovalAccess();
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
    else if (!access.canPublishStage(stage))
      errors.stage =
        'Publishing requires All-scope route management and review authority for this stage.';
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
    <div className="approval-card">
      <div className="approval-card-header">
        <div className="approval-card-title-group">
          <h2 className="approval-card-title">
            <PlusCircle size={15} />
            Publish Route Stage
          </h2>
          <p className="approval-card-sub">
            Publish an immutable stage version to advance or register the &ldquo;{requestType}
            &rdquo; approval pipeline.
          </p>
        </div>
      </div>

      <div className="approval-info-banner">
        <Info size={13} />
        <span>
          Publishing requires an active web session with MFA, <code>approvals.routes.manage</code>,
          and stage-review permission for the chosen stage.
        </span>
      </div>

      {!access.canPublishStage(Number(form.stage)) && (
        <div className="approval-alert-banner">
          <AlertCircle size={13} />
          <span>
            Your account needs All-scope route management and review authority for the selected
            stage before you can publish it.
          </span>
        </div>
      )}

      <form onSubmit={(event) => void submit(event)}>
        <div className="approval-form-grid">
          <div className="approval-form-section-divider">Pipeline Specification</div>

          <TextInput
            label="Code"
            required
            maxLength={30}
            value={form.code}
            error={fieldErrors.code}
            placeholder="e.g. ALLOWANCE_DEFAULT"
            onChange={set('code')}
          />
          <TextInput
            label="Expected latest version"
            required
            type="number"
            min={0}
            value={form.expectedLatestVersion}
            error={fieldErrors.expectedLatestVersion}
            placeholder="0 for new"
            onChange={set('expectedLatestVersion')}
          />
          <SelectInput
            label="Stage"
            required
            value={form.stage}
            error={fieldErrors.stage}
            onChange={set('stage')}
            options={stageOptions}
            compact
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

          <div className="approval-form-section-divider">
            Organizational Scope Constraints (Optional)
          </div>

          <TextInput
            label="Staff category ID"
            value={form.staffCategoryId}
            error={fieldErrors.staffCategoryId}
            placeholder="UUID (optional)"
            onChange={set('staffCategoryId')}
          />
          <TextInput
            label="Department ID"
            value={form.departmentId}
            error={fieldErrors.departmentId}
            placeholder="UUID (optional)"
            onChange={set('departmentId')}
          />
          <TextInput
            label="Site ID"
            value={form.siteId}
            error={fieldErrors.siteId}
            placeholder="UUID (optional)"
            onChange={set('siteId')}
          />

          <div className="approval-form-section-divider">
            Financial & Duration Thresholds (Optional)
          </div>

          <TextInput
            label="Minimum amount"
            type="number"
            min={0}
            step="0.01"
            value={form.minimumAmount}
            error={fieldErrors.minimumAmount}
            placeholder="0.00"
            onChange={set('minimumAmount')}
          />
          <TextInput
            label="Maximum amount"
            type="number"
            min={0}
            step="0.01"
            value={form.maximumAmount}
            error={fieldErrors.maximumAmount}
            placeholder="Optional ceiling"
            onChange={set('maximumAmount')}
          />
          <TextInput
            label="Currency"
            maxLength={3}
            value={form.currency}
            error={fieldErrors.currency}
            placeholder="PKR"
            onChange={set('currency')}
          />
          <TextInput
            label="Min duration (mins)"
            type="number"
            min={0}
            value={form.minimumDurationMinutes}
            error={fieldErrors.minimumDurationMinutes}
            placeholder="0"
            onChange={set('minimumDurationMinutes')}
          />
          <TextInput
            label="Max duration (mins)"
            type="number"
            min={0}
            value={form.maximumDurationMinutes}
            error={fieldErrors.maximumDurationMinutes}
            placeholder="No max"
            onChange={set('maximumDurationMinutes')}
          />
          <TextInput
            label="Risk code"
            value={form.riskCode}
            placeholder="e.g. HIGH_RISK"
            onChange={set('riskCode')}
          />
        </div>

        <TextAreaField
          label="Governance & Audit Reason"
          required
          rows={2}
          value={form.reason}
          error={fieldErrors.reason}
          onChange={(value) => setForm((previous) => ({ ...previous, reason: value }))}
        />

        <ApprovalActionMessage action={action} operation="publishRoute" />

        <div className="approval-form-actions">
          <Button
            type="submit"
            className="approval-submit-btn"
            disabled={action.pending || !access.canPublishStage(Number(form.stage))}
          >
            <Send size={12} />
            {action.pending ? 'Publishing...' : 'Publish Stage'}
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
      <div className="approval-card">
        <div className="approval-card-header">
          <div className="approval-card-title-group">
            <h2 className="approval-card-title">
              <UserCheck size={15} />
              Delegated Review Authority
            </h2>
            <p className="approval-card-sub">
              Active and historical delegations of stage review permissions granted by your account.
            </p>
          </div>
        </div>

        <ApprovalQueryState query={list}>
          {!list.data?.length ? (
            <div className="approval-empty-box">
              <Users size={22} className="approval-empty-icon" />
              <div className="approval-empty-text">
                <h4>No Delegations Active</h4>
                <p>No stage-review delegations have been granted from this account.</p>
              </div>
            </div>
          ) : (
            <div className="approval-routes-list">
              {list.data.map((delegation) => (
                <article className="approval-delegation-item" key={delegation.id}>
                  <div className="approval-delegation-header">
                    <div className="approval-route-header-left">
                      <span className="approval-stage-badge">{delegation.requestType}</span>
                      <span className="approval-perm-tag">{delegation.permission}</span>
                      {delegation.revokedAt === null ? (
                        <span className="approval-status-pill approval-status--latest">Active</span>
                      ) : (
                        <span className="approval-status-pill approval-status--history">
                          Revoked
                        </span>
                      )}
                    </div>
                    <div className="approval-route-header-right">
                      <span className="approval-date-pill">
                        <Clock size={11} />
                        {labelDate(delegation.startsAt)} &rarr; {labelDate(delegation.endsAt)}
                      </span>
                    </div>
                  </div>

                  <div className="approval-delegation-body">
                    <div className="approval-route-props-grid">
                      <div className="approval-prop-item">
                        <span className="approval-prop-label">Delegate Account</span>
                        <code className="approval-code-pill">{delegation.delegateAccountId}</code>
                      </div>
                      <div className="approval-prop-item">
                        <span className="approval-prop-label">Scope Kind</span>
                        <span className="approval-prop-val">
                          {scopeKindOptions.find((s) => Number(s.value) === delegation.scopeKind)
                            ?.label ?? delegation.scopeKind}
                        </span>
                      </div>
                      <div className="approval-prop-item">
                        <span className="approval-prop-label">Scope Target</span>
                        <span className="approval-prop-val">
                          {delegation.targetId ? (
                            delegation.targetId
                          ) : (
                            <span className="approval-muted">All Units</span>
                          )}
                        </span>
                      </div>
                      <div className="approval-prop-item">
                        <span className="approval-prop-label">Status Details</span>
                        <span className="approval-prop-val">
                          {delegation.revokedAt
                            ? `Revoked on ${labelDate(delegation.revokedAt)}`
                            : 'Active Authority'}
                        </span>
                      </div>
                    </div>

                    {delegation.revokedAt === null && (
                      <div className="approval-revoke-box">
                        {revoking === delegation.id ? (
                          <>
                            <TextInput
                              label="Revoke reason"
                              required
                              value={revokeReason}
                              error={fieldErrors.revokeReason}
                              placeholder="Reason for revoking delegation..."
                              onChange={(e) => setRevokeReason(e.target.value)}
                            />
                            <Button
                              variant="secondary"
                              className="approval-btn-sm"
                              onClick={() => void revoke(delegation)}
                              disabled={action.pending}
                            >
                              Confirm Revoke
                            </Button>
                            <Button
                              variant="secondary"
                              className="approval-btn-sm"
                              onClick={() => {
                                setRevoking(null);
                                setRevokeReason('');
                              }}
                            >
                              Cancel
                            </Button>
                          </>
                        ) : (
                          <Button
                            variant="secondary"
                            className="approval-btn-sm"
                            onClick={() => setRevoking(delegation.id)}
                          >
                            Revoke Delegation
                          </Button>
                        )}
                      </div>
                    )}
                  </div>
                </article>
              ))}
            </div>
          )}
        </ApprovalQueryState>
        <ApprovalActionMessage action={action} operation="revokeDelegation" />
      </div>

      <div className="approval-card">
        <div className="approval-card-header">
          <div className="approval-card-title-group">
            <h2 className="approval-card-title">
              <PlusCircle size={15} />
              Grant Authority Delegation
            </h2>
            <p className="approval-card-sub">
              Temporarily delegate your stage-review authority to an active, approved employee.
            </p>
          </div>
        </div>

        <form onSubmit={(event) => void grant(event)}>
          <div className="approval-form-grid">
            <TextInput
              label="Delegate account ID"
              required
              value={form.delegateAccountId}
              error={fieldErrors.delegateAccountId}
              placeholder="UUID of delegate account"
              onChange={set('delegateAccountId')}
            />
            <SelectInput
              label="Request type"
              value={form.requestType}
              onChange={set('requestType')}
              options={approvalRequestTypeOptions}
              compact
            />
            <SelectInput
              label="Stage"
              value={form.stage}
              onChange={set('stage')}
              options={stageOptions}
              compact
            />
            <SelectInput
              label="Scope kind"
              value={form.scopeKind}
              onChange={set('scopeKind')}
              options={scopeKindOptions}
              compact
            />
            <TextInput
              label="Scope target ID"
              value={form.targetId}
              error={fieldErrors.targetId}
              placeholder="UUID (optional)"
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
            rows={2}
            value={form.reason}
            error={fieldErrors.reason}
            onChange={(value) => setForm((previous) => ({ ...previous, reason: value }))}
          />

          <ApprovalActionMessage action={action} operation="grantDelegation" />

          <div className="approval-form-actions">
            <Button type="submit" className="approval-submit-btn" disabled={action.pending}>
              <Send size={12} />
              {action.pending ? 'Granting...' : 'Grant Delegation'}
            </Button>
          </div>
        </form>
      </div>
    </>
  );
}
