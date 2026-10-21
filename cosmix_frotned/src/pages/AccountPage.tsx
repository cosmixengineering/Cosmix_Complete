import { useState } from 'react';
import { Link } from 'react-router-dom';
import { LogOut, RefreshCw, ShieldCheck, Laptop, CheckCircle2 } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { useAuth } from '@/auth/use-auth';

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

  const displayName = user?.account?.displayName || 'User Profile';
  const roleName = user?.grants.some((g: { permission: string }) =>
    g.permission.startsWith('admin.'),
  )
    ? 'System Administrator'
    : 'HR Officer / Employee';

  return (
    <div style={{ maxWidth: '800px', display: 'flex', flexDirection: 'column', gap: '14px' }}>
      <div className="hr-page-heading">
        <div>
          <p className="hr-kicker">USER PROFILE</p>
          <h1>Account & Security</h1>
        </div>
      </div>

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
              background: 'linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%)',
              color: '#ffffff',
              fontSize: '18px',
              fontWeight: 700,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              flexShrink: 0,
            }}
          >
            {displayName.charAt(0).toUpperCase()}
          </div>
          <div>
            <h2 style={{ margin: 0, fontSize: '15px', fontWeight: 700, color: '#0f172a' }}>
              {displayName}
            </h2>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginTop: '3px' }}>
              <span
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '4px',
                  padding: '2px 7px',
                  borderRadius: '4px',
                  fontSize: '10.5px',
                  fontWeight: 600,
                  background: '#eff6ff',
                  color: '#2563eb',
                  border: '1px solid #dbeafe',
                }}
              >
                <ShieldCheck size={12} />
                {roleName}
              </span>
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
              Active Password & Session SMS
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
            style={{ fontSize: '11.5px', padding: '5px 12px' }}
          >
            <Laptop size={14} />
            <span>Sessions and devices</span>
          </Link>
          <Button
            variant="secondary"
            disabled={busy}
            onClick={() => void retrySession()}
            style={{ fontSize: '11.5px', padding: '5px 12px' }}
          >
            <RefreshCw size={14} />
            <span>Refresh access</span>
          </Button>
          <Button
            variant="ghost"
            disabled={busy}
            onClick={() => void logout()}
            style={{ fontSize: '11.5px', padding: '5px 12px', color: '#ef4444' }}
          >
            <LogOut size={14} />
            <span>{busy ? 'Signing out...' : 'Sign out'}</span>
          </Button>
        </div>
      </div>
    </div>
  );
}
