import type { ReactNode } from 'react';
import { Button } from '@/components/ui/Button';
import { RefreshCw } from 'lucide-react';
import type { FoundationPolicyRules, FoundationPolicyVersion } from './contracts';
import { policyError } from './hooks';

export function PolicyQueryState({
  query,
  children,
}: {
  query: { isPending: boolean; isError: boolean; error: unknown; refetch: () => unknown };
  children: ReactNode;
}) {
  if (query.isPending) return <p role="status">Loading policy records...</p>;
  if (query.isError)
    return (
      <div role="alert" className="hr-notice">
        <p>{policyError(query.error)}</p>
        <Button variant="secondary" onClick={() => void query.refetch()}>
          <RefreshCw size={16} />
          Retry read
        </Button>
      </div>
    );
  return children;
}
export function PolicyFacts({ rows }: { rows: readonly (readonly [string, ReactNode])[] }) {
  return (
    <dl className="foundation-facts">
      {rows.map(([label, value]) => (
        <div key={label}>
          <dt>{label}</dt>
          <dd>{value ?? 'Not recorded'}</dd>
        </div>
      ))}
    </dl>
  );
}
const yesNo = (value: boolean) => (value ? 'Yes' : 'No');
const values = (list: string[]) => (list.length ? list.join(', ') : 'No configured values');
export function PolicyRulesView({ rules }: { rules: FoundationPolicyRules }) {
  if (rules.lifecycle)
    return (
      <PolicyFacts
        rows={[
          ['Allow retirement', yesNo(rules.lifecycle.allowRetirement)],
          ['Allow suspension release', yesNo(rules.lifecycle.allowSuspensionRelease)],
        ]}
      />
    );
  if (rules.records)
    return <PolicyFacts rows={[['Operational record review mode', 'Fixed Admin review']]} />;
  if (!rules.fields) return <p role="alert">Policy rules are unavailable.</p>;
  const fields = rules.fields;
  return (
    <>
      <PolicyFacts
        rows={[
          ['Gender values', values(fields.genderValues)],
          ['Marital status values', values(fields.maritalStatusValues)],
          ['SIM status values', values(fields.simStatusValues)],
          ['SIM credit tier values', values(fields.simCreditTierValues)],
          ['Require gender', yesNo(fields.requireGender)],
          ['Require marital status', yesNo(fields.requireMaritalStatus)],
          ['Require father or spouse name', yesNo(fields.requireFatherOrSpouseName)],
          ['Require probation end', yesNo(fields.requireProbationEnd)],
          ['Minimum age at joining', fields.minimumAgeAtJoining ?? 'Not configured'],
          ['Maximum age at joining', fields.maximumAgeAtJoining ?? 'Not configured'],
          ['Maximum probation days', fields.maximumProbationDays ?? 'Not configured'],
          ['Identity expiry required', values(fields.requireExpiryForIdentityTypes)],
        ]}
      />
      <h4>Required documents</h4>
      {!fields.requiredDocuments.length && <p className="muted">No additional document rules</p>}
      {fields.requiredDocuments.map((row) => (
        <PolicyFacts
          key={row.staffCategoryCode ?? 'global'}
          rows={[
            ['Staff category', row.staffCategoryCode ?? 'All staff categories'],
            ['Document types', row.types.join(', ')],
          ]}
        />
      ))}
    </>
  );
}
export function PolicyVersionView({ version }: { version: FoundationPolicyVersion }) {
  return (
    <>
      <PolicyFacts
        rows={[
          ['Publication number', version.identity.number],
          ['Approved version', version.identity.approvedVersion || 'Unavailable'],
          ['Effective from (inclusive)', version.identity.effectiveFrom],
          ['Effective to (exclusive)', version.identity.effectiveTo ?? 'Open-ended'],
          [
            'Publication state',
            version.state === 'unavailable'
              ? 'Unavailable'
              : version.state === 'published'
                ? 'Published'
                : 'Effective',
          ],
          ['Published at (UTC)', new Date(version.publishedAt).toISOString()],
          ['Published by', version.publishedByUserId],
          ['Approval request', version.approvalRequestId],
        ]}
      />
      {version.state === 'unavailable' || !version.rules ? (
        <p role="alert">Published rules are unavailable. Contact the policy administrator.</p>
      ) : (
        <PolicyRulesView rules={version.rules} />
      )}
    </>
  );
}
