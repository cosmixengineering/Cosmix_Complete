import type { ReactNode } from 'react';
import { RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { RecordView } from '../components/RecordView';
import { outstationError } from './api';
import type { DeploymentCalculation } from './contracts';

export function OutstationRead({
  query,
  children,
}: {
  query: {
    isPending: boolean;
    isFetching: boolean;
    isError: boolean;
    error: unknown;
    refetch: () => unknown;
  };
  children: ReactNode;
}) {
  if (query.isError)
    return (
      <div className="hr-notice" role="alert">
        <p>{outstationError(query.error)}</p>
        <Button
          variant="secondary"
          disabled={query.isFetching}
          onClick={() => void query.refetch()}
        >
          <RefreshCw size={16} />
          Reload records
        </Button>
      </div>
    );
  if (query.isPending || query.isFetching)
    return <p role="status">Loading current deployment evidence...</p>;
  return children;
}
const pendingStates: Record<string, string> = {
  deployment_approval_pending: 'Deployment approval is pending.',
  deployment_actual_end_pending: 'The actual deployment end is pending.',
  deployment_home_break_review_pending: 'Home-break review is pending.',
  deployment_return_evidence_pending: 'Approved return evidence is pending.',
  deployment_valuation_policy_pending: 'Explicit rate, day-count or rounding policy is missing.',
  deployment_category_not_configured:
    'This deployment category is not configured in the effective policy.',
  deployment_policy_transition_not_configured:
    'The policy does not define treatment across policy changes.',
  deployment_policy_currency_changes:
    'Currency changes across this trip require policy resolution.',
  deployment_policy_counting_changes: 'Day-count rules change across this trip.',
  deployment_policy_rounding_changes: 'Rounding rules change across this trip.',
  deployment_policy_change_inside_continuous_cycle:
    'A policy change falls inside an uninterrupted duty cycle.',
  deployment_policy_coverage_pending: 'Effective policy coverage is incomplete.',
  deployment_amount_out_of_range: 'The calculated amount exceeds the supported range.',
};
export function CalculationEvidence({
  value,
  retained = false,
}: {
  value: DeploymentCalculation;
  retained?: boolean;
}) {
  const ready = value.state === 'ready';
  return (
    <section className="hr-form-section">
      <h3>{retained ? 'Retained claim calculation' : 'Deployment eligibility'}</h3>
      <p role="status">
        {ready
          ? 'Calculated from explicit policy. Not a payment or settlement.'
          : (pendingStates[value.state] ?? 'Calculation is pending.')}
      </p>
      <RecordView
        value={{
          state: value.state,
          calculatedDayUnits: ready ? value.valuation?.dayUnits : 'Pending',
          calculatedAmount: ready ? value.valuation?.amount : 'Pending',
          currency: ready ? value.valuation?.currency : 'Pending',
          fingerprint: value.fingerprint,
          departurePolicyVersion: value.policyVersionId,
          approvedBreakIds: value.approvedBreakIds,
        }}
      />
      {!!value.valuation?.lines.length && (
        <div className="hr-table-scroll">
          <table className="hr-prepared-table">
            <caption>Server calculation lines</caption>
            <thead>
              <tr>
                <th>Date</th>
                <th>UTC interval</th>
                <th>Day units</th>
                <th>Unrounded amount</th>
                <th>Treatment</th>
              </tr>
            </thead>
            <tbody>
              {value.valuation.lines.map((line, index) => (
                <tr key={`${line.startsAtUtc}:${index}`}>
                  <td>{line.date ?? 'Continuous cycle'}</td>
                  <td>
                    {line.startsAtUtc}
                    <br />
                    {line.endsAtUtc}
                  </td>
                  <td>{line.dayUnits}</td>
                  <td>{line.unroundedAmount}</td>
                  <td>{line.treatment}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <details>
        <summary>Effective policy evidence ({value.policyDays.length} dates)</summary>
        {!value.policyDays.length && <p>No effective policy evidence returned.</p>}
        {value.policyDays.map((day) => (
          <details key={day.date}>
            <summary>
              {day.date} / {day.policyVersionId}
            </summary>
            <RecordView value={{ assignments: day.assignments, fingerprint: day.fingerprint }} />
            <pre style={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>
              {day.canonicalRules}
            </pre>
          </details>
        ))}
      </details>
    </section>
  );
}
