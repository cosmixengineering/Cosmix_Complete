import { useState } from 'react';
import { Link } from 'react-router-dom';
import { LogOut, RefreshCw, ShieldCheck, Laptop, CheckCircle2 } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { useAuth } from '@/auth/use-auth';
import { AuthenticatorSettings } from '@/auth/AuthenticatorSettings';
import { workspaceHome, workspaceLabel } from '@/auth/workspace-access';

export function AccountPage() {
  const { user, signOut, retrySession } = useAuth();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function logout() {
    setBusy(true);
    setError(null);
    try {
      await signOut();
    } catch {
      setError('Sign out could not be confirmed. Please check your connection and try again.');
    } finally {
      setBusy(false);
    }
  }

  const rawDisplayName = user?.account?.displayName || 'User Profile';
  const roleName = workspaceLabel(user);
  const home = workspaceHome(user);

  // Parse clean name and custom role from parentheses if present, e.g. "Muhammad Rashid (Lead Engineer)"
  const parenMatch = rawDisplayName.match(/^(.*?)\s*\((.*?)\)$/);
  const displayName = parenMatch?.[1]?.trim() ?? rawDisplayName;
  const customRole = parenMatch?.[2]?.trim() ?? null;

  // Get 2-letter initials for avatar from clean name
  const initials =
    displayName
      .replace(/^Engr\.\s+/, '')
      .split(' ')
      .filter(Boolean)
      .map((part) => part[0])
      .join('')
      .slice(0, 2)
      .toUpperCase() || 'U';

  return (
    <div style={{ maxWidth: '800px', display: 'flex', flexDirection: 'column', gap: '14px' }}>
      <div className="hr-page-heading">
        <div>
          <p className="hr-kicker">USER PROFILE</p>
          <h1>Account & Security</h1>
        </div>
      </div>

      {home === '/account' ? (
        <p className="hr-notice">
          Your account is signed in. Available web workspaces appear in the navigation according to
          your current permissions. Use the employee mobile application for personal attendance and
          leave. Account security and session controls remain available here.
        </p>
      ) : (
        <Link className="hr-link" to={home}>
          Open {roleName} workspace
        </Link>
      )}

      <div
        style={{
          background: '#ffffff',
          border: '1px solid #e2e8f0',
          borderRadius: '8px',
          padding: '16px 18px',
          boxShadow: '0 1px 3px rgba(0, 0, 0, 0.02)',
          display: 'flex',
          flexDirection: 'column',
          gap: '14px',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '14px' }}>
          <div
            style={{
              width: '44px',
              height: '44px',
              borderRadius: '50%',
              aspectRatio: '1 / 1',
              background: 'linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%)',
              color: '#ffffff',
              fontSize: '16px',
              fontWeight: 700,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              flexShrink: 0,
              letterSpacing: '0.5px',
            }}
          >
            {initials}
          </div>
          <div>
            <h2 style={{ margin: 0, fontSize: '15px', fontWeight: 700, color: '#0f172a' }}>
              {displayName}
            </h2>
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '8px',
                marginTop: '4px',
                flexWrap: 'wrap',
              }}
            >
              <span
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '4px',
                  padding: '2px 7px',
                  borderRadius: '4px',
                  fontSize: '11px',
                  fontWeight: 600,
                  background: '#eff6ff',
                  color: '#2563eb',
                  border: '1px solid #dbeafe',
                }}
              >
                <ShieldCheck size={12} />
                {roleName}
              </span>
              {customRole && (
                <span
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    padding: '2px 7px',
                    borderRadius: '4px',
                    fontSize: '11px',
                    fontWeight: 600,
                    background: '#f1f5f9',
                    color: '#475569',
                    border: '1px solid #e2e8f0',
                  }}
                >
                  {customRole}
                </span>
              )}
              <span style={{ fontSize: '11px', color: '#64748b' }}>
                Ref ID: {user?.account?.accountId?.slice(0, 12) ?? 'system'}
              </span>
            </div>
          </div>
        </div>

        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
            gap: '10px',
            borderTop: '1px solid #f1f5f9',
            paddingTop: '12px',
          }}
        >
          <div
            style={{
              padding: '8px 10px',
              background: '#f8fafc',
              borderRadius: '6px',
              border: '1px solid #e2e8f0',
            }}
          >
            <span style={{ fontSize: '10.5px', color: '#64748b', display: 'block' }}>
              Web Access
            </span>
            <strong
              style={{
                fontSize: '12px',
                color: '#0f172a',
                display: 'inline-flex',
                alignItems: 'center',
                gap: '4px',
                marginTop: '2px',
              }}
            >
              <CheckCircle2 size={13} style={{ color: '#16a34a' }} /> Enabled
            </strong>
          </div>
          <div
            style={{
              padding: '8px 10px',
              background: '#f8fafc',
              borderRadius: '6px',
              border: '1px solid #e2e8f0',
            }}
          >
            <span style={{ fontSize: '10.5px', color: '#64748b', display: 'block' }}>
              Verification & Credentials
            </span>
            <strong
              style={{ fontSize: '12px', color: '#0f172a', marginTop: '2px', display: 'block' }}
            >
              Password & verified second factor
            </strong>
          </div>
        </div>

        {error && (
          <p role="alert" className="hr-message hr-message--error" style={{ margin: 0 }}>
            {error}
          </p>
        )}

        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            flexWrap: 'wrap',
            borderTop: '1px solid #f1f5f9',
            paddingTop: '12px',
          }}
        >
          <Link
            className="button button--secondary"
            to="/account/sessions"
            style={{ fontSize: '12px', padding: '0 12px', height: '32px' }}
          >
            <Laptop size={13} />
            <span>Sessions and devices</span>
          </Link>
          <Button
            variant="secondary"
            disabled={busy}
            onClick={() => void retrySession()}
            style={{ fontSize: '12px', padding: '0 12px', height: '32px' }}
          >
            <RefreshCw size={13} />
            <span>Refresh access</span>
          </Button>
          <Button
            variant="ghost"
            disabled={busy}
            onClick={() => void logout()}
            style={{ fontSize: '12px', padding: '0 12px', height: '32px', color: '#ef4444' }}
          >
            <LogOut size={13} />
            <span>{busy ? 'Signing out...' : 'Sign out'}</span>
          </Button>
        </div>
      </div>
      <AuthenticatorSettings />
    </div>
  );
}
