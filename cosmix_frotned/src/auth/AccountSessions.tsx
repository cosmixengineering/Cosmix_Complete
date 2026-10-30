import { useId, useLayoutEffect, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ChevronLeft,
  ChevronRight,
  LogOut,
  Monitor,
  RefreshCw,
  Smartphone,
  Laptop,
  CheckCircle2,
  Copy,
  Check,
} from 'lucide-react';
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
  if (value === null) return 'Not recorded';
  return new Date(value).toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  });
}

function CopySessionChip({ value }: { value: string }) {
  const [copied, setCopied] = useState(false);
  return (
    <div
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: '5px',
        background: '#ffffff',
        border: '1px solid #e2e8f0',
        borderRadius: '4px',
        padding: '2px 6px',
        maxWidth: '100%',
      }}
    >
      <code
        style={{
          fontFamily: 'monospace',
          fontSize: '11px',
          fontWeight: 600,
          color: '#334155',
        }}
        title={value}
      >
        {value.length > 18 ? `${value.slice(0, 8)}…${value.slice(-6)}` : value}
      </code>
      <button
        type="button"
        style={{
          background: 'none',
          border: 'none',
          padding: 0,
          margin: 0,
          cursor: 'pointer',
          color: copied ? '#16a34a' : '#94a3b8',
          display: 'inline-flex',
          alignItems: 'center',
        }}
        title={copied ? 'Copied' : 'Copy ID'}
        onClick={() => {
          if (navigator.clipboard?.writeText) {
            void navigator.clipboard.writeText(value);
            setCopied(true);
            setTimeout(() => setCopied(false), 1500);
          }
        }}
      >
        {copied ? <Check size={11} /> : <Copy size={11} />}
      </button>
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
  if (page === 1 && count < contract.pageSize) return null;
  return (
    <div
      style={{
        display: 'flex',
        flexWrap: 'wrap',
        alignItems: 'center',
        justifyContent: 'flex-end',
        gap: 8,
        marginTop: 10,
      }}
    >
      <Button
        variant="secondary"
        title="Previous page"
        aria-label="Previous page"
        disabled={disabled || page === 1}
        onClick={() => onPage(page - 1)}
        style={{ height: '28px', padding: '0 9px', fontSize: '11.5px' }}
      >
        <ChevronLeft size={14} />
        <span>Previous</span>
      </Button>
      <span aria-live="polite" style={{ fontSize: '11.5px', color: '#64748b', fontWeight: 550 }}>
        Page {page}
      </span>
      <Button
        variant="secondary"
        title="Next page"
        aria-label="Next page"
        disabled={disabled || count < contract.pageSize || page >= contract.maxPage}
        onClick={() => onPage(page + 1)}
        style={{ height: '28px', padding: '0 9px', fontSize: '11.5px' }}
      >
        <span>Next</span>
        <ChevronRight size={14} />
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
        <div className="account-section-box">
          <p role="status" style={{ margin: 0, color: '#64748b', fontSize: '13px' }}>
            Your current permissions do not allow access to your sessions and devices.
          </p>
        </div>
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
      if (uncertain && current !== false && !signal.aborted) await retrySession();
    } finally {
      if (active.current === signal) active.current = null;
    }
  }

  return (
    <div
      className={`account-sessions-container ${className ?? ''}`}
      aria-labelledby={titleId}
      style={{ minWidth: 0 }}
    >
      {ownAction?.error && (
        <p className="hr-message hr-message--error" role="alert" style={{ margin: 0 }}>
          {ownAction.error}
        </p>
      )}
      {ownAction?.success && (
        <p className="hr-message hr-message--success" role="status" style={{ margin: 0 }}>
          {ownAction.success}
        </p>
      )}

      {/* Active Sessions Section */}
      <section
        className="account-section-box"
        aria-labelledby={`${titleId}-sessions`}
        aria-busy={sessions.isFetching}
      >
        <div className="account-section-header">
          <div className="account-section-header-left">
            <Monitor size={16} style={{ color: '#2563eb' }} />
            <h2 id={`${titleId}-sessions`} className="account-section-title">
              Active sessions
            </h2>
            {!sessions.isError && (
              <span className="account-section-count">{sessions.data?.length ?? 0} active</span>
            )}
          </div>
          <Button
            variant="secondary"
            title="Refresh sessions and devices"
            aria-label="Refresh sessions and devices"
            disabled={busy || reading}
            onClick={() => void refresh()}
            style={{
              height: '30px',
              padding: '0 10px',
              fontSize: '11.5px',
              display: 'inline-flex',
              alignItems: 'center',
              gap: '5px',
            }}
          >
            <RefreshCw size={12} className={reading ? 'animate-spin' : ''} />
            <span>{reading ? 'Refreshing…' : 'Refresh'}</span>
          </Button>
        </div>

        {sessions.isPending && (
          <p role="status" style={{ margin: '8px 0', fontSize: '12.5px', color: '#64748b' }}>
            Loading active sessions…
          </p>
        )}
        {sessions.isError && (
          <p className="hr-message hr-message--error" role="alert" style={{ margin: 0 }}>
            {message(sessions.error)}
          </p>
        )}
        {!sessions.isError && sessions.data?.length === 0 && (
          <div className="session-empty-state">No active sessions on this page.</div>
        )}
        {!sessions.isError && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
            {sessions.data?.map((session) => (
              <article
                key={session.id}
                className={`session-item-card ${session.isCurrent ? 'session-item-card--current' : ''}`}
              >
                <div className="session-item-topbar">
                  <div className="session-client-info">
                    <div
                      className={`session-client-icon ${
                        session.client === 1
                          ? 'session-client-icon--web'
                          : 'session-client-icon--mobile'
                      }`}
                    >
                      {session.client === 1 ? <Laptop size={16} /> : <Smartphone size={16} />}
                    </div>
                    <div
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '8px',
                        flexWrap: 'wrap',
                      }}
                    >
                      <span className="session-client-name">
                        {session.client === 1 ? 'Web session' : 'Mobile session'}
                      </span>
                      {session.isCurrent && (
                        <span className="session-current-pill">
                          <CheckCircle2 size={11} /> Current session
                        </span>
                      )}
                    </div>
                  </div>
                  <Button
                    variant="secondary"
                    className="session-revoke-btn"
                    disabled={revokeDisabled}
                    onClick={() => void revoke('sessions', session)}
                  >
                    <LogOut size={12} />
                    <span>Revoke session</span>
                  </Button>
                </div>

                <div className="session-meta-grid">
                  <div className="session-meta-item">
                    <span className="session-meta-label">Session ID</span>
                    <div>
                      <CopySessionChip value={session.id} />
                    </div>
                  </div>
                  <div className="session-meta-item">
                    <span className="session-meta-label">Signed In</span>
                    <span className="session-meta-val">{date(session.createdAt)}</span>
                  </div>
                  <div className="session-meta-item">
                    <span className="session-meta-label">Expires</span>
                    <span className="session-meta-val">{date(session.expiresAt)}</span>
                  </div>
                  <div className="session-meta-item">
                    <span className="session-meta-label">Device ID</span>
                    <div>
                      {session.deviceId ? (
                        <CopySessionChip value={session.deviceId} />
                      ) : (
                        <span
                          style={{
                            fontSize: '11.5px',
                            color: '#94a3b8',
                            fontStyle: 'italic',
                          }}
                        >
                          No registered device
                        </span>
                      )}
                    </div>
                  </div>
                </div>
              </article>
            ))}
          </div>
        )}

        <Pager
          page={sessionPage}
          count={!sessions.isError ? (sessions.data?.length ?? 0) : 0}
          disabled={busy || sessions.isFetching}
          onPage={setSessionPage}
        />
      </section>

      {/* Registered Devices Section */}
      <section
        className="account-section-box"
        aria-labelledby={`${titleId}-devices`}
        aria-busy={devices.isFetching}
      >
        <div className="account-section-header">
          <div className="account-section-header-left">
            <Smartphone size={16} style={{ color: '#0f766e' }} />
            <h2 id={`${titleId}-devices`} className="account-section-title">
              Registered devices
            </h2>
            {!devices.isError && (
              <span className="account-section-count">{devices.data?.length ?? 0} registered</span>
            )}
          </div>
        </div>

        {devices.isPending && (
          <p role="status" style={{ margin: '8px 0', fontSize: '12.5px', color: '#64748b' }}>
            Loading registered devices…
          </p>
        )}
        {devices.isError && (
          <p className="hr-message hr-message--error" role="alert" style={{ margin: 0 }}>
            {message(devices.error)}
          </p>
        )}
        {!devices.isError && devices.data?.length === 0 && (
          <div className="session-empty-state">No registered devices found on this account.</div>
        )}
        {!devices.isError && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
            {devices.data?.map((device) => (
              <article key={device.id} className="session-item-card">
                <div className="session-item-topbar">
                  <div className="session-client-info">
                    <div className="session-client-icon session-client-icon--mobile">
                      <Smartphone size={16} />
                    </div>
                    <div
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '8px',
                        flexWrap: 'wrap',
                      }}
                    >
                      <span className="session-client-name">{device.name || 'Mobile Device'}</span>
                      {device.id === currentDeviceId && (
                        <span className="session-current-pill">
                          <CheckCircle2 size={11} /> Current session device
                        </span>
                      )}
                    </div>
                  </div>
                  {!device.revokedAt && (
                    <Button
                      variant="secondary"
                      className="session-revoke-btn"
                      disabled={revokeDisabled || devices.isError}
                      onClick={() => void revoke('devices', device)}
                    >
                      <LogOut size={12} />
                      <span>Revoke device</span>
                    </Button>
                  )}
                </div>

                <div className="session-meta-grid">
                  <div className="session-meta-item">
                    <span className="session-meta-label">Platform</span>
                    <span className="session-meta-val">{device.platform || 'Unknown'}</span>
                  </div>
                  <div className="session-meta-item">
                    <span className="session-meta-label">Device ID</span>
                    <div>
                      <CopySessionChip value={device.id} />
                    </div>
                  </div>
                  <div className="session-meta-item">
                    <span className="session-meta-label">Registered</span>
                    <span className="session-meta-val">{date(device.registeredAt)}</span>
                  </div>
                  <div className="session-meta-item">
                    <span className="session-meta-label">Last Seen</span>
                    <span className="session-meta-val">{date(device.lastSeenAt)}</span>
                  </div>
                  <div className="session-meta-item">
                    <span className="session-meta-label">Status</span>
                    <span
                      style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '4px',
                        fontSize: '11.5px',
                        fontWeight: 600,
                        color: device.revokedAt ? '#dc2626' : '#16a34a',
                      }}
                    >
                      {device.revokedAt ? 'Revoked' : 'Active'}
                    </span>
                  </div>
                </div>
              </article>
            ))}
          </div>
        )}

        <Pager
          page={devicePage}
          count={!devices.isError ? (devices.data?.length ?? 0) : 0}
          disabled={busy || devices.isFetching}
          onPage={setDevicePage}
        />
      </section>
    </div>
  );
}
