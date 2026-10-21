import { useParams } from 'react-router-dom';
import { FoundationPoliciesWorkspace } from './FoundationPoliciesWorkspace';
import type { FoundationPolicyKind } from './contracts';

export function FoundationPoliciesPage({
  requests = false,
  newProposal = false,
}: {
  requests?: boolean;
  newProposal?: boolean;
}) {
  const { kind: segment, requestId } = useParams();
  const kind: FoundationPolicyKind | undefined =
    segment === undefined || segment === 'fields'
      ? 'Fields'
      : segment === 'lifecycle'
        ? 'Lifecycle'
        : segment === 'records'
          ? 'Records'
          : undefined;
  return (
    <>
      <div className="hr-page-heading">
        <h1>HR foundation policies</h1>
      </div>
      {kind ? (
        <FoundationPoliciesWorkspace
          kind={kind}
          requestId={requestId}
          initialView={requests ? 'requests' : 'current'}
          newProposal={newProposal}
        />
      ) : (
        <p role="alert">Unsupported HR foundation policy family.</p>
      )}
    </>
  );
}
