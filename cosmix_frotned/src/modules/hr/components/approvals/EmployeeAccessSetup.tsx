import { useCallback, useEffect, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { KeyRound, RefreshCw, ShieldCheck, Smartphone } from 'lucide-react';
import { request } from '@/services/api/http-client';
import { ApiError } from '@/services/api/api-error';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { PasswordInput } from '@/components/forms/PasswordInput';
import { AuthenticatorReset } from '@/auth/AuthenticatorReset';
import { SelectInput } from '@/components/forms/SelectInput';
import { useHrAccess } from '../../hooks/use-hr-access';
import { useHrLifetime } from '../../hooks/use-hr-lifetime';
import { useDirtyForm } from '../../hooks/use-dirty-form';
import { Denied } from '../Feedback';
import { AdditionalRoleAssignment } from './AdditionalRoleAssignment';
import { errorMessage } from '../../utils/messages';
import {
  employeeAccessSetupSchema,
  accessPermissionSchema,
  accessRolePageSchema,
  accessAssignmentPageSchema,
  initialAccessResultSchema,
  mobileProvisioningResultSchema,
  accessAccountSchema,
  firstWebCredentialsSchema,
  additionalRoleAssignmentSchema,
  initialRoleAssignmentSchema,
  accessReasonSchema,
  accessStatusLabels,
  type AccessRole,
  type AccessAssignment,
} from '../../schemas/access-setup';

export interface EmployeeAccessSetupProps {
  employeeId: string;
  approved: boolean;
  onSaved?: () => void;
  onDirty?: (dirty: boolean) => void;
  onBusy?: (busy: boolean) => void;
}

const accessErrors: Record<string, string> = {
  password_policy_failed: 'The password does not meet the configured server policy.',
  web_password_policy_not_configured: 'The server password policy is not configured.',
  web_credential_already_provisioned:
    'Existing web credentials cannot be replaced by first-time setup.',
  web_login_email_conflict: 'This web login email is already assigned to another account.',
  approved_login_phone_required: 'A login phone approved in the hiring submission is required.',
  phone_recovery_review_required:
    'The existing phone enrollment requires a separate recovery review.',
  login_phone_conflict: 'The approved login phone is already assigned to another account.',
  assigned_access_required: 'A current active role assignment is required.',
  account_ineligible: 'The current account or employment state does not permit this setup.',
  web_credential_setup_ineligible:
    'The approved phone, initial access or account state does not permit first web setup.',
  initial_access_requires_current_self_scope_and_no_invitation:
    'Initial access requires current Self scope and no outstanding invitation.',
};

// Remount private forms and request locks whenever the employee or session grants change.
export function EmployeeAccessSetup(props: EmployeeAccessSetupProps) {
  const access = useHrAccess();
  const identity = JSON.stringify([access.subject, access.ready, access.user]);
  return <AccessSetupPanel key={props.employeeId + identity + props.approved} {...props} />;
}

async function read<T extends z.ZodType>(path: string, schema: T, signal: AbortSignal) {
  return schema.parse(await request(path, { signal }));
}

async function readPages<T extends z.ZodType>(path: string, schema: T, signal: AbortSignal) {
  const items: unknown[] = [];
  for (let page = 1; page <= 10000; page++) {
    const result = (await read(`${path}?page=${page}&pageSize=100`, schema, signal)) as {
      items: unknown[];
      totalCount: number;
      page: number;
      pageSize: number;
    };
    if (result.page !== page || result.pageSize !== 100) throw new Error('Invalid access page.');
    items.push(...result.items);
    if (items.length >= result.totalCount) return items;
    if (!result.items.length) throw new Error('Incomplete access catalogue.');
  }
  throw new Error('Access catalogue is too large.');
}

function AccessSetupPanel({
  employeeId,
  approved,
  onSaved,
  onDirty,
  onBusy,
}: EmployeeAccessSetupProps) {
  const access = useHrAccess();
  const lifetime = useHrLifetime();
  const cache = useQueryClient();
  const form = useRef<HTMLFormElement>(null);
  const bindCredentialForm = useCallback((element: HTMLFormElement | null) => {
    form.current = element;
    return () => {
      element
        ?.querySelectorAll<HTMLInputElement>('input[name="password"], input[name="confirmation"]')
        .forEach((input) => {
          input.value = '';
        });
      form.current = null;
    };
  }, []);
  const active = useRef(false);
  const [pending, setPending] = useState(false);
  const [mustReload, setMustReload] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [roleId, setRoleId] = useState('');
  const [reason, setReason] = useState('');
  const assignmentIntent = useRef<{
    path: string;
    body: unknown;
    key: string;
    additional: boolean;
  } | null>(null);
  const [uncertainAssignment, setUncertainAssignment] = useState(false);
  const [email, setEmail] = useState('');
  const [passwordDirty, setPasswordDirty] = useState(false);
  const [additionalDirty, setAdditionalDirty] = useState(false);
  const [additionalRevision, setAdditionalRevision] = useState(0);
  const canRead =
    approved &&
    z.uuid().safeParse(employeeId).success &&
    access.can('hr.employees.submissions.review', 'access.accounts.read');
  const canRoles = access.can('access.roles.read');
  const query = useQuery({
    queryKey: ['hr-access-setup', access.subject, access.user?.grants, employeeId, canRoles],
    queryFn: async ({ signal }) => {
      const currentSignal = AbortSignal.any([signal, lifetime()]);
      const setup = await read(
        `/hr/employees/${employeeId}/access-setup`,
        employeeAccessSetupSchema,
        currentSignal,
      );
      if (
        setup.employeeId !== employeeId ||
        (setup.account && setup.account.employeeId !== employeeId)
      )
        throw new Error('Access response does not match this employee.');
      const assignments = setup.account
        ? ((await readPages(
            `/access/accounts/${setup.account.id}/assignments`,
            accessAssignmentPageSchema,
            currentSignal,
          )) as AccessAssignment[])
        : [];
      const catalog = canRoles
        ? await read('/access/permissions', z.array(accessPermissionSchema), currentSignal)
        : [];
      const roles = canRoles
        ? ((await readPages('/access/roles', accessRolePageSchema, currentSignal)) as AccessRole[])
        : [];
      return { setup, assignments, catalog, roles, readAt: Date.now() };
    },
    enabled: canRead,
    gcTime: 0,
    staleTime: 60000,
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    refetchOnMount: 'always',
  });
  const dirty =
    !!roleId || !!reason || !!email || passwordDirty || additionalDirty || uncertainAssignment;
  useDirtyForm(dirty || pending);
  useEffect(() => {
    onDirty?.(dirty);
    return () => onDirty?.(false);
  }, [dirty, onDirty]);
  useEffect(() => {
    onBusy?.(pending);
    return () => onBusy?.(false);
  }, [pending, onBusy]);
  const data = query.data;
  const setup = data?.setup;
  const account = setup?.account;
  const self =
    employeeId === access.user?.account?.employeeId ||
    (!!account && account.id === access.user?.account?.accountId);
  const role = data?.roles.find((item) => item.id === roleId);
  // Session self-service grants are capped to Self; only the server can prove original All delegation.
  const mayDelegate = (code: string) => {
    const definition = data?.catalog.find((item) => item.code === code);
    return (
      !!definition &&
      (access.can(code) ||
        (definition.selfService &&
          !!access.user?.grants.some((grant) => grant.permission === code)))
    );
  };
  const selectable =
    data?.roles.filter(
      (item) =>
        item.isActive &&
        !item.isSystemRole &&
        item.systemRole === 0 &&
        item.permissions.length > 0 &&
        item.permissions.every(mayDelegate),
    ) ?? [];
  const systemTarget = data?.assignments.some((assignment) => {
    const assignedRole = data.roles.find((item) => item.id === assignment.roleId);
    return (
      (!assignedRole || assignedRole.isSystemRole) &&
      !assignment.revokedAt &&
      (!assignment.effectiveTo || Date.parse(assignment.effectiveTo) > data.readAt)
    );
  });
  const delegatedTarget =
    canRoles &&
    !systemTarget &&
    !!data?.assignments.length &&
    data.assignments.some(
      (assignment) =>
        !assignment.revokedAt &&
        Date.parse(assignment.effectiveFrom) <= data.readAt &&
        (!assignment.effectiveTo || Date.parse(assignment.effectiveTo) > data.readAt) &&
        data.roles.some(
          (item) => item.id === assignment.roleId && item.isActive && item.permissions.length > 0,
        ),
    ) &&
    data.assignments
      .filter(
        (assignment) =>
          !assignment.revokedAt &&
          (!assignment.effectiveTo || Date.parse(assignment.effectiveTo) > data.readAt),
      )
      .every((assignment) => {
        const assignedRole = data.roles.find((item) => item.id === assignment.roleId);
        return !!assignedRole && assignedRole.permissions.every(mayDelegate);
      });
  const locked =
    pending ||
    query.isFetching ||
    query.isError ||
    query.isStale ||
    !data ||
    mustReload ||
    self ||
    !access.user?.account;
  const canAssign =
    !locked &&
    !!account &&
    !setup?.initialAccessAssigned &&
    (account.status === 0 || account.status === 1) &&
    access.can('access.accounts.assign', 'access.roles.permissions.manage') &&
    accessReasonSchema.safeParse(reason).success &&
    selectable.some((item) => item.id === roleId);
  const canMobile =
    !locked &&
    !!account &&
    !!setup?.initialAccessAssigned &&
    !!setup.mobileAccessRequested &&
    !setup.mobilePhoneEnrolled &&
    (account.status === 1 || account.status === 3) &&
    delegatedTarget &&
    access.can('access.accounts.mobile.manage', 'access.roles.permissions.manage');
  const canWeb =
    !locked &&
    !!account &&
    !!setup?.initialAccessAssigned &&
    !account.webAccessEnabled &&
    (account.status === 1 || account.status === 3) &&
    delegatedTarget &&
    access.can('access.accounts.web.manage', 'access.roles.permissions.manage');
  const canAdditional =
    !locked &&
    canRoles &&
    !systemTarget &&
    !!account &&
    !!setup?.initialAccessAssigned &&
    [1, 2, 3].includes(account.status) &&
    access.can('access.accounts.assign', 'access.roles.permissions.manage');

  function clearPasswords() {
    form.current
      ?.querySelectorAll<HTMLInputElement>('input[name="password"], input[name="confirmation"]')
      .forEach((input) => {
        input.value = '';
      });
    setPasswordDirty(false);
  }

  async function reload() {
    if (active.current || lifetime().aborted) return;
    if (dirty && !window.confirm('Discard unsaved access setup changes and refresh?')) return;
    clearPasswords();
    setEmail('');
    setRoleId('');
    setReason('');
    assignmentIntent.current = null;
    setUncertainAssignment(false);
    setAdditionalRevision((value) => value + 1);
    setAdditionalDirty(false);
    const result = await query.refetch();
    if (lifetime().aborted) return;
    if (!result.isError) {
      setMustReload(false);
      setError('');
      setMessage('Current access reloaded.');
    }
  }

  async function write(
    path: string,
    body: unknown,
    schema: z.ZodType,
    success: string,
    additional = false,
    retryAssignment = false,
  ): Promise<boolean> {
    const signal = lifetime();
    if (active.current || signal.aborted || (!retryAssignment && locked)) return false;
    if (retryAssignment && !assignmentIntent.current) return false;
    if (!additional && additionalDirty) {
      if (!window.confirm('Discard the unsaved additional role assignment and continue?'))
        return false;
      setAdditionalRevision((value) => value + 1);
      setAdditionalDirty(false);
    }
    if (!retryAssignment && Date.now() - query.dataUpdatedAt >= 60000) {
      setMustReload(true);
      setError('Access data is stale. Refresh before sending.');
      return false;
    }
    const isAssignment = /^\/access\/accounts\/[^/]+\/assignments$/.test(path);
    if (isAssignment && !retryAssignment) {
      const assignment = (
        additional ? additionalRoleAssignmentSchema : initialRoleAssignmentSchema
      ).safeParse(body);
      if (!assignment.success) {
        setError(
          'Enter a reason of 1–1000 characters without control characters and valid access details.',
        );
        return false;
      }
      body = assignment.data;
      assignmentIntent.current = { path, body, key: crypto.randomUUID(), additional };
    }
    if (additional && !retryAssignment) {
      const assignment = additionalRoleAssignmentSchema.safeParse(body);
      if (
        !assignment.success ||
        (assignment.data.effectiveTo && Date.parse(assignment.data.effectiveTo) <= Date.now())
      ) {
        setError(
          'The additional assignment is invalid or its end date is no longer in the future.',
        );
        return false;
      }
    }
    active.current = true;
    setPending(true);
    setMustReload(true);
    setError('');
    setMessage('');
    try {
      // Only assignment payloads/keys remain in memory for an explicit identical retry.
      // Credential payloads are never retained or replayed.
      const response = await request(path, {
        method: 'POST',
        body,
        signal,
        idempotencyKey: isAssignment ? assignmentIntent.current?.key : undefined,
      });
      body = undefined;
      schema.parse(response);
      if (signal.aborted) return false;
      assignmentIntent.current = null;
      setUncertainAssignment(false);
      setRoleId('');
      setReason('');
      setEmail('');
      setAdditionalRevision((value) => value + 1);
      setAdditionalDirty(false);
      await cache.invalidateQueries({ queryKey: ['hr'], refetchType: 'none' });
      if (signal.aborted) return false;
      const refreshed = await query.refetch();
      if (signal.aborted) return false;
      if (refreshed.isError) {
        setError(
          'The server accepted the action, but current access could not be reloaded. Refresh before continuing.',
        );
      } else {
        setMustReload(false);
        setMessage(success);
        onSaved?.();
        return true;
      }
    } catch (failure) {
      if (signal.aborted) return false;
      const definitive = failure instanceof ApiError && failure.status < 500;
      if (definitive) assignmentIntent.current = null;
      const replayable = !definitive && isAssignment && !!assignmentIntent.current;
      setUncertainAssignment(replayable);
      setError(
        definitive
          ? `${accessErrors[failure.code ?? ''] ?? errorMessage(failure)} Refresh current access before another action.`
          : replayable
            ? 'The result is uncertain. Retry the same assignment with its saved key, or refresh to reconcile current access.'
            : 'The result is uncertain. Refresh and check current access before another action. Do not resend credentials automatically.',
      );
    } finally {
      active.current = false;
      if (!signal.aborted) setPending(false);
    }
    return false;
  }

  if (!approved)
    return <p className="hr-notice">Access setup requires an approved hiring employee.</p>;
  if (!canRead) return <Denied />;
  return (
    <section aria-label="Employee access setup">
      <div className="hr-actions">
        <h2>Employee access</h2>
        <Button
          variant="secondary"
          disabled={pending || query.isFetching}
          onClick={() => void reload()}
        >
          <RefreshCw size={16} /> Refresh
        </Button>
      </div>
      {query.isPending && <p role="status">Loading current access...</p>}
      {query.isError && (
        <p role="alert" className="hr-message hr-message--error">
          {errorMessage(query.error)}
        </p>
      )}
      {error && (
        <p role="alert" className="hr-message hr-message--error">
          {error}
        </p>
      )}
      {uncertainAssignment && (
        <Button
          variant="secondary"
          disabled={pending || query.isFetching}
          onClick={() => {
            const intent = assignmentIntent.current;
            if (intent)
              void write(
                intent.path,
                intent.body,
                initialAccessResultSchema,
                'Assignment confirmed and current access reloaded.',
                intent.additional,
                true,
              );
          }}
        >
          Retry same assignment
        </Button>
      )}
      {message && (
        <p role="status" className="hr-message">
          {message}
        </p>
      )}
      {setup && !query.isError && (
        <>
          <dl className="hr-dl">
            <div>
              <dt>Account status</dt>
              <dd>{account ? accessStatusLabels[account.status] : 'No existing account'}</dd>
            </div>
            <div>
              <dt>Initial access assigned</dt>
              <dd>{setup.initialAccessAssigned ? 'Yes' : 'No'}</dd>
            </div>
            <div>
              <dt>Mobile requested in hiring</dt>
              <dd>{setup.mobileAccessRequested ? 'Yes' : 'No'}</dd>
            </div>
            <div>
              <dt>Phone enrolled</dt>
              <dd>{setup.mobilePhoneEnrolled ? 'Yes' : 'No'}</dd>
            </div>
            <div>
              <dt>Mobile channel</dt>
              <dd>{account?.mobileAccessEnabled ? 'Enabled on server' : 'Not enabled'}</dd>
            </div>
            <div>
              <dt>Web channel</dt>
              <dd>{account?.webAccessEnabled ? 'Enabled on server' : 'Not enabled'}</dd>
            </div>
            <div>
              <dt>MFA required</dt>
              <dd>{account?.mfaRequired ? 'Yes' : 'No'}</dd>
            </div>
          </dl>
          {!account && (
            <p className="hr-notice">
              No existing account is available. Account creation requires the approved backend
              handoff.
            </p>
          )}
          {self && (
            <p className="hr-notice">
              Administrators cannot configure their own onboarding access.
            </p>
          )}
          {!access.user?.account && (
            <p className="hr-notice">
              Current administrator identity is unavailable. Reload your session before configuring
              access.
            </p>
          )}
          {query.isStale && (
            <p className="hr-notice">
              This access snapshot is stale. Refresh before configuring access.
            </p>
          )}
          {data?.assignments.map((assignment) => (
            <p key={assignment.id}>
              {assignment.roleCode} &middot; {assignment.revokedAt ? 'Revoked' : 'Assigned'}{' '}
              &middot; Effective {assignment.effectiveFrom}
              {assignment.effectiveTo ? ` to ${assignment.effectiveTo}` : ''} &middot; Scopes{' '}
              {assignment.scopes
                .map(
                  (scope) =>
                    `${['', 'Self', 'Employee', 'Department', 'Team', 'Site', 'Project', 'All'][scope.kind]}${scope.targetId ? ` (${scope.targetId})` : ''}`,
                )
                .join(', ')}
            </p>
          ))}
          <h3>Initial role</h3>
          {!canRoles ? (
            <p className="hr-notice">Role catalogue access requires access.roles.read.</p>
          ) : (
            <>
              <SelectInput
                label="Existing role"
                value={roleId}
                disabled={locked || !!setup.initialAccessAssigned || !account}
                options={[
                  { value: '', label: 'Select a role' },
                  ...selectable.map((item) => ({
                    value: item.id,
                    label: `${item.name} (${item.code})`,
                  })),
                ]}
                onChange={(event) => setRoleId(event.target.value)}
              />
              {!selectable.length && (
                <p className="hr-notice">
                  No active non-system role with delegable catalogue permissions is available.
                </p>
              )}
              {role && (
                <ul>
                  {role.permissions.map((code) => {
                    const definition = data?.catalog.find((item) => item.code === code);
                    return (
                      <li key={code}>
                        {code}: {definition?.description ?? 'Unsupported permission'}
                        {definition?.sensitive ? ' (sensitive)' : ''}
                        {definition?.management ? ' (management)' : ''}
                        {definition?.selfService ? ' (self-service)' : ''}
                      </li>
                    );
                  })}
                </ul>
              )}
              <p className="muted">
                Initial access uses Self scope. Additional role access is configured separately
                below. System-role assignment requires separate access administration. The server
                verifies All-scope delegation for every role permission.
              </p>
              <TextInput
                label="Reason for initial access"
                required
                maxLength={1000}
                value={reason}
                disabled={locked || !!setup.initialAccessAssigned || !account}
                onChange={(event) => setReason(event.target.value)}
              />
              <Button
                disabled={!canAssign}
                onClick={() => {
                  if (!canAssign || !account || !role) return;
                  void write(
                    `/access/accounts/${account.id}/assignments`,
                    {
                      roleId: role.id,
                      scopes: [{ kind: 1, targetId: null }],
                      effectiveFrom: new Date().toISOString(),
                      effectiveTo: null,
                      expectedAccountVersion: account.version,
                      expectedRoleVersion: role.version,
                      configureAsInitialAccess: true,
                      reason,
                    },
                    initialAccessResultSchema,
                    'Initial Self access saved and current access reloaded.',
                  );
                }}
              >
                <ShieldCheck size={16} /> Assign initial access
              </Button>
            </>
          )}
          {account && setup.initialAccessAssigned && data && (
            <AdditionalRoleAssignment
              key={additionalRevision}
              account={account}
              roles={selectable}
              catalog={data.catalog}
              disabled={!canAdditional}
              onDirty={setAdditionalDirty}
              onAssign={async (payload) => {
                if (!canAdditional) return false;
                if (
                  (email || passwordDirty) &&
                  !window.confirm('Discard unsaved web credentials and assign the additional role?')
                )
                  return false;
                clearPasswords();
                setEmail('');
                return write(
                  `/access/accounts/${account.id}/assignments`,
                  payload,
                  initialAccessResultSchema,
                  'Additional role assignment saved and current access reloaded. Login channels are unchanged.',
                  true,
                );
              }}
            />
          )}
          <h3>Mobile enrollment</h3>
          <p className="muted">
            Enrollment uses the approved hiring phone. Employee verification and activation still
            occur through the mobile sign-in flow.
          </p>
          <Button
            disabled={!canMobile}
            onClick={() => {
              if (canMobile) {
                if (
                  (email || passwordDirty) &&
                  !window.confirm('Discard unsaved web credentials and provision mobile access?')
                )
                  return;
                clearPasswords();
                setEmail('');
                void write(
                  `/hr/employees/${employeeId}/mobile-provisioning`,
                  undefined,
                  mobileProvisioningResultSchema,
                  'Mobile provisioning completed and current access reloaded.',
                );
              }
            }}
          >
            <Smartphone size={16} /> Provision mobile access
          </Button>
          {setup.mobilePhoneEnrolled && !account?.mobileAccessEnabled && (
            <p className="hr-notice">
              Phone enrollment already exists. Mobile channel recovery or re-enablement requires
              separate access administration.
            </p>
          )}
          <h3>First web credentials</h3>
          <p className="muted">
            This sets the first email and password only. Approved phone, joining date, employment,
            invitation and credential checks remain server-controlled. Web login still requires SMS
            verification; Self access does not grant broader management access.
          </p>
          {!delegatedTarget && (
            <p className="hr-notice">
              Credential and mobile setup require the role catalogue, known non-system assignments
              and delegation of all assigned permissions. System-account setup requires separate
              SuperAdmin administration.
            </p>
          )}
          {account?.webAccessEnabled ? (
            <p>
              Web access is enabled on the server. Existing credentials cannot be replaced here.
            </p>
          ) : (
            <form
              ref={bindCredentialForm}
              onSubmit={(event) => {
                event.preventDefault();
                const fields = new FormData(event.currentTarget);
                const password = String(fields.get('password') ?? '');
                const confirmation = String(fields.get('confirmation') ?? '');
                clearPasswords();
                if (!canWeb || !account) return;
                const payload = firstWebCredentialsSchema.safeParse({
                  email: email.trim(),
                  password,
                  expectedAccountVersion: account.version,
                });
                if (!payload.success || password !== confirmation) {
                  setError(
                    'Enter a valid email and matching non-empty passwords (maximum 1024 characters). The server enforces its configured password policy.',
                  );
                  return;
                }
                void write(
                  `/access/accounts/${account.id}/web-credentials`,
                  payload.data,
                  accessAccountSchema,
                  'First web credentials accepted and current access reloaded. Login eligibility and SMS verification are checked at sign-in.',
                );
              }}
            >
              <fieldset disabled={!canWeb} className="hr-form-grid">
                <TextInput
                  label="Web login email"
                  name="email"
                  type="email"
                  autoComplete="off"
                  required
                  maxLength={320}
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                />
                <PasswordInput
                  label="First password"
                  name="password"
                  autoComplete="new-password"
                  required
                  maxLength={1024}
                  onChange={() => setPasswordDirty(true)}
                />
                <PasswordInput
                  label="Confirm first password"
                  name="confirmation"
                  autoComplete="new-password"
                  required
                  maxLength={1024}
                  onChange={() => setPasswordDirty(true)}
                />
                <Button type="submit">
                  <KeyRound size={16} /> Set first web credentials
                </Button>
              </fieldset>
            </form>
          )}
        </>
      )}
      {account && (
        <AuthenticatorReset
          accountId={account.id}
          employeeId={account.employeeId}
          version={account.version}
          enabled={account.authenticatorRequired}
          onSaved={() => {
            setMustReload(true);
            onSaved?.();
          }}
        />
      )}
    </section>
  );
}
