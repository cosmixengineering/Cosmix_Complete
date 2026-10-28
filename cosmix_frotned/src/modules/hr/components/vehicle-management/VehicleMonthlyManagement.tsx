import { useEffect, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Eye, Search, Send } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { ApiError } from '@/services/api/api-error';
import { TextAreaField, FinanceStatus } from '../finance/FinanceChrome';
import { Denied, QueryState } from '../Feedback';
import { RecordView } from '../RecordView';
import { useFinanceAccess } from '../../hooks/use-finance-access';
import { useFinanceQuery } from '../../hooks/use-finance-queries';
import { queryString } from '../../api/hr-finance-api';
import '@/styles/vehicle-management.css';
import {
  inputError,
  requiredId,
  requiredText,
  useVehiclePreview,
  vehicleManagementPermissions,
  vehicleManagementRoot,
  vehicleMonthlyHistory,
  vehicleMonthlyPreview,
  vehicleMonthlyRevision,
  vehicleMonthlyEntitlement,
} from '../../api/vehicle-management';
import {
  useVehicleCommand,
  VehicleCommandStatus,
  VehicleDecisionForm,
  VehicleRefresh,
  useVehicleNavigation,
} from './VehicleManagementShared';

const permission = vehicleManagementPermissions.monthly;
type Selection = { vehicleId: string; employeeId: string; monthStart: string };

export function VehicleMonthlyManagement() {
  const access = useFinanceAccess();
  const navigation = useVehicleNavigation();
  const [form, setForm] = useState({ vehicleId: '', employeeId: '', month: '' });
  const [selected, setSelected] = useState<Selection | null>(null);
  const [error, setError] = useState<string | null>(null);
  if (!access.can(permission)) return <Denied />;
  function load() {
    try {
      if (!/^\d{4}-\d{2}$/.test(form.month))
        throw new Error('Choose the completed calendar month.');
      const next = {
        vehicleId: requiredId(form.vehicleId, 'Vehicle'),
        employeeId: requiredId(form.employeeId, 'Employee'),
        monthStart: form.month + '-01',
      };
      if (JSON.stringify(next) !== JSON.stringify(selected))
        navigation.navigate(() => setSelected(next));
      setError(null);
    } catch (failure) {
      setError(inputError(failure));
    }
  }
  return (
    <section className="veh-monthly-section">
      <div className="veh-monthly-header-row">
        <h2 className="veh-section-title">Monthly distance adjustments</h2>
        <p className="veh-section-subtitle">
          Query vehicle distance reconciliation, review odometer adjustments, and submit revisions.
        </p>
      </div>

      <div className="veh-monthly-filter-card">
        <form
          className="veh-monthly-filter-form"
          onSubmit={(event) => {
            event.preventDefault();
            load();
          }}
        >
          <div className="veh-monthly-filter-grid">
            <TextInput
              label="Vehicle ID"
              disabled={navigation.pending}
              required
              value={form.vehicleId}
              onChange={(event) => setForm({ ...form, vehicleId: event.target.value })}
              placeholder="e.g. VEH-001"
            />
            <TextInput
              label="Employee ID"
              disabled={navigation.pending}
              required
              value={form.employeeId}
              onChange={(event) => setForm({ ...form, employeeId: event.target.value })}
              placeholder="e.g. EMP-101"
            />
            <TextInput
              label="Completed month"
              disabled={navigation.pending}
              required
              type="month"
              value={form.month}
              onChange={(event) => setForm({ ...form, month: event.target.value })}
            />
            <div className="veh-filter-action">
              <Button type="submit" disabled={navigation.pending} className="btn-veh-primary">
                <Search size={14} /> Load month
              </Button>
            </div>
          </div>
        </form>
      </div>

      {error && (
        <p className="veh-filter-error" role="alert">
          {error}
        </p>
      )}
      {selected && <VehicleMonthlyWorkspace key={JSON.stringify(selected)} selection={selected} />}
    </section>
  );
}

function VehicleMonthlyWorkspace({ selection }: { selection: Selection }) {
  const access = useFinanceAccess();
  const cache = useQueryClient();
  const allowed = access.can(permission);
  const { vehicleId, employeeId, monthStart } = selection;
  const root = vehicleManagementRoot + '/' + vehicleId + '/monthly-adjustments';
  const query = queryString({ employeeId, monthStart });
  const matches = (value: Selection) =>
    value.vehicleId === vehicleId &&
    value.employeeId === employeeId &&
    value.monthStart === monthStart;
  const history = useFinanceQuery(
    root + query,
    vehicleMonthlyHistory.refine((rows) => rows.every((row) => matches(row.calculation))),
    allowed,
  );
  const denied =
    history.isError &&
    history.error instanceof ApiError &&
    [401, 403].includes(history.error.status);
  const entitlement = useFinanceQuery(
    root + '/entitlement' + query,
    vehicleMonthlyEntitlement.refine(matches),
    allowed,
    !denied,
  );
  const preview = useVehiclePreview(
    permission,
    root + '/preview',
    vehicleMonthlyPreview.refine((value) => matches(value.calculation)),
  );
  useEffect(() => {
    if (!denied) return;
    preview.clear();
    const filter = {
      queryKey: ['hr', 'finance', access.subject, root + '/entitlement' + query],
      exact: true,
    };
    void cache.cancelQueries(filter);
    cache.removeQueries(filter);
  }, [denied, preview.clear, access.subject, root, query, cache]);
  const command = useVehicleCommand(permission);
  const [evidence, setEvidence] = useState('');
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const body = { employeeId, monthStart };
  const pending = history.data?.some((item) => item.state === 'Pending');
  const ready = !history.isPending && !history.isError && history.data !== undefined;
  const current =
    ready && preview.value?.input === JSON.stringify(body) ? preview.value.result : null;
  const refresh = () => {
    preview.clear();
    void history.refetch();
    void entitlement.refetch();
  };
  async function submit() {
    if (!allowed || command.locked || !current || !ready || pending) return;
    setError(null);
    try {
      await command.run(
        root,
        {
          ...body,
          expectedLatestRequestId: current.latestRequestId,
          expectedPreviewFingerprint: current.previewFingerprint,
          evidenceReference: requiredText(evidence, 'Evidence reference', 500),
          reason: requiredText(reason, 'Submission reason'),
        },
        vehicleMonthlyRevision.refine((value) => matches(value.calculation)),
        refresh,
        () => preview.clear(),
      );
    } catch (failure) {
      setError(inputError(failure));
    }
  }
  if (denied) return <QueryState query={history}>{null}</QueryState>;
  return (
    <section className="hr-form-section">
      <div className="hr-repeat-heading">
        <h3>{monthStart}: retained monthly evidence</h3>
        <VehicleRefresh
          busy={history.isFetching || entitlement.isFetching || command.locked || preview.pending}
          onClick={refresh}
        />
      </div>
      <RecordView value={selection} />
      <QueryState query={entitlement}>
        {entitlement.data && (
          <details open>
            <summary>Current HR kilometre entitlement: {entitlement.data.state}</summary>
            <RecordView value={entitlement.data} />
          </details>
        )}
      </QueryState>
      <fieldset disabled={command.locked || preview.pending} className="veh-revision-fieldset">
        <legend>New adjustment revision</legend>
        <div style={{ marginBottom: 12 }}>
          <Button
            type="button"
            variant="secondary"
            disabled={!ready || pending}
            onClick={() => void preview.run(body)}
            className="btn-veh-secondary"
          >
            <Eye size={14} /> Preview month
          </Button>
        </div>
        {preview.error && <p role="alert">{preview.error}</p>}
        {current && (
          <details open>
            <summary>Server calculation and policy partitions</summary>
            <RecordView value={current} />
          </details>
        )}
        {pending && (
          <p role="status">A pending revision must be reviewed before another is submitted.</p>
        )}
        <div className="veh-revision-inputs">
          <TextInput
            label="Evidence reference"
            required
            maxLength={500}
            value={evidence}
            onChange={(event) => setEvidence(event.target.value)}
          />
          <TextAreaField label="Submission reason" required value={reason} onChange={setReason} />
        </div>
        <Button
          type="button"
          disabled={!current || !ready || pending}
          onClick={() => void submit()}
          className="btn-veh-primary"
        >
          <Send size={14} /> Submit revision
        </Button>
      </fieldset>
      {error && <p role="alert">{error}</p>}
      <VehicleCommandStatus command={command} />
      <h3>Retained revisions</h3>
      <QueryState query={history}>
        {history.data?.length === 0 && <p>No monthly adjustment revisions returned.</p>}
        {history.data?.map((item) => (
          <details key={item.requestId} open={item.state === 'Pending'}>
            <summary>
              Revision {item.revision}: <FinanceStatus value={item.state} />
            </summary>
            <RecordView value={item} />
            {item.state === 'Pending' && (
              <VehicleDecisionForm
                permission={permission}
                path={root + '/' + item.requestId + '/review'}
                schema={vehicleMonthlyRevision}
                version={item.version}
                requestedByAccountId={item.requestedByAccountId}
                employeeId={employeeId}
                onSuccess={refresh}
                onRejected={refresh}
              />
            )}
          </details>
        ))}
      </QueryState>
    </section>
  );
}
