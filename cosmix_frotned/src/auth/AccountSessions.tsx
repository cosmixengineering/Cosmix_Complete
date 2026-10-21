import { useId, useLayoutEffect, useRef, useState, type ReactNode } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ChevronLeft, ChevronRight, LogOut, Monitor, RefreshCw, Smartphone } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { useConfirm } from '@/components/feedback/ConfirmDialog';
import { ApiError } from '@/services/api/api-error';
import { request } from '@/services/api/http-client';
import { useAuth } from './use-auth';
import type { SessionUser } from './types';
import {
  accountSessionContract as contract,
  devicesPageSchema,
  sessionsPageSchema,
  hasOwnAccountPermission,
  type DeviceResponse,
  type SessionSummary,
} from './session-contracts';

export interface AccountSessionsProps {
  className?: string;
}

function message(error: unknown) {
  return error instanceof ApiError
    ? error.message
    : 'Account records could not be loaded. Please refresh and try again.';
}

function date(value: string | null) {
  return value === null ? 'Not recorded' : new Date(value).toLocaleString();
}

function Metadata({ children }: { children: ReactNode }) {
  return (
    <dl className="account-details" style={{ marginBottom: 12, overflowWrap: 'anywhere' }}>
      {children}
    </dl>
  );
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <dt>{label}</dt>
      <dd style={{ maxWidth: '100%', overflowWrap: 'anywhere' }}>{children}</dd>
    </div>
  );
}

function Pager({
  page,
  count,
  disabled,
  onPage,
}: {
  page: number;
  count: number;
  disabled: boolean;
  onPage: (page: number) => void;
}) {
  return (
    <div
      style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 12, margin: '16px 0' }}
    >
      <Button
        variant="secondary"
        title="Previous page"
        aria-label="Previous page"
        disabled={disabled || page === 1}
        onClick={() => onPage(page - 1)}
      >
        <ChevronLeft size={16} />
      </Button>
      <span aria-live="polite">Page {page}</span>
      <Button
        variant="secondary"
        title="Next page"
        aria-label="Next page"
        disabled={disabled || count < contract.pageSize || page >= contract.maxPage}
        onClick={() => onPage(page + 1)}
      >
        <ChevronRight size={16} />
      </Button>
    </div>
  );
}

export function AccountSessions({ className }: AccountSessionsProps) {
  const { user, status } = useAuth();
  if (status !== 'authenticated' || !user) return null;
  if (!hasOwnAccountPermission(user, contract.permissions.manage)) {
    return (
      <section className={className}>
        <h2>Sessions and devices</h2>
        <p role="status">
          Your current permissions do not allow access to your sessions and devices.
        </p>
      </section>
    );
  }
  const subject =
    user.account?.accountId ??
    user.grants.find(
      (grant) =>
        grant.permission === contract.permissions.manage && grant.scopeKind === contract.scopeKind,
    )?.targetId;
  return (
    <SessionLists
      key={subject}
      user={user}
      subject={subject ?? 'restricted'}
      className={className}
    />
  );
}

function SessionLists({
  user,
  subject,
  className,
}: AccountSessionsProps & { user: SessionUser; subject: string }) {
  const { status, retrySession } = useAuth();
  const confirm = useConfirm();
  const cache = useQueryClient();
  const titleId = useId();
  const [devicePage, setDevicePage] = useState(1);
  const [sessionPage, setSessionPage] = useState(1);
  const controller = useRef<AbortController | null>(null);
  const active = useRef<AbortSignal | null>(null);
  const [action, setAction] = useState<{
    owner: SessionUser;
    pending: boolean;
    error?: string;
    success?: string;
    uncertain?: boolean;
  } | null>(null);
  useLayoutEffect(() => {
    const current = new AbortController();
    controller.current = current;
    if (status !== 'authenticated') current.abort();
    return () => current.abort();
  }, [user, status]);
  const lifetime = () => controller.current?.signal ?? AbortSignal.abort();
  const prefix = ['account-sessions', subject];
  const devices = useQuery({
    queryKey: [...prefix, user.grants, contract.devices, devicePage, contract.pageSize],
    queryFn: async ({ signal }) => {
      const combined = AbortSignal.any([signal, lifetime()]);
      const result = await request(
        `${contract.devices}?page=${devicePage}&pageSize=${contract.pageSize}`,
        { signal: combined },
      );
      combined.throwIfAborted();
      return devicesPageSchema.parse(result);
    },
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });
  const sessions = useQuery({
    queryKey: [...prefix, user.grants, contract.sessions, sessionPage, contract.pageSize],
    queryFn: async ({ signal }) => {
      const combined = AbortSignal.any([signal, lifetime()]);
      const result = await request(
        `${contract.sessions}?page=${sessionPage}&pageSize=${contract.pageSize}`,
        { signal: combined },
      );
      combined.throwIfAborted();
      return sessionsPageSchema.parse(result);
    },
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });
  const ownAction = action?.owner === user ? action : null;
  const busy = ownAction?.pending === true;
  const currentSession = !sessions.isError
    ? sessions.data?.find((session) => session.isCurrent)
    : undefined;
  const currentDeviceId = currentSession?.deviceId;
  const reading = devices.isFetching || sessions.isFetching;
  const revokeDisabled = busy || reading || ownAction?.uncertain === true;

  async function refresh() {
    const signal = lifetime();
    if (signal.aborted || active.current === signal) return;
    active.current = signal;
    setAction({ owner: user, pending: true, uncertain: ownAction?.uncertain });
    try {
      // Refresh invalidates other visited pages too; never retain a stale revoke target.
      await cache.invalidateQueries({ queryKey: prefix, refetchType: 'none' });
      if (signal.aborted) return;
      await Promise.all([
        devices.refetch({ throwOnError: true }),
        sessions.refetch({ throwOnError: true }),
      ]);
      if (!signal.aborted) setAction({ owner: user, pending: false });
    } catch (error) {
      if (!signal.aborted)
        setAction({
          owner: user,
          pending: false,
          error: message(error),
          uncertain: ownAction?.uncertain,
        });
    } finally {
      if (active.current === signal) active.current = null;
    }
  }

  async function revoke(kind: 'devices' | 'sessions', record: DeviceResponse | SessionSummary) {
    const signal = lifetime();
    if (signal.aborted || active.current === signal || revokeDisabled) return;
    const isDevice = kind === 'devices';
    const current = isDevice
      ? currentSession
        ? currentDeviceId === record.id
        : undefined
      : (record as SessionSummary).isCurrent;
    const warning = isDevice
      ? 'Revoke this device and all of its sessions?'
      : 'Revoke this session?';
    const confirmed = await confirm({
      title: isDevice ? 'Revoke Device Access?' : 'Revoke Session Access?',
      message: `${warning}${current === true ? ' Your current session will end immediately.' : current === undefined ? ' This may end your current session.' : ''}`,
      confirmText: 'Revoke Access',
      cancelText: 'Keep Active',
      variant: 'danger',
    });
    if (!confirmed) return;
    if (signal.aborted) return;
    active.current = signal;
    setAction({ owner: user, pending: true });
    try {
      await request(`/me/${kind}/${encodeURIComponent(record.id)}`, { method: 'DELETE', signal });
      if (signal.aborted) return;
      if (current !== false) {
        await retrySession();
        return;
      }
      await cache.invalidateQueries({ queryKey: prefix });
      if (!signal.aborted)
        setAction({
          owner: user,
          pending: false,
          success: isDevice ? 'Device revoked.' : 'Session revoked.',
        });
    } catch (error) {
      if (signal.aborted) return;
      const uncertain = !(error instanceof ApiError) || error.status >= 500;
      setAction({
        owner: user,
        pending: false,
        uncertain,
        error: uncertain
          ? 'Revocation could not be confirmed. Refresh the records before trying again.'
          : message(error),
      });
      // An uncertain DELETE may already have ended the cookie session.
      if (uncertain && current !== false && !signal.aborted) await retrySession();
    } finally {
      if (active.current === signal) active.current = null;
    }
  }

  return (
    <section className={className} aria-labelledby={titleId} style={{ minWidth: 0 }}>
      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          justifyContent: 'space-between',
          alignItems: 'center',
          gap: 12,
          marginBottom: 16,
        }}
      >
        <h2 id={titleId} style={{ margin: 0 }}>
          Sessions and devices
        </h2>
        <Button
          variant="secondary"
          title="Refresh sessions and devices"
          aria-label="Refresh sessions and devices"
          disabled={busy || reading}
          onClick={() => void refresh()}
        >
          <RefreshCw size={16} />
        </Button>
      </div>
      {ownAction?.error && (
        <p className="auth-message auth-message--error" role="alert">
          {ownAction.error}
        </p>
      )}
      {ownAction?.success && (
        <p className="auth-message" role="status">
          {ownAction.success}
        </p>
      )}
      {busy && <p role="status">Updating account records...</p>}
      <section aria-labelledby={`${titleId}-sessions`} aria-busy={sessions.isFetching}>
        <h3 id={`${titleId}-sessions`} style={{ fontSize: 16 }}>
          Active sessions
        </h3>
        {sessions.isPending && <p role="status">Loading sessions...</p>}
        {sessions.isError && (
          <p className="auth-message auth-message--error" role="alert">
            {message(sessions.error)}
          </p>
        )}
        {!sessions.isError && sessions.data?.length === 0 && (
          <p>No active sessions on this page.</p>
        )}
        {!sessions.isError &&
          sessions.data?.map((session) => (
            <article
              key={session.id}
              style={{ padding: '16px 0', borderBottom: '1px solid #e7e9ed' }}
            >
              <h4
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 8,
                  margin: '0 0 8px',
                  fontSize: 14,
                }}
              >
                {session.client === 1 ? <Monitor size={18} /> : <Smartphone size={18} />}
                {session.client === 1 ? 'Web session' : 'Mobile session'}
                {session.isCurrent && ' (Current session)'}
              </h4>
              <Metadata>
                <Field label="Session ID">{session.id}</Field>
                <Field label="Created">{date(session.createdAt)}</Field>
                <Field label="Expires">{date(session.expiresAt)}</Field>
                <Field label="Device ID">{session.deviceId ?? 'No registered device'}</Field>
              </Metadata>
              <Button
                variant="secondary"
                disabled={revokeDisabled}
                onClick={() => void revoke('sessions', session)}
              >
                <LogOut size={16} />
                Revoke session
              </Button>
            </article>
          ))}
        <Pager
          page={sessionPage}
          count={!sessions.isError ? (sessions.data?.length ?? 0) : 0}
          disabled={busy || sessions.isFetching}
          onPage={setSessionPage}
        />
      </section>
      <section
        aria-labelledby={`${titleId}-devices`}
        aria-busy={devices.isFetching}
        style={{ marginTop: 24 }}
      >
        <h3 id={`${titleId}-devices`} style={{ fontSize: 16 }}>
          Registered devices
        </h3>
        {devices.isPending && <p role="status">Loading devices...</p>}
        {devices.isError && (
          <p className="auth-message auth-message--error" role="alert">
            {message(devices.error)}
          </p>
        )}
        {!devices.isError && devices.data?.length === 0 && (
          <p>No registered devices on this page.</p>
        )}
        {!devices.isError &&
          devices.data?.map((device) => (
            <article
              key={device.id}
              style={{
                padding: '16px 0',
                borderBottom: '1px solid #e7e9ed',
                overflowWrap: 'anywhere',
              }}
            >
              <h4 style={{ margin: '0 0 8px', fontSize: 14 }}>
                {device.name}
                {device.id === currentDeviceId && ' (Current session device)'}
              </h4>
              <Metadata>
                <Field label="Platform">{device.platform}</Field>
                <Field label="Device ID">{device.id}</Field>
                <Field label="Registered">{date(device.registeredAt)}</Field>
                <Field label="Last seen">{date(device.lastSeenAt)}</Field>
                <Field label="Status">{device.revokedAt ? 'Revoked' : 'Registered'}</Field>
                {device.revokedAt && <Field label="Revoked">{date(device.revokedAt)}</Field>}
              </Metadata>
              {!device.revokedAt && (
                <Button
                  variant="secondary"
                  disabled={revokeDisabled || devices.isError}
                  onClick={() => void revoke('devices', device)}
                >
                  <LogOut size={16} />
                  Revoke device
                </Button>
              )}
            </article>
          ))}
        <Pager
          page={devicePage}
          count={!devices.isError ? (devices.data?.length ?? 0) : 0}
          disabled={busy || devices.isFetching}
          onPage={setDevicePage}
        />
      </section>
    </section>
  );
}
