import { EmployeeReference } from '@/components/employees/EmployeeReference';
import { useCallback, useEffect, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import {
  KeyRound,
  RefreshCw,
  ShieldCheck,
  Smartphone,
  Globe,
  Lock,
  UserCheck,
  CheckCircle2,
  AlertCircle,
  Shield,
} from 'lucide-react';
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
  initialRoleAssignmentSchema,
  additionalRoleAssignmentSchema,
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
    active.current = true;
    setPending(true);
    setMustReload(true);
    setError('');
    setMessage('');
    try {
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

  const rolePermissions = role ? role.permissions : [];

  return (
    <section className="emp-access-container" aria-label="Employee access setup">
      {/* Header */}
      <div className="emp-access-header">
        <div className="emp-access-header-left">
          <h2 className="emp-access-title">Employee Access & Channels</h2>
          <p className="emp-access-subtitle">
            Configure baseline role, login credentials (Web portal & Mobile app), and delegated scopes.
          </p>
        </div>
        <Button
          variant="secondary"
          className="access-btn"
          disabled={pending || query.isFetching}
          onClick={() => void reload()}
        >
          <RefreshCw size={13} className={query.isFetching ? 'animate-spin' : ''} />
          <span>{query.isFetching ? 'Refreshing…' : 'Refresh'}</span>
        </Button>
      </div>

      {query.isPending && (
        <p role="status" style={{ fontSize: '12.5px', color: '#64748b', margin: 0 }}>
          Loading current access setup…
        </p>
      )}
      {query.isError && (
        <p role="alert" className="hr-message hr-message--error" style={{ margin: 0 }}>
          {errorMessage(query.error)}
        </p>
      )}
      {error && (
        <p role="alert" className="hr-message hr-message--error" style={{ margin: 0 }}>
          {error}
        </p>
      )}
      {message && (
        <p role="status" className="hr-message hr-message--success" style={{ margin: 0 }}>
          {message}
        </p>
      )}

      {uncertainAssignment && (
        <div
          style={{
            background: '#fffbeb',
            border: '1px solid #fde68a',
            borderRadius: '6px',
            padding: '12px 14px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: '12px',
          }}
        >
          <span style={{ fontSize: '12px', color: '#92400e' }}>
            Previous assignment status could not be verified. Retry with identical saved key or refresh.
          </span>
          <Button
            variant="secondary"
            className="access-btn"
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
        </div>
      )}

      {setup && !query.isError && (
        <>
          {/* 1. Account & Channels Overview Cards */}
          <div className="access-overview-grid">
            {/* Stat 1: Account Status */}
            <div className="access-stat-card">
              <div className="access-stat-top">
                <div className="access-stat-icon access-stat-icon--account">
                  <UserCheck size={14} />
                </div>
                <span className="access-stat-label">Account</span>
              </div>
              <div className="access-stat-value">
                {account ? (
                  account.status === 0 ? (
                    <span
                      style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '4px',
                        background: '#fffbeb',
                        color: '#b45309',
                        border: '1px solid #fde68a',
                        padding: '2px 7px',
                        borderRadius: '4px',
                        fontSize: '11px',
                        fontWeight: 600,
                      }}
                    >
                      Pending Setup
                    </span>
                  ) : account.status === 1 ? (
                    <span
                      style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '4px',
                        background: '#eff6ff',
                        color: '#2563eb',
                        border: '1px solid #dbeafe',
                        padding: '2px 7px',
                        borderRadius: '4px',
                        fontSize: '11px',
                        fontWeight: 600,
                      }}
                    >
                      Ready for Invite
                    </span>
                  ) : account.status === 3 ? (
                    <span
                      style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '4px',
                        background: '#ecfdf5',
                        color: '#047857',
                        border: '1px solid #a7f3d0',
                        padding: '2px 7px',
                        borderRadius: '4px',
                        fontSize: '11px',
                        fontWeight: 600,
                      }}
                    >
                      Active
                    </span>
                  ) : (
                    <span
                      style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '4px',
                        background: '#f8fafc',
                        color: '#475569',
                        border: '1px solid #e2e8f0',
                        padding: '2px 7px',
                        borderRadius: '4px',
                        fontSize: '11px',
                        fontWeight: 600,
                      }}
                    >
                      {accessStatusLabels[account.status]}
                    </span>
                  )
                ) : (
                  <span style={{ fontSize: '11.5px', color: '#94a3b8' }}>No account</span>
                )}
              </div>
            </div>

            {/* Stat 2: Initial Role */}
            <div className="access-stat-card">
              <div className="access-stat-top">
                <div className="access-stat-icon access-stat-icon--role">
                  <ShieldCheck size={14} />
                </div>
                <span className="access-stat-label">Initial Role</span>
              </div>
              <div className="access-stat-value">
                {setup.initialAccessAssigned ? (
                  <span
                    style={{
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: '4px',
                      background: '#ecfdf5',
                      color: '#047857',
                      border: '1px solid #a7f3d0',
                      padding: '2px 7px',
                      borderRadius: '4px',
                      fontSize: '11px',
                      fontWeight: 600,
                    }}
                  >
                    <CheckCircle2 size={11} /> Assigned (Self)
                  </span>
                ) : (
                  <span
                    style={{
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: '4px',
                      background: '#fef2f2',
                      color: '#b91c1c',
                      border: '1px solid #fecaca',
                      padding: '2px 7px',
                      borderRadius: '4px',
                      fontSize: '11px',
                      fontWeight: 600,
                    }}
                  >
                    <AlertCircle size={11} /> Not Assigned
                  </span>
                )}
              </div>
            </div>

            {/* Stat 3: Web Channel */}
            <div className="access-stat-card">
              <div className="access-stat-top">
                <div className="access-stat-icon access-stat-icon--web">
                  <Globe size={14} />
                </div>
                <span className="access-stat-label">Web Portal</span>
              </div>
              <div className="access-stat-value">
                {account?.webAccessEnabled ? (
                  <span
                    style={{
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: '4px',
                      background: '#ecfdf5',
                      color: '#047857',
                      border: '1px solid #a7f3d0',
                      padding: '2px 7px',
                      borderRadius: '4px',
                      fontSize: '11px',
                      fontWeight: 600,
                    }}
                  >
                    <CheckCircle2 size={11} /> Active
                  </span>
                ) : (
                  <span
                    style={{
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: '4px',
                      background: '#f8fafc',
                      color: '#64748b',
                      border: '1px solid #e2e8f0',
                      padding: '2px 7px',
                      borderRadius: '4px',
                      fontSize: '11px',
                      fontWeight: 600,
                    }}
                  >
                    Disabled
                  </span>
                )}
              </div>
            </div>

            {/* Stat 4: Mobile App */}
            <div className="access-stat-card">
              <div className="access-stat-top">
                <div className="access-stat-icon access-stat-icon--mobile">
                  <Smartphone size={14} />
                </div>
                <span className="access-stat-label">Mobile App</span>
              </div>
              <div className="access-stat-value">
                {account?.mobileAccessEnabled ? (
                  <span
                    style={{
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: '4px',
                      background: '#ecfdf5',
                      color: '#047857',
                      border: '1px solid #a7f3d0',
                      padding: '2px 7px',
                      borderRadius: '4px',
                      fontSize: '11px',
                      fontWeight: 600,
                    }}
                  >
                    <CheckCircle2 size={11} /> Active
                  </span>
                ) : setup.mobileAccessRequested ? (
                  <span
                    style={{
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: '4px',
                      background: '#eff6ff',
                      color: '#2563eb',
                      border: '1px solid #dbeafe',
                      padding: '2px 7px',
                      borderRadius: '4px',
                      fontSize: '11px',
                      fontWeight: 600,
                    }}
                  >
                    Requested
                  </span>
                ) : (
                  <span
                    style={{
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: '4px',
                      background: '#f8fafc',
                      color: '#64748b',
                      border: '1px solid #e2e8f0',
                      padding: '2px 7px',
                      borderRadius: '4px',
                      fontSize: '11px',
                      fontWeight: 600,
                    }}
                  >
                    Not Requested
                  </span>
                )}
              </div>
            </div>
          </div>

          {!account && (
            <div className="hr-notice" style={{ margin: 0 }}>
              No existing account is available. Account creation requires the approved backend handoff.
            </div>
          )}
          {self && (
            <div className="hr-notice" style={{ margin: 0 }}>
              Administrators cannot configure their own onboarding access.
            </div>
          )}
          {!access.user?.account && (
            <div className="hr-notice" style={{ margin: 0 }}>
              Current administrator identity is unavailable. Reload your session before configuring access.
            </div>
          )}

          {/* 2. Active Role Assignments List */}
          {data?.assignments && data.assignments.length > 0 && (
            <div className="access-step-card">
              <div className="access-card-head">
                <div className="access-card-head-left">
                  <div className="access-card-icon-badge">
                    <ShieldCheck size={16} />
                  </div>
                  <div>
                    <h3 className="access-card-title">
                      Currently Assigned Roles ({data.assignments.length})
                    </h3>
                    <p className="access-card-desc">
                      Active and historical role assignments for this employee
                    </p>
                  </div>
                </div>
              </div>
              <div className="access-assignments-list">
                {data.assignments.map((assignment) => (
                  <div key={assignment.id} className="access-assignment-item">
                    <div className="access-assignment-left">
                      <span className="access-assignment-code">{assignment.roleCode}</span>
                      <span
                        style={{
                          fontSize: '11px',
                          fontWeight: 600,
                          padding: '2px 7px',
                          borderRadius: '4px',
                          background: assignment.revokedAt ? '#fef2f2' : '#ecfdf5',
                          color: assignment.revokedAt ? '#dc2626' : '#047857',
                          border: `1px solid ${assignment.revokedAt ? '#fecaca' : '#a7f3d0'}`,
                        }}
                      >
                        {assignment.revokedAt ? 'Revoked' : 'Active'}
                      </span>
                      {assignment.scopes.map((scope, index) => (
                        <span key={index} className="access-assignment-scope">
                          {
                            ['', 'Self', 'Employee', 'Department', 'Team', 'Site', 'Project', 'All'][
                              scope.kind
                            ]
                          }
                          {scope.targetId && (
                            <>
                              {' '}
                              (
                              {scope.kind === 2 ? (
                                <EmployeeReference employeeId={scope.targetId} />
                              ) : (
                                `${scope.targetId.slice(0, 8)}…`
                              )}
                              )
                            </>
                          )}
                        </span>
                      ))}
                    </div>
                    <span className="access-assignment-date">
                      Effective: {new Date(assignment.effectiveFrom).toLocaleDateString()}
                      {assignment.effectiveTo
                        ? ` – ${new Date(assignment.effectiveTo).toLocaleDateString()}`
                        : ' (Permanent)'}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* 3. Step 1: Initial Role Assignment */}
          <div className="access-step-card">
            <div className="access-card-head">
              <div className="access-card-head-left">
                <div className="access-card-icon-badge">
                  <ShieldCheck size={16} />
                </div>
                <div>
                  <h3 className="access-card-title">Step 1: Baseline Initial Role</h3>
                  <p className="access-card-desc">
                    Assign the initial Self-service role required to unlock web portal and mobile app channels.
                  </p>
                </div>
              </div>
              {setup.initialAccessAssigned && (
                <span
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '4px',
                    background: '#ecfdf5',
                    color: '#047857',
                    border: '1px solid #a7f3d0',
                    padding: '3px 8px',
                    borderRadius: '12px',
                    fontSize: '11px',
                    fontWeight: 600,
                  }}
                >
                  <CheckCircle2 size={12} /> Assigned
                </span>
              )}
            </div>

            {!canRoles ? (
              <p className="hr-notice" style={{ margin: 0 }}>
                Role catalogue access requires access.roles.read permission.
              </p>
            ) : setup.initialAccessAssigned ? (
              <div
                style={{
                  background: '#f8fafc',
                  border: '1px solid #e2e8f0',
                  borderRadius: '6px',
                  padding: '12px 14px',
                  fontSize: '12.5px',
                  color: '#334155',
                }}
              >
                <CheckCircle2
                  size={15}
                  style={{
                    color: '#16a34a',
                    display: 'inline',
                    marginRight: '6px',
                    verticalAlign: 'middle',
                  }}
                />
                <strong>Initial baseline role is already assigned.</strong> You can now configure web
                login credentials, mobile access, or grant additional scoped roles below.
              </div>
            ) : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                <div
                  style={{
                    display: 'grid',
                    gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))',
                    gap: '12px 16px',
                    alignItems: 'flex-start',
                  }}
                >
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
                    <SelectInput
                      label="Existing baseline role"
                      value={roleId}
                      disabled={locked || !account}
                      options={[
                        { value: '', label: 'Select a role (e.g. Employee Self Service)' },
                        ...selectable.map((item) => ({
                          value: item.id,
                          label: `${item.name} (${item.code})`,
                        })),
                      ]}
                      onChange={(event) => setRoleId(event.target.value)}
                    />
                    {role && (
                      <details style={{ fontSize: '11.5px', color: '#64748b', marginTop: '2px' }}>
                        <summary style={{ cursor: 'pointer', fontWeight: 550, color: '#2563eb' }}>
                          View {rolePermissions.length} included permissions
                        </summary>
                        <div
                          style={{
                            maxHeight: '130px',
                            overflowY: 'auto',
                            background: '#f8fafc',
                            border: '1px solid #e2e8f0',
                            borderRadius: '5px',
                            padding: '8px 10px',
                            marginTop: '6px',
                          }}
                        >
                          <ul style={{ margin: 0, paddingLeft: '16px' }}>
                            {rolePermissions.map((code) => {
                              const definition = data?.catalog.find((item) => item.code === code);
                              return (
                                <li key={code}>
                                  <strong>{code}</strong>:{' '}
                                  {definition?.description ?? 'Standard permission'}
                                </li>
                              );
                            })}
                          </ul>
                        </div>
                      </details>
                    )}
                  </div>

                  <TextInput
                    label="Reason for initial access"
                    required
                    maxLength={1000}
                    placeholder="e.g. Baseline onboarding self-service access"
                    value={reason}
                    disabled={locked || !account}
                    onChange={(event) => setReason(event.target.value)}
                  />
                </div>

                <div style={{ display: 'flex', justifyContent: 'flex-start', paddingTop: '4px' }}>
                  <Button
                    variant="primary"
                    className="access-btn"
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
                    <ShieldCheck size={14} />
                    <span>Assign Initial Role</span>
                  </Button>
                </div>
              </div>
            )}
          </div>

          {/* 4. Step 2: Access Channels (Web & Mobile) */}
          <div className="access-step-card">
            <div className="access-card-head">
              <div className="access-card-head-left">
                <div
                  className="access-card-icon-badge"
                  style={{ background: '#f5f3ff', color: '#7c3aed', borderColor: '#ede9fe' }}
                >
                  <KeyRound size={16} />
                </div>
                <div>
                  <h3 className="access-card-title">Step 2: Sign-In Channels & Credentials</h3>
                  <p className="access-card-desc">
                    Configure web login credentials and provision mobile app enrollment.
                  </p>
                </div>
              </div>
            </div>

            {!setup.initialAccessAssigned ? (
              <div
                style={{
                  background: '#f8fafc',
                  border: '1px dashed #cbd5e1',
                  borderRadius: '6px',
                  padding: '16px',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '12px',
                }}
              >
                <Lock size={18} style={{ color: '#94a3b8', flexShrink: 0 }} />
                <span style={{ fontSize: '12.5px', color: '#64748b' }}>
                  Web credentials and mobile access will become available immediately once an initial
                  baseline role is assigned in Step 1 above.
                </span>
              </div>
            ) : (
              <div className="access-channels-grid">
                {/* Channel A: Web Portal */}
                <div className="access-channel-box">
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      <Globe size={15} style={{ color: '#2563eb' }} />
                      <strong style={{ fontSize: '13px', color: '#0f172a' }}>
                        Web Portal Access
                      </strong>
                    </div>
                    {account?.webAccessEnabled && (
                      <span
                        style={{
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: '4px',
                          background: '#ecfdf5',
                          color: '#047857',
                          border: '1px solid #a7f3d0',
                          padding: '2px 7px',
                          borderRadius: '10px',
                          fontSize: '10.5px',
                          fontWeight: 600,
                        }}
                      >
                        <CheckCircle2 size={11} /> Enabled
                      </span>
                    )}
                  </div>

                  {account?.webAccessEnabled ? (
                    <p style={{ margin: 0, fontSize: '12px', color: '#64748b' }}>
                      Web access is active on the server. Existing web credentials cannot be
                      overwritten from this onboarding panel.
                    </p>
                  ) : (
                    <form
                      ref={bindCredentialForm}
                      style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}
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
                      <TextInput
                        label="Web login email"
                        name="email"
                        type="email"
                        autoComplete="off"
                        required
                        maxLength={320}
                        placeholder="work.email@cosmix.invalid"
                        value={email}
                        onChange={(event) => setEmail(event.target.value)}
                      />
                      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                        <PasswordInput
                          label="First password"
                          name="password"
                          autoComplete="new-password"
                          required
                          maxLength={1024}
                          placeholder="Temporary password"
                          onChange={() => setPasswordDirty(true)}
                        />
                        <PasswordInput
                          label="Confirm password"
                          name="confirmation"
                          autoComplete="new-password"
                          required
                          maxLength={1024}
                          placeholder="Confirm password"
                          onChange={() => setPasswordDirty(true)}
                        />
                      </div>
                      <div style={{ paddingTop: '4px' }}>
                        <Button
                          type="submit"
                          variant="primary"
                          className="access-btn"
                          disabled={!canWeb}
                        >
                          <KeyRound size={13} />
                          <span>Set Web Credentials</span>
                        </Button>
                      </div>
                    </form>
                  )}
                </div>

                {/* Channel B: Mobile App */}
                <div className="access-channel-box">
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      <Smartphone size={15} style={{ color: '#ea580c' }} />
                      <strong style={{ fontSize: '13px', color: '#0f172a' }}>
                        Mobile App Enrollment
                      </strong>
                    </div>
                    {account?.mobileAccessEnabled && (
                      <span
                        style={{
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: '4px',
                          background: '#ecfdf5',
                          color: '#047857',
                          border: '1px solid #a7f3d0',
                          padding: '2px 7px',
                          borderRadius: '10px',
                          fontSize: '10.5px',
                          fontWeight: 600,
                        }}
                      >
                        <CheckCircle2 size={11} /> Enabled
                      </span>
                    )}
                  </div>

                  <p
                    style={{
                      margin: 0,
                      fontSize: '12px',
                      color: '#64748b',
                      lineHeight: 1.5,
                    }}
                  >
                    Enrollment uses the approved hiring phone number. Verification and device
                    activation occur through the mobile sign-in flow.
                  </p>

                  <div style={{ marginTop: 'auto', paddingTop: '8px' }}>
                    <Button
                      variant="secondary"
                      className="access-btn"
                      disabled={!canMobile}
                      onClick={() => {
                        if (canMobile) {
                          if (
                            (email || passwordDirty) &&
                            !window.confirm(
                              'Discard unsaved web credentials and provision mobile access?',
                            )
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
                      <Smartphone size={13} />
                      <span>Provision Mobile Access</span>
                    </Button>
                  </div>

                  {setup.mobilePhoneEnrolled && !account?.mobileAccessEnabled && (
                    <p className="hr-notice" style={{ margin: '8px 0 0', fontSize: '11.5px' }}>
                      Phone enrollment already exists. Mobile channel recovery requires separate access
                      administration.
                    </p>
                  )}
                </div>
              </div>
            )}
          </div>

          {/* 5. Additional Role Assignments (Delegated Scopes) */}
          {account && setup.initialAccessAssigned && data && (
            <div className="access-step-card">
              <div className="access-card-head">
                <div className="access-card-head-left">
                  <div className="access-card-icon-badge">
                    <Shield size={16} />
                  </div>
                  <div>
                    <h3 className="access-card-title">Additional Roles & Custom Scopes</h3>
                    <p className="access-card-desc">
                      Assign elevated roles with Department, Site, Team, or All scope if this employee
                      requires management rights.
                    </p>
                  </div>
                </div>
              </div>

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
                    !window.confirm(
                      'Discard unsaved web credentials and assign the additional role?',
                    )
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
            </div>
          )}

          {/* 6. Two-Factor / Authenticator Reset */}
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
        </>
      )}
    </section>
  );
}
