import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  ChevronLeft,
  ChevronRight,
  Eye,
  Plus,
  RefreshCw,
  Replace,
  Search,
  ShieldCheck,
  SlidersHorizontal,
  X,
} from 'lucide-react';
import { z } from 'zod';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import {
  Command,
  Denied,
  MutationForm,
  PrivateWorkspace,
  QueryState,
} from '../../components/SchedulingUi';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { queryString } from '../../api/attendance-api';
import { date, id, reason } from '../../schemas/common';
import { businessToday } from '../../utils/messages';
import {
  assignmentResult,
  code,
  financialRecommendations,
  policyAssignment,
  policyDefinition,
  policyPageOf,
  policyRules,
  policyVersion,
  publicationPreview,
  publicationResult,
  settingsValidation,
  type PolicyDefinition,
  type PolicyRules,
} from './contracts';
import { blankRules, canAll, normalizedRules } from './inputs';
import { Choice, EvidenceRecord, JsonEvidence, MorePages, Reload, RuleErrors } from './Ui';
import { PolicyRulesFields } from './PolicyRulesFields';
import { useServerPreview } from './preview';
import '@/styles/attendance-policies.css';

const root = '/hr/attendance-policies';
export function AttendancePoliciesPage() {
  return (
    <PrivateWorkspace resource="attendance-policies">
      <PolicyList />
    </PrivateWorkspace>
  );
}
export function AttendancePolicyDetailPage() {
  const { policyId = '' } = useParams();
  return (
    <PrivateWorkspace resource={`attendance-policy:${policyId}`}>
      <PolicyDetail policyId={policyId} />
    </PrivateWorkspace>
  );
}
function PolicyList() {
  const access = useAttendanceAccess();
  const read = access.ready && canAll(access.user, 'attendance.policies.configure');
  const publish = access.ready && canAll(access.user, 'attendance.policies.publish');
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [creating, setCreating] = useState(false);
  const query = useAttendanceQuery(
    root + queryString({ page, pageSize: 25, search }),
    policyPageOf(policyDefinition),
    read,
  );
  function reload() {
    setCreating(false);
    if (read) void query.refetch();
  }
  if (!read && !publish)
    return (
      <Denied>Attendance policies require their own supported configure or publish grant.</Denied>
    );

  const totalCount = query.data?.items.length ?? 0;

  return (
    <div className="attendance-policies-page">
      <header className="policy-page-header">
        <div className="policy-header-left">
          <p className="hr-kicker">HUMAN RESOURCES &bull; ATTENDANCE</p>
          <div className="policy-title-row">
            <SlidersHorizontal size={18} style={{ color: '#2563eb' }} />
            <h1>Attendance Policies</h1>
            {query.data && (
              <span className="policy-count-badge">
                {totalCount} {totalCount === 1 ? 'Definition' : 'Definitions'}
              </span>
            )}
          </div>
          <p className="policy-header-sub">
            Configure working hours, late arrival grace thresholds, half-day cuts, and penalty
            slabs.
          </p>
        </div>
        <div className="policy-header-actions">
          {read && (
            <Button
              variant="secondary"
              onClick={reload}
              disabled={creating || query.isFetching}
              className="policy-action-btn"
            >
              <RefreshCw size={13} className={query.isFetching ? 'spin' : ''} />
              <span>Reload</span>
            </Button>
          )}
          {publish && !creating && (
            <Button onClick={() => setCreating(true)} className="policy-action-btn">
              <Plus size={14} />
              <span>Create Definition</span>
            </Button>
          )}
        </div>
      </header>

      {creating && <CreatePolicyForm onSaved={reload} onCancel={reload} />}

      {read ? (
        <div className="policy-card-container">
          <div className="policy-toolbar">
            <div className="policy-search-wrap">
              <Search size={13} className="policy-search-icon" aria-hidden="true" />
              <input
                type="text"
                className="policy-search-input"
                placeholder="Search policies by code or title..."
                value={search}
                disabled={creating}
                onChange={(e) => {
                  setSearch(e.target.value);
                  setPage(1);
                }}
              />
              {search && (
                <button
                  type="button"
                  className="policy-search-clear"
                  onClick={() => {
                    setSearch('');
                    setPage(1);
                  }}
                  aria-label="Clear search"
                >
                  <X size={12} />
                </button>
              )}
            </div>
            <div className="policy-toolbar-meta">
              Showing page {page} &bull; {totalCount} records
            </div>
          </div>

          <QueryState query={query} empty={query.data?.items.length === 0} />

          {!query.isError && query.data && (
            <>
              <div className="policy-table-scroll">
                <table className="policy-table">
                  <thead>
                    <tr>
                      <th style={{ width: '240px' }}>Policy Code</th>
                      <th>Policy Name & Description</th>
                      <th style={{ width: '120px', textAlign: 'center' }}>Revision</th>
                      <th style={{ width: '110px', textAlign: 'right' }}>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {query.data.items.map((row) => (
                      <tr key={row.id}>
                        <td>
                          <span className="policy-code-pill">
                            <ShieldCheck size={13} />
                            {row.code}
                          </span>
                        </td>
                        <td>
                          <div className="policy-name-cell">
                            <span className="policy-name-title">{row.name}</span>
                          </div>
                        </td>
                        <td style={{ textAlign: 'center' }}>
                          <span className="policy-rev-pill">Rev {row.revision}</span>
                        </td>
                        <td style={{ textAlign: 'right' }}>
                          <button
                            type="button"
                            className="policy-open-btn"
                            disabled={creating}
                            onClick={() => navigate(`/hr/attendance/policies/${row.id}`)}
                          >
                            <Eye size={12} />
                            <span>Open</span>
                          </button>
                        </td>
                      </tr>
                    ))}
                    {query.data.items.length === 0 && (
                      <tr>
                        <td colSpan={4} className="policy-empty-box">
                          No attendance policies found matching your search.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>

              <div className="policy-pagination-bar">
                <span>Page {page}</span>
                <div className="policy-page-buttons">
                  <Button
                    variant="secondary"
                    className="policy-page-nav-btn"
                    disabled={creating || page === 1}
                    onClick={() => setPage(page - 1)}
                  >
                    <ChevronLeft size={13} /> Previous
                  </Button>
                  <Button
                    variant="secondary"
                    className="policy-page-nav-btn"
                    disabled={creating || !query.data.hasMore}
                    onClick={() => setPage(page + 1)}
                  >
                    Next <ChevronRight size={13} />
                  </Button>
                </div>
              </div>
            </>
          )}
        </div>
      ) : (
        <Denied>Policy history requires a separate All-scoped configure grant.</Denied>
      )}
    </div>
  );
}
function CreatePolicyForm({ onSaved, onCancel }: { onSaved: () => void; onCancel: () => void }) {
  const [inputCode, setCode] = useState('');
  const [name, setName] = useState('');
  const [inputReason, setReason] = useState('');
  return (
    <MutationForm
      title="Create policy definition"
      onSaved={onSaved}
      onCancel={onCancel}
      build={() => ({
        path: root,
        method: 'POST',
        schema: policyDefinition,
        body: {
          code: code.parse(inputCode),
          name: reason(200).parse(name),
          reason: reason().parse(inputReason),
        },
      })}
    >
      <div className="attendance-grid">
        <TextInput
          label="Code"
          required
          maxLength={40}
          value={inputCode}
          onChange={(e) => setCode(e.target.value)}
        />
        <TextInput
          label="Name"
          required
          maxLength={200}
          value={name}
          onChange={(e) => setName(e.target.value)}
        />
      </div>
      <TextInput
        label="Reason"
        required
        maxLength={1000}
        value={inputReason}
        onChange={(e) => setReason(e.target.value)}
      />
    </MutationForm>
  );
}
function PolicyDetail({ policyId }: { policyId: string }) {
  const access = useAttendanceAccess();
  const allowed = access.ready && canAll(access.user, 'attendance.policies.configure');
  const publish = access.ready && canAll(access.user, 'attendance.policies.publish');
  const [versionPage, setVersionPage] = useState(1);
  const [assignmentPage, setAssignmentPage] = useState(1);
  const [form, setForm] = useState('');
  const [epoch, setEpoch] = useState(0);
  const [copied, setCopied] = useState<PolicyRules | null>(null);
  const valid = id.safeParse(policyId).success;
  const detail = useAttendanceQuery(`${root}/${policyId}`, policyDefinition, allowed && valid);
  const versions = useAttendanceQuery(
    `${root}/${policyId}/versions` + queryString({ page: versionPage, pageSize: 25 }),
    policyPageOf(policyVersion),
    allowed && valid,
  );
  const assignments = useAttendanceQuery(
    `${root}/${policyId}/assignments` + queryString({ page: assignmentPage, pageSize: 25 }),
    policyPageOf(policyAssignment),
    allowed && valid,
  );
  function reload() {
    setForm('');
    setCopied(null);
    setEpoch((e) => e + 1);
    void detail.refetch();
    void versions.refetch();
    void assignments.refetch();
  }
  if (!allowed)
    return <Denied>Policy detail/history requires its own All-scoped configure grant.</Denied>;
  if (!valid) return <p role="alert">Invalid policy ID.</p>;
  const policy = detail.isError || detail.isFetching ? undefined : detail.data;
  const assignment = assignments.data?.items.find((a) => form.endsWith(a.id));
  return (
    <>
      <div className="attendance-heading">
        <h1>{policy?.name ?? 'Attendance policy'}</h1>
        <Reload
          onClick={reload}
          disabled={
            Boolean(form) || detail.isFetching || versions.isFetching || assignments.isFetching
          }
        />
      </div>
      <QueryState query={detail} />
      {policy && (
        <>
          <EvidenceRecord value={policy} />
          {form === 'publish' ? (
            <PublishPolicyForm
              key={`${policy.version}:${policy.revision}:${epoch}`}
              policy={policy}
              initial={copied ?? undefined}
              mayPublish={publish}
              onSaved={reload}
              onCancel={reload}
            />
          ) : form && publish ? (
            <AssignmentForm
              key={`${policy.version}:${form}:${epoch}`}
              policy={policy}
              mode={
                form.startsWith('end:') ? 'end' : form.startsWith('replace:') ? 'replace' : 'assign'
              }
              assignment={assignment}
              onSaved={reload}
              onCancel={reload}
            />
          ) : (
            <div className="attendance-commands">
              <Command
                icon={Plus}
                onClick={() => {
                  setCopied(null);
                  setForm('publish');
                }}
              >
                Configure and preview version
              </Command>
              {publish && (
                <Command icon={Plus} onClick={() => setForm('assign')}>
                  Assign published version
                </Command>
              )}
            </div>
          )}
          <section className="attendance-section">
            <h2>Version history</h2>
            <QueryState query={versions} empty={versions.data?.items.length === 0} />
            {!versions.isError && versions.data && (
              <>
                {versions.data.items.map((version) => {
                  let parsed: { success: true; data: PolicyRules } | { success: false } | undefined;
                  try {
                    parsed = policyRules.safeParse(JSON.parse(version.canonicalJson) as unknown);
                  } catch {
                    parsed = undefined;
                  }
                  return (
                    <details key={version.id}>
                      <summary>
                        Version {version.number}: {version.effectiveFrom} to{' '}
                        {version.effectiveTo ?? 'open'}
                      </summary>
                      <EvidenceRecord
                        value={{
                          versionId: version.id,
                          configurationSha256: version.configurationSha256,
                          publishedAt: version.publishedAt,
                          authority: version.authority,
                          publishedByUserId: version.publishedByUserId,
                        }}
                      />
                      <JsonEvidence json={version.canonicalJson} />
                      {!form && parsed?.success && (
                        <Command
                          icon={Replace}
                          variant="secondary"
                          onClick={() => {
                            if (parsed?.success) {
                              setCopied(parsed.data);
                              setForm('publish');
                            }
                          }}
                        >
                          Use settings for a new review
                        </Command>
                      )}
                    </details>
                  );
                })}
                <MorePages
                  page={versionPage}
                  hasMore={versions.data.hasMore}
                  disabled={Boolean(form) || versions.isFetching}
                  onChange={setVersionPage}
                />
              </>
            )}
          </section>
          <section className="attendance-section">
            <h2>Dated assignment and ending history</h2>
            <QueryState query={assignments} empty={assignments.data?.items.length === 0} />
            {!assignments.isError && assignments.data && (
              <>
                {assignments.data.items.map((row) => (
                  <details key={row.id}>
                    <summary>
                      {row.target}: {row.targetId}, {row.effectiveFrom} to{' '}
                      {row.effectiveTo ?? 'open'}
                    </summary>
                    <EvidenceRecord value={row} />
                    {publish && !form && !row.endId && row.target !== 'role' && (
                      <div className="attendance-commands">
                        <Command icon={X} onClick={() => setForm(`end:${row.id}`)}>
                          End future period
                        </Command>
                        <Command icon={Replace} onClick={() => setForm(`replace:${row.id}`)}>
                          Replace future period
                        </Command>
                      </div>
                    )}
                  </details>
                ))}
                <MorePages
                  page={assignmentPage}
                  hasMore={assignments.data.hasMore}
                  disabled={assignments.isFetching || Boolean(form)}
                  onChange={setAssignmentPage}
                />
              </>
            )}
          </section>
        </>
      )}
    </>
  );
}
function PublishPolicyForm({
  policy,
  initial,
  mayPublish,
  onSaved,
  onCancel,
}: {
  policy: PolicyDefinition;
  initial?: PolicyRules;
  mayPublish: boolean;
  onSaved: () => void;
  onCancel: () => void;
}) {
  const [source] = useState(policy);
  const [rules, setRules] = useState(initial ?? blankRules);
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [inputReason, setReason] = useState('');
  const [recommend, setRecommend] = useState(false);
  const suggestions = useAttendanceQuery(
    root + '/recommended-financial-settings',
    financialRecommendations,
    recommend,
  );
  const preview = useServerPreview(`${root}/${source.id}/publication-preview`, publicationPreview);
  const validate = useServerPreview(root + '/validate', settingsValidation);
  const [checked, setChecked] = useState<{
    signature: string;
    value: z.infer<typeof publicationPreview>;
  } | null>(null);
  const [validation, setValidation] = useState<{
    signature: string;
    value: z.infer<typeof settingsValidation>;
  } | null>(null);
  const [error, setError] = useState('');
  const rulesSignature = JSON.stringify(rules);
  const signature = JSON.stringify([rules, from, to]);
  const currentPreview = checked?.signature === signature ? checked.value : null;
  function proposal() {
    date.parse(from);
    if (to) date.parse(to);
    return {
      expectedVersion: source.version,
      expectedRevision: source.revision,
      effectiveFrom: from,
      effectiveTo: to || null,
      rules: normalizedRules(rules),
    };
  }
  async function check(publication: boolean) {
    setError('');
    try {
      if (publication) {
        const result = await preview.run(proposal());
        if (result) setChecked({ signature, value: result });
      } else {
        const result = await validate.run({ rules: normalizedRules(rules) });
        if (result) setValidation({ signature: rulesSignature, value: result });
      }
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Check the explicit settings.');
    }
  }
  return (
    <MutationForm
      title="Review attendance policy publication"
      onSaved={onSaved}
      onCancel={onCancel}
      submitLabel="Publish directly"
      disabled={!mayPublish || !currentPreview?.canPublish || preview.pending || validate.pending}
      build={() => {
        if (
          !currentPreview?.canPublish ||
          !currentPreview.canonicalJson ||
          currentPreview.revision !== source.revision ||
          currentPreview.version !== source.version
        )
          throw new Error('Reload and obtain a current publication preview.');
        return {
          path: `${root}/${source.id}/versions`,
          method: 'POST',
          schema: publicationResult,
          body: {
            ...proposal(),
            expectedPreviewFingerprint: currentPreview.previewFingerprint,
            reason: reason().parse(inputReason),
          },
        };
      }}
    >
      <fieldset disabled={preview.pending || validate.pending}>
        <div className="attendance-grid">
          <TextInput
            label="Future effective from"
            type="date"
            required
            value={from}
            onChange={(e) => setFrom(e.target.value)}
          />
          <TextInput
            label="Exclusive end (optional)"
            type="date"
            value={to}
            onChange={(e) => setTo(e.target.value)}
          />
        </div>
        <PolicyRulesFields rules={rules} onChange={setRules} />
        <div className="attendance-commands">
          <Command icon={Eye} variant="secondary" onClick={() => setRecommend(true)}>
            Review server financial suggestions
          </Command>
          {recommend && (
            <Reload onClick={() => void suggestions.refetch()} disabled={suggestions.isFetching} />
          )}
        </div>
        {recommend && (
          <>
            <QueryState query={suggestions} />
            {!suggestions.isError && suggestions.data && (
              <>
                <EvidenceRecord value={suggestions.data} />
                <Command
                  icon={Replace}
                  variant="secondary"
                  onClick={() => {
                    if (suggestions.data)
                      setRules((current) => ({
                        ...current,
                        ordinaryPay: suggestions.data.ordinaryPay,
                        lateDeduction: {
                          ...current.lateDeduction,
                          policyChangeCountingStrategy:
                            suggestions.data.policyChangeCountingStrategy,
                          salaryEffectiveDateBasis: suggestions.data.salaryEffectiveDateBasis,
                          overlapTreatment: suggestions.data.overlapTreatment,
                        },
                      }));
                  }}
                >
                  Use reviewed suggestions
                </Command>
              </>
            )}
          </>
        )}
        <TextInput
          label="Publication reason"
          maxLength={1000}
          required
          value={inputReason}
          onChange={(e) => setReason(e.target.value)}
        />
        <div className="attendance-commands">
          <Command icon={Eye} variant="secondary" onClick={() => void check(false)}>
            Validate settings
          </Command>
          <Command icon={Eye} onClick={() => void check(true)}>
            Preview publication
          </Command>
        </div>
      </fieldset>
      {(error || preview.error || validate.error) && (
        <p role="alert" className="attendance-error">
          {error || preview.error || validate.error}
        </p>
      )}
      {validation?.signature === rulesSignature && (
        <>
          <RuleErrors errors={validation.value.errors} />
          <EvidenceRecord
            value={{
              isValid: validation.value.isValid,
              published: validation.value.published,
              configurationSha256: validation.value.configurationSha256,
            }}
          />
          {validation.value.canonicalJson && <JsonEvidence json={validation.value.canonicalJson} />}
        </>
      )}
      {currentPreview && (
        <>
          <RuleErrors errors={currentPreview.errors} />
          <EvidenceRecord
            value={{
              canPublish: currentPreview.canPublish,
              revision: currentPreview.revision,
              version: currentPreview.version,
              previewFingerprint: currentPreview.previewFingerprint,
              configurationSha256: currentPreview.configurationSha256,
              affectsBaseSalary: currentPreview.affectsBaseSalary,
            }}
          />
          {currentPreview.canonicalJson && <JsonEvidence json={currentPreview.canonicalJson} />}
        </>
      )}
      {!mayPublish && (
        <Denied>Direct publication requires a separate All-scoped publish grant.</Denied>
      )}
    </MutationForm>
  );
}
function AssignmentForm({
  policy,
  mode,
  assignment,
  onSaved,
  onCancel,
}: {
  policy: PolicyDefinition;
  mode: 'assign' | 'end' | 'replace';
  assignment?: z.infer<typeof policyAssignment>;
  onSaved: () => void;
  onCancel: () => void;
}) {
  const [source] = useState(policy);
  const [original] = useState(assignment);
  const [page, setPage] = useState(1);
  const [version, setVersion] = useState<z.infer<typeof policyVersion> | null>(null);
  const versions = useAttendanceQuery(
    `${root}/${source.id}/versions` + queryString({ page, pageSize: 25 }),
    policyPageOf(policyVersion),
    mode !== 'end',
  );
  const [target, setTarget] = useState('');
  const [targetId, setTargetId] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [inputReason, setReason] = useState('');
  function build() {
    if (mode !== 'assign' && !original) throw new Error('Reload the original assignment.');
    const body = {
      expectedVersion: source.version,
      expectedRevision: source.revision,
      reason: reason().parse(inputReason),
    };
    const base = `${root}/${source.id}/assignments`;
    if (mode === 'end') {
      date.parse(to);
      if (
        !original ||
        to <= businessToday() ||
        to <= original.effectiveFrom ||
        (original.effectiveTo && to >= original.effectiveTo)
      )
        throw new Error('Choose a future boundary that shortens the original period.');
      return {
        path: `${base}/${original.id}/end`,
        method: 'POST' as const,
        schema: assignmentResult,
        body: { ...body, effectiveTo: to },
      };
    }
    date.parse(from);
    if (to) date.parse(to);
    if (from <= businessToday() || (to && to <= from))
      throw new Error('Use a future start and a later exclusive end.');
    if (!version || version.authority !== 'DirectHr')
      throw new Error('Select a directly published version.');
    if (from < version.effectiveFrom || (version.effectiveTo && (!to || to > version.effectiveTo)))
      throw new Error('Assignment must fit within the selected version.');
    if (
      mode === 'replace' &&
      original &&
      (from <= original.effectiveFrom || (original.effectiveTo && from >= original.effectiveTo))
    )
      throw new Error('Replacement must shorten the original future period.');
    return {
      path: base + (mode === 'replace' ? `/${original!.id}/replace` : ''),
      method: 'POST' as const,
      schema: assignmentResult,
      body: {
        ...body,
        policyVersionId: version.id,
        effectiveFrom: from,
        effectiveTo: to || null,
        ...(mode === 'assign'
          ? {
              target: z.enum(['employee', 'department', 'staff_category', 'site']).parse(target),
              targetId: id.parse(targetId),
            }
          : {}),
      },
    };
  }
  const options = (versions.data?.items ?? [])
    .filter((v) => v.authority === 'DirectHr')
    .map((v) => ({
      value: v.id,
      label: `Version ${v.number}: ${v.effectiveFrom} to ${v.effectiveTo ?? 'open'}`,
    }));
  if (version && !options.some((v) => v.value === version.id))
    options.push({ value: version.id, label: `Selected version ${version.number}` });
  return (
    <MutationForm
      title={
        mode === 'assign'
          ? 'Assign attendance policy'
          : mode === 'end'
            ? 'End policy assignment'
            : 'Replace policy assignment'
      }
      build={build}
      onSaved={onSaved}
      onCancel={onCancel}
    >
      {original && <EvidenceRecord value={original} />}
      {mode !== 'end' && (
        <>
          <QueryState query={versions} />
          <Choice
            label="Immutable policy version"
            value={version?.id ?? ''}
            options={options}
            onChange={(value) =>
              setVersion(versions.data?.items.find((v) => v.id === value) ?? null)
            }
          />
          {versions.data && (
            <MorePages
              page={page}
              hasMore={versions.data.hasMore}
              disabled={versions.isFetching}
              onChange={setPage}
            />
          )}{' '}
          {version && <JsonEvidence json={version.canonicalJson} />}
          {mode === 'assign' && (
            <>
              <Choice
                label="Population"
                value={target}
                options={['employee', 'department', 'staff_category', 'site']}
                onChange={(value) => {
                  setTarget(value);
                  setTargetId('');
                }}
              />
              <TextInput
                label="Explicit target ID"
                required
                value={targetId}
                onChange={(e) => setTargetId(e.target.value)}
              />
            </>
          )}
          <TextInput
            label="Future effective from"
            type="date"
            required
            value={from}
            onChange={(e) => setFrom(e.target.value)}
          />
        </>
      )}
      <TextInput
        label={mode === 'end' ? 'Future exclusive end' : 'Exclusive end (optional)'}
        type="date"
        required={mode === 'end'}
        value={to}
        onChange={(e) => setTo(e.target.value)}
      />
      <TextInput
        label="Reason"
        maxLength={1000}
        required
        value={inputReason}
        onChange={(e) => setReason(e.target.value)}
      />
    </MutationForm>
  );
}
