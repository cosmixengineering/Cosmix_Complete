import type { ReactNode } from 'react';
import { RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { policyUses, type Mapping } from './contracts';
import { taxonomyError } from './messages';

export function ReadState({
  query,
  children,
  locked = false,
}: {
  query: { isPending: boolean; isError: boolean; error: unknown; refetch: () => unknown };
  children: ReactNode;
  locked?: boolean;
}) {
  if (query.isPending) return <p role="status">Loading taxonomy records...</p>;
  if (query.isError)
    return (
      <div role="alert">
        <p>{taxonomyError(query.error)}</p>
        <Button variant="secondary" disabled={locked} onClick={() => void query.refetch()}>
          <RefreshCw size={16} />
          Retry read
        </Button>
      </div>
    );
  return children;
}
export function MappingView({ mapping }: { mapping: Mapping }) {
  return (
    <article className="taxonomy-mapping">
      <h3>
        {mapping.name} <span className="muted">{mapping.code}</span>
      </h3>
      <dl className="taxonomy-facts">
        <div>
          <dt>Work location</dt>
          <dd>{mapping.workLocation === 1 ? 'Office' : 'Site'}</dd>
        </div>
        <div>
          <dt>Schedule basis</dt>
          <dd>{mapping.scheduleBasis === 1 ? 'Office' : 'Site'}</dd>
        </div>
        <div>
          <dt>Site required before activation</dt>
          <dd>{mapping.requireSiteBeforeActivation ? 'Yes' : 'No'}</dd>
        </div>
        <div>
          <dt>Outside OT inheritance</dt>
          <dd>{mapping.outsideOvertimeInheritance ? 'Yes' : 'No'}</dd>
        </div>
      </dl>
      <div className="taxonomy-table">
        <table>
          <thead>
            <tr>
              <th>Policy use</th>
              <th>Applicability</th>
              <th>Immutable version</th>
            </tr>
          </thead>
          <tbody>
            {policyUses.map(({ value, label }) => {
              const r = mapping.policyReferences.find((r) => r.use === value)!;
              return (
                <tr key={value}>
                  <th scope="row">{label}</th>
                  <td>
                    {r.applicability === 0
                      ? 'Unconfigured'
                      : r.applicability === 1
                        ? 'Not applicable'
                        : 'Required'}
                  </td>
                  <td>{r.policyVersionId ?? 'None'}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </article>
  );
}
export function CapabilityBoundary() {
  return (
    <p className="taxonomy-notice">
      Claim eligibility, rates and readiness: not supplied. Outside OT inheritance remains subject
      to the effective OT policy and denial rules.
    </p>
  );
}
export function MutationFeedback({
  mutation,
}: {
  mutation: { error: string | null; uncertain: boolean; reloadRequired: boolean };
}) {
  return (
    <>
      {mutation.error && (
        <p role="alert" className="hr-message hr-message--error">
          {mutation.error}
        </p>
      )}
      {mutation.uncertain && (
        <p role="status">
          The result is unconfirmed. The exact action and versions are retained for an unchanged
          retry.
        </p>
      )}
      {mutation.reloadRequired && (
        <p role="alert">Reload and review current state before another action.</p>
      )}
    </>
  );
}
