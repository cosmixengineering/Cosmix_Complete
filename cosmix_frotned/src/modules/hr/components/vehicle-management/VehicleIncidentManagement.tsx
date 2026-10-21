import { useState } from 'react';
import { Send } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { TextAreaField } from '../finance/FinanceChrome';
import { QueryState, Denied } from '../Feedback';
import { RecordView } from '../RecordView';
import { useFinanceAccess } from '../../hooks/use-finance-access';
import { useFinanceQuery } from '../../hooks/use-finance-queries';
import type { VehicleIncident } from '../../schemas/finance';
import {
  explicitNumber,
  inputError,
  requiredText,
  vehicleManagementPermissions,
  vehicleManagementRoot,
  vehicleIncidentReview,
  vehicleIncidentHistory,
  vehicleIncidentAmendment,
} from '../../api/vehicle-management';
import { vehicleIncidentOutcomeOptions } from '../../api/vehicle-policy-settings';
import {
  useVehicleCommand,
  VehicleCommandStatus,
  VehicleDecisionForm,
  VehicleRefresh,
  useVehicleNavigation,
} from './VehicleManagementShared';

const permission = vehicleManagementPermissions.incident;

export function VehicleIncidentManagement({
  incident,
  onChanged,
}: {
  incident: VehicleIncident;
  onChanged: () => void;
}) {
  const access = useFinanceAccess();
  const navigation = useVehicleNavigation();
  const allowed = access.can(permission);
  const root = vehicleManagementRoot + '/incidents/' + incident.id;
  const history = useFinanceQuery(
    root + '/review-history',
    vehicleIncidentHistory.refine(
      (value) =>
        value.original.incidentId === incident.id &&
        value.current.incidentId === incident.id &&
        value.amendments.every((item) => item.incidentId === incident.id),
    ),
    allowed,
    incident.approvalState !== 'Pending',
  );
  function refresh() {
    onChanged();
    if (incident.approvalState !== 'Pending') void history.refetch();
  }
  if (!allowed) return <Denied />;
  return (
    <section className="hr-form-section">
      <div className="hr-repeat-heading">
        <h3>Incident review: {incident.id}</h3>
        <VehicleRefresh
          busy={history.isFetching || navigation.pending}
          onClick={() => navigation.navigate(refresh)}
        />
      </div>
      <RecordView value={incident} />
      <p>HR interpretation only. Employee deduction and insurance settlement are not authorized.</p>
      {incident.approvalState === 'Pending' ? (
        incident.approvalRequestId ? (
          <IncidentOutcomeForm
            key={incident.approvalRequestId}
            incidentId={incident.id}
            identity={incident.approvalRequestId}
            amendment={false}
            onChanged={refresh}
          />
        ) : (
          <p role="alert">Approval identity unavailable. Reload the incident.</p>
        )
      ) : (
        <QueryState query={history}>
          {history.data && (
            <>
              <details open>
                <summary>Current retained interpretation</summary>
                <RecordView value={history.data.current} />
              </details>
              <details>
                <summary>Original decision</summary>
                <RecordView value={history.data.original} />
              </details>
              {!history.data.amendments.some((item) => item.state === 'Pending') && (
                <IncidentOutcomeForm
                  incidentId={incident.id}
                  identity={history.data.current.decisionId}
                  amendment={true}
                  onChanged={refresh}
                />
              )}
              <h4>Amendment history</h4>
              {history.data.amendments.length === 0 && <p>No amendment proposals returned.</p>}
              {history.data.amendments.map((item) => (
                <details key={item.requestId} open={item.state === 'Pending'}>
                  <summary>
                    Revision {item.revision}: {item.state}
                  </summary>
                  <RecordView value={item} />
                  {item.state === 'Pending' && (
                    <VehicleDecisionForm
                      permission={permission}
                      path={root + '/amendments/' + item.requestId + '/review'}
                      schema={vehicleIncidentAmendment}
                      version={item.version}
                      requestedByAccountId={item.requestedByAccountId}
                      onSuccess={refresh}
                      onRejected={refresh}
                    />
                  )}
                </details>
              ))}
            </>
          )}
        </QueryState>
      )}
    </section>
  );
}

function IncidentOutcomeForm({
  incidentId,
  identity,
  amendment,
  onChanged,
}: {
  incidentId: string;
  identity: string;
  amendment: boolean;
  onChanged: () => void;
}) {
  const access = useFinanceAccess();
  const command = useVehicleCommand(permission);
  const [form, setForm] = useState({
    outcome: '',
    evidence: '',
    reason: '',
    amount: '',
    currency: '',
    insurance: '',
  });
  const [error, setError] = useState<string | null>(null);
  async function submit() {
    if (command.locked || !access.can(permission)) return;
    setError(null);
    try {
      if (!vehicleIncidentOutcomeOptions.some((option) => option.value === form.outcome))
        throw new Error('Choose an explicit outcome.');
      const amount =
        form.outcome === '4'
          ? explicitNumber(form.amount, 'Proposed liability', 2, 0, 10000000)
          : null;
      if (amount === 0) throw new Error('Proposed liability must be positive.');
      const currency = form.outcome === '4' ? form.currency.trim() : null;
      if (currency !== null && !/^[A-Z]{3}$/.test(currency))
        throw new Error('Enter the explicit three-letter uppercase currency.');
      const proposal = {
        outcome: Number(form.outcome),
        evidenceReference: requiredText(form.evidence, 'Evidence reference', 500),
        reason: requiredText(form.reason, 'Reason'),
        proposedEmployeeLiability: amount,
        currency,
        insuranceClaimReference:
          form.outcome === '5' || form.insurance.trim()
            ? requiredText(form.insurance, 'Insurance claim reference', 200)
            : null,
      };
      const root = vehicleManagementRoot + '/incidents/' + incidentId;
      if (amendment)
        await command.run(
          root + '/amendments',
          { expectedDecisionId: identity, ...proposal },
          vehicleIncidentAmendment,
          onChanged,
          onChanged,
        );
      else
        await command.run(
          root + '/review',
          { expectedApprovalRequestId: identity, ...proposal },
          vehicleIncidentReview,
          onChanged,
          onChanged,
        );
    } catch (failure) {
      setError(inputError(failure));
    }
  }
  return (
    <form
      onSubmit={(event) => {
        event.preventDefault();
        void submit();
      }}
    >
      <fieldset disabled={command.locked} className="hr-form-grid">
        <legend>{amendment ? 'Propose amendment' : 'Record independent incident review'}</legend>
        <SelectInput
          label="Outcome"
          required
          value={form.outcome}
          options={[{ value: '', label: 'Choose an outcome' }, ...vehicleIncidentOutcomeOptions]}
          onChange={(event) => setForm({ ...form, outcome: event.target.value })}
        />
        <TextInput
          label="Evidence reference"
          required
          maxLength={500}
          value={form.evidence}
          onChange={(event) => setForm({ ...form, evidence: event.target.value })}
        />
        {form.outcome === '4' && (
          <>
            <TextInput
              label="Proposed liability amount"
              required
              type="number"
              min={0}
              max={10000000}
              step="0.01"
              value={form.amount}
              onChange={(event) => setForm({ ...form, amount: event.target.value })}
            />
            <TextInput
              label="Currency"
              required
              maxLength={3}
              value={form.currency}
              onChange={(event) => setForm({ ...form, currency: event.target.value })}
            />
          </>
        )}
        <TextInput
          label="Insurance claim reference"
          required={form.outcome === '5'}
          maxLength={200}
          value={form.insurance}
          onChange={(event) => setForm({ ...form, insurance: event.target.value })}
        />
        <TextAreaField
          label="Reason"
          required
          value={form.reason}
          onChange={(reason) => setForm({ ...form, reason })}
        />
        <Button type="submit">
          <Send size={15} /> {amendment ? 'Submit amendment proposal' : 'Record HR review'}
        </Button>
      </fieldset>
      {error && <p role="alert">{error}</p>}
      <VehicleCommandStatus command={command} />
    </form>
  );
}
