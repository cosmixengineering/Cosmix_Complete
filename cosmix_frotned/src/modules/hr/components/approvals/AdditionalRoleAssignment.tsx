import { useEffect, useState, type FormEvent } from 'react';
import { Clock, ShieldPlus } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { useHrAccess } from '../../hooks/use-hr-access';
import { useHrLifetime } from '../../hooks/use-hr-lifetime';
import { ReferenceField } from '../ReferenceField';
import {
  additionalRoleAssignmentSchema,
  type AdditionalRoleAssignmentRequest,
  type AccessAccount,
  type AccessPermission,
  type AccessRole,
} from '../../schemas/access-setup';

const scopes = [
  { value: '', label: 'Select a scope' },
  { value: '1', label: 'Self' },
  { value: '7', label: 'All' },
  { value: '2', label: 'Employee' },
  { value: '3', label: 'Department' },
  { value: '5', label: 'Site' },
  { value: '4', label: 'Team' },
  { value: '6', label: 'Project' },
];
const references: Record<string, { kind: string; permission: string; label: string }> = {
  '2': { kind: 'employees', permission: 'hr.employees.read', label: 'Employee' },
  '3': { kind: 'departments', permission: 'hr.organization.read', label: 'Department' },
  '5': { kind: 'sites', permission: 'hr.sites.read', label: 'Site' },
};

export function AdditionalRoleAssignment({
  account,
  roles,
  catalog,
  disabled,
  onDirty,
  onAssign,
}: {
  account: AccessAccount;
  roles: AccessRole[];
  catalog: AccessPermission[];
  disabled: boolean;
  onDirty: (dirty: boolean) => void;
  onAssign: (request: AdditionalRoleAssignmentRequest) => Promise<boolean>;
}) {
  const access = useHrAccess();
  const lifetime = useHrLifetime();
  const [roleId, setRoleId] = useState('');
  const [scope, setScope] = useState('');
  const [targetId, setTargetId] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [reason, setReason] = useState('');
  const [confirmAll, setConfirmAll] = useState(false);
  const [error, setError] = useState('');
  const [sending, setSending] = useState(false);
  const role = roles.find((item) => item.id === roleId);
  const reference = references[scope];
  const blocked = disabled || sending;
  const dirty = !!roleId || !!scope || !!targetId || !!from || !!to || !!reason || confirmAll;
  useEffect(() => {
    onDirty(dirty);
    return () => onDirty(false);
  }, [dirty, onDirty]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (blocked) return;
    const start = new Date(from);
    const end = to ? new Date(to) : null;
    if (
      !role ||
      !role.isActive ||
      role.isSystemRole ||
      role.systemRole !== 0 ||
      !role.permissions.length ||
      !scope ||
      !Number.isFinite(start.getTime()) ||
      (end && !Number.isFinite(end.getTime())) ||
      (reference && !access.can(reference.permission)) ||
      (scope === '7' && !confirmAll)
    ) {
      setError(
        'Select a role, scope, permitted target and valid effective dates. An end date must be in the future. All scope requires confirmation.',
      );
      return;
    }
    const result = additionalRoleAssignmentSchema.safeParse({
      roleId: role.id,
      scopes: [{ kind: Number(scope), targetId: scope === '1' || scope === '7' ? null : targetId }],
      effectiveFrom: start.toISOString(),
      effectiveTo: end?.toISOString() ?? null,
      expectedAccountVersion: account.version,
      expectedRoleVersion: role.version,
      configureAsInitialAccess: false,
      reason,
    });
    if (!result.success) {
      setError(
        'Enter a reason of 1–1000 characters without control characters, a valid target and an end date after the start date.',
      );
      return;
    }
    setError('');
    setSending(true);
    const signal = lifetime();
    await onAssign(result.data).finally(() => {
      if (!signal.aborted) setSending(false);
    });
  }

  return (
    <section className="hr-form-section" aria-label="Additional role assignment">
      <h3>Additional role access</h3>
      {disabled && (
        <p className="muted">
          Additional assignments require fresh access data, initial Self access, a non-system target
          account, and role assignment/delegation permissions.
        </p>
      )}
      {!roles.length && (
        <p className="hr-notice">
          No existing active non-system role with delegable permissions is available.
        </p>
      )}
      <form onSubmit={submit}>
        <fieldset disabled={blocked} className="hr-form-grid">
          <SelectInput
            label="Additional existing role"
            value={roleId}
            required
            options={[
              { value: '', label: 'Select a role' },
              ...roles.map((item) => ({ value: item.id, label: `${item.name} (${item.code})` })),
            ]}
            onChange={(event) => setRoleId(event.target.value)}
          />
          <SelectInput
            label="Access scope"
            value={scope}
            required
            options={scopes}
            onChange={(event) => {
              setScope(event.target.value);
              setTargetId('');
              setConfirmAll(false);
            }}
          />
          {reference && (
            <ReferenceField
              key={scope}
              label={reference.label}
              kind={reference.kind}
              required
              value={targetId || null}
              disabled={blocked}
              onChange={(value) => setTargetId(value ?? '')}
            />
          )}
          {(scope === '4' || scope === '6') && (
            <TextInput
              label={scope === '4' ? 'Existing team ID' : 'Existing project ID'}
              required
              value={targetId}
              maxLength={36}
              autoComplete="off"
              onChange={(event) => setTargetId(event.target.value.trim())}
            />
          )}
          {scope === '7' && (
            <label className="wide">
              <input
                type="checkbox"
                checked={confirmAll}
                required
                onChange={(event) => setConfirmAll(event.target.checked)}
              />{' '}
              Confirm access to all records for every permission in this role
            </label>
          )}
          <TextInput
            label="Reason for additional access"
            required
            maxLength={1000}
            value={reason}
            onChange={(event) => setReason(event.target.value)}
          />
          <TextInput
            label="Effective from (local time)"
            type="datetime-local"
            step={1}
            value={from}
            required
            onChange={(event) => setFrom(event.target.value)}
          />
          <TextInput
            label="Effective until (local time, optional)"
            type="datetime-local"
            step={1}
            value={to}
            onChange={(event) => setTo(event.target.value)}
          />
          <Button
            variant="secondary"
            title="Use current local time"
            onClick={() => {
              const now = new Date();
              setFrom(
                new Date(now.getTime() - now.getTimezoneOffset() * 60000)
                  .toISOString()
                  .slice(0, 19),
              );
            }}
          >
            <Clock size={16} /> Start now
          </Button>
          <Button
            type="submit"
            disabled={
              !role ||
              !scope ||
              !from ||
              !reason.trim() ||
              (scope === '7' && !confirmAll) ||
              (!!reference && !access.can(reference.permission))
            }
          >
            <ShieldPlus size={16} /> Assign additional role
          </Button>
        </fieldset>
        {role && (
          <ul>
            {role.permissions.map((code) => {
              const definition = catalog.find((item) => item.code === code);
              return (
                <li key={code}>
                  {code}: {definition?.description ?? 'Unsupported permission'}
                  {definition?.management ? ' (management)' : ''}
                  {definition?.sensitive ? ' (sensitive)' : ''}
                  {definition?.selfService ? ' (self-service)' : ''}
                </li>
              );
            })}
          </ul>
        )}
        {role &&
          !role.permissions.some(
            (code) => catalog.find((item) => item.code === code)?.management,
          ) && (
            <p className="muted">
              This role contains no management permissions. Broader scope cannot add permissions to
              it.
            </p>
          )}
        {role &&
          scope &&
          scope !== '7' &&
          role.permissions.some(
            (code) => catalog.find((item) => item.code === code)?.management,
          ) && (
            <p className="muted">
              Current HR and access administration endpoints require All-scoped permissions. This
              narrower assignment will not unlock those operations.
            </p>
          )}
        <p className="muted">
          Assignments start and expire on the selected dates. Self covers this employee only; All
          covers all resources. The server verifies target existence and delegation. Additional
          access does not replace initial access or enable a login channel.
        </p>
        {error && (
          <p className="hr-message hr-message--error" role="alert">
            {error}
          </p>
        )}
      </form>
    </section>
  );
}
