import { useEffect, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Eye, Plus, Send, ShieldCheck } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { ApiError } from '@/services/api/api-error';
import { TextAreaField, HasMorePager } from '../finance/FinanceChrome';
import { Denied, QueryState } from '../Feedback';
import { RecordView } from '../RecordView';
import { useFinanceAccess } from '../../hooks/use-finance-access';
import { useFinanceQuery } from '../../hooks/use-finance-queries';
import {
  inputError,
  requiredText,
  useVehiclePreview,
  vehicleManagementPermissions,
  vehicleManagementRoot,
  vehiclePolicy,
  vehiclePolicyPage,
  vehiclePolicyPreview,
  vehiclePolicyPublication,
  vehiclePolicyValidation,
  vehiclePolicyVersionPage,
} from '../../api/vehicle-management';
import { policyDraft, serializePolicyDraft } from '../../api/vehicle-policy-settings';
import { VehiclePolicyRulesEditor } from './VehiclePolicyRulesEditor';
import {
  useVehicleCommand,
  useVehicleNavigation,
  VehicleCommandStatus,
  VehicleRefresh,
} from './VehicleManagementShared';

const permission = vehicleManagementPermissions.policy;
const root = vehicleManagementRoot + '/policies';

export function VehiclePolicyManagement() {
  const access = useFinanceAccess();
  const cache = useQueryClient();
  const navigation = useVehicleNavigation();
  const allowed = access.can(permission);
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState('');
  const [form, setForm] = useState({ code: '', name: '', reason: '' });
  const [error, setError] = useState<string | null>(null);
  const query = useFinanceQuery(
    root + '?page=' + page + '&pageSize=20',
    vehiclePolicyPage,
    allowed,
  );
  const command = useVehicleCommand(permission);
  const denied =
    query.isError && query.error instanceof ApiError && [401, 403].includes(query.error.status);
  useEffect(() => {
    if (!denied || !selected) return;
    const selectedPath = root + '/' + selected;
    const filter = {
      queryKey: ['hr', 'finance', access.subject],
      predicate: (entry: { queryKey: readonly unknown[] }) =>
        entry.queryKey[3] === selectedPath ||
        (typeof entry.queryKey[3] === 'string' && entry.queryKey[3].startsWith(selectedPath + '/')),
    };
    setSelected('');
    void cache.cancelQueries(filter);
    cache.removeQueries(filter);
  }, [denied, selected, access.subject, cache]);
  async function create() {
    if (denied) return;
    setError(null);
    try {
      await command.run(
        root,
        {
          code: requiredText(form.code, 'Code', 60),
          name: requiredText(form.name, 'Name', 200),
          reason: requiredText(form.reason, 'Reason'),
        },
        vehiclePolicy,
        (result) => {
          setSelected(result.id);
          void query.refetch();
        },
      );
    } catch (failure) {
      setError(inputError(failure));
    }
  }
  if (!allowed) return <Denied />;
  return (
    <section>
      <div className="hr-repeat-heading">
        <h2>Vehicle policies</h2>
        <VehicleRefresh
          busy={query.isFetching || navigation.pending}
          onClick={() => navigation.navigate(() => void query.refetch())}
        />
      </div>
      <QueryState query={query}>
        {query.data?.items.map((policy) => (
          <div className="hr-repeat" key={policy.id}>
            <Button
              variant={selected === policy.id ? 'primary' : 'secondary'}
              disabled={navigation.pending}
              onClick={() => {
                if (selected !== policy.id) navigation.navigate(() => setSelected(policy.id));
              }}
            >
              {policy.code}: {policy.name}
            </Button>
            <span>
              {' '}
              Revision {policy.revision}; {policy.isActive ? 'Active' : 'Inactive'}
            </span>
          </div>
        ))}
        {query.data?.items.length === 0 && <p>No vehicle policies returned.</p>}
        {query.data && (
          <HasMorePager
            page={page}
            hasMore={query.data.hasMore}
            busy={query.isFetching || navigation.pending}
            onPrevious={() => navigation.navigate(() => setPage((value) => value - 1))}
            onNext={() => navigation.navigate(() => setPage((value) => value + 1))}
          />
        )}
      </QueryState>
      <details>
        <summary>Create policy definition</summary>
        <form
          onSubmit={(event) => {
            event.preventDefault();
            void create();
          }}
        >
          <fieldset className="hr-form-grid" disabled={command.locked || denied}>
            <legend>New definition</legend>
            <TextInput
              label="Code"
              required
              value={form.code}
              onChange={(event) => setForm({ ...form, code: event.target.value })}
            />
            <TextInput
              label="Name"
              required
              value={form.name}
              onChange={(event) => setForm({ ...form, name: event.target.value })}
            />
            <TextAreaField
              label="Reason"
              required
              value={form.reason}
              onChange={(reason) => setForm({ ...form, reason })}
            />
            <Button type="submit">
              <Plus size={15} /> Create definition
            </Button>
          </fieldset>
          {error && <p role="alert">{error}</p>}
          <VehicleCommandStatus command={command} />
        </form>
      </details>
      {selected && !query.isError && !query.isPending && (
        <VehiclePolicyWorkspace key={selected} policyId={selected} />
      )}
    </section>
  );
}

function VehiclePolicyWorkspace({ policyId }: { policyId: string }) {
  const access = useFinanceAccess();
  const allowed = access.can(permission);
  const [page, setPage] = useState(1);
  const [draft, setDraft] = useState<unknown>(() => policyDraft());
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const detail = useFinanceQuery(
    root + '/' + policyId,
    vehiclePolicy.refine((value) => value.id === policyId),
    allowed,
  );
  const versions = useFinanceQuery(
    root + '/' + policyId + '/versions?page=' + page + '&pageSize=20',
    vehiclePolicyVersionPage.refine((value) =>
      value.items.every((item) => item.policyId === policyId),
    ),
    allowed,
  );
  const validation = useVehiclePreview(permission, root + '/validate', vehiclePolicyValidation);
  const preview = useVehiclePreview(
    permission,
    root + '/' + policyId + '/publication-preview',
    vehiclePolicyPreview,
  );
  const command = useVehicleCommand(permission);
  function candidate() {
    if (!detail.data || detail.isError || detail.isPending)
      throw new Error('Reload the current policy version.');
    if (!from || !to || to <= from)
      throw new Error('Explicit start and later exclusive end dates are required.');
    return {
      expectedVersion: detail.data.version,
      expectedRevision: detail.data.revision,
      effectiveFrom: from,
      effectiveTo: to,
      rules: serializePolicyDraft(draft),
    };
  }
  let currentInput = '';
  let currentValidationInput = '';
  try {
    const value = candidate();
    currentInput = JSON.stringify(value);
    currentValidationInput = JSON.stringify({
      effectiveFrom: from,
      effectiveTo: to,
      rules: value.rules,
    });
  } catch {
    /* Incomplete drafts cannot match a validated publication preview. */
  }
  const readyPreview =
    currentInput && preview.value?.input === currentInput ? preview.value.result : null;
  const readyValidation =
    currentValidationInput && validation.value?.input === currentValidationInput
      ? validation.value.result
      : null;
  const busy = command.locked || preview.pending || validation.pending;
  function refresh() {
    if (busy) return;
    preview.clear();
    validation.clear();
    void detail.refetch();
    void versions.refetch();
  }
  async function check(mode: 'validate' | 'preview' | 'publish') {
    if (busy || !allowed) return;
    setError(null);
    try {
      const body = candidate();
      if (mode === 'validate')
        await validation.run({ effectiveFrom: from, effectiveTo: to, rules: body.rules });
      else if (mode === 'preview') await preview.run(body);
      else {
        if (
          !readyPreview?.canPublish ||
          !readyPreview.canonicalJson ||
          readyPreview.version !== body.expectedVersion ||
          readyPreview.revision !== body.expectedRevision
        )
          throw new Error('A matching publishable preview is required.');
        await command.run(
          root + '/' + policyId + '/versions',
          {
            ...body,
            expectedPreviewFingerprint: readyPreview.previewFingerprint,
            reason: requiredText(reason, 'Publication reason'),
          },
          vehiclePolicyPublication,
          () => {
            preview.clear();
            validation.clear();
            void detail.refetch();
            void versions.refetch();
          },
          () => preview.clear(),
        );
      }
    } catch (failure) {
      setError(inputError(failure));
    }
  }
  function copyRules(json: string) {
    if (busy) return;
    try {
      const rules = JSON.parse(json) as Record<string, unknown>;
      if (![1, 2, 3].includes(Number(rules.SchemaVersion)))
        throw new Error('Unsupported retained rules schema.');
      setDraft(policyDraft(undefined, { ...rules, Succession: undefined }));
      preview.clear();
      validation.clear();
      setError(null);
    } catch (failure) {
      setError(inputError(failure));
    }
  }
  return (
    <section className="hr-form-section">
      <div className="hr-repeat-heading">
        <h3>Policy publication and history</h3>
        <VehicleRefresh busy={busy || detail.isFetching || versions.isFetching} onClick={refresh} />
      </div>
      <QueryState query={detail}>
        {detail.data && (
          <>
            <RecordView value={detail.data} />
            {detail.data.isActive && (
              <fieldset disabled={busy} style={{ minWidth: 0 }}>
                <legend>New immutable version</legend>
                <div className="hr-form-grid">
                  <TextInput
                    label="Effective from (future)"
                    required
                    type="date"
                    value={from}
                    onChange={(event) => setFrom(event.target.value)}
                  />
                  <TextInput
                    label="Effective to (exclusive)"
                    required
                    type="date"
                    value={to}
                    onChange={(event) => setTo(event.target.value)}
                  />
                </div>
                <VehiclePolicyRulesEditor value={draft} onChange={setDraft} />
                <TextAreaField
                  label="Publication reason"
                  required
                  value={reason}
                  onChange={setReason}
                />
                <div className="hr-actions">
                  <Button variant="secondary" type="button" onClick={() => void check('validate')}>
                    <ShieldCheck size={15} /> Validate
                  </Button>
                  <Button variant="secondary" type="button" onClick={() => void check('preview')}>
                    <Eye size={15} /> Preview publication
                  </Button>
                  <Button
                    type="button"
                    disabled={!readyPreview?.canPublish}
                    onClick={() => void check('publish')}
                  >
                    <Send size={15} /> Publish future version
                  </Button>
                </div>
              </fieldset>
            )}
            {readyValidation && (
              <details open>
                <summary>Validation result</summary>
                <RecordView value={readyValidation} />
              </details>
            )}
            {readyPreview && (
              <details open>
                <summary>Publication preview</summary>
                <RecordView value={readyPreview} />
              </details>
            )}
          </>
        )}
      </QueryState>
      {error && <p role="alert">{error}</p>}
      {validation.error && <p role="alert">{validation.error}</p>}
      {preview.error && <p role="alert">{preview.error}</p>}
      <VehicleCommandStatus command={command} />
      <h3>Retained versions</h3>
      <QueryState query={versions}>
        {versions.data?.items.length === 0 && <p>No published versions returned.</p>}
        {versions.data?.items.map((item) => (
          <details key={item.id}>
            <summary>
              Version {item.number}: {item.effectiveFrom} to{' '}
              {item.effectiveTo ?? 'open-ended (retained)'}
            </summary>
            <RecordView value={item} />
            <Button
              type="button"
              variant="secondary"
              disabled={busy}
              onClick={() => copyRules(item.rulesJson)}
            >
              Use rules in new draft
            </Button>
          </details>
        ))}
        {versions.data && (
          <HasMorePager
            page={page}
            hasMore={versions.data.hasMore}
            busy={busy || versions.isFetching}
            onPrevious={() => setPage((value) => value - 1)}
            onNext={() => setPage((value) => value + 1)}
          />
        )}
      </QueryState>
    </section>
  );
}
