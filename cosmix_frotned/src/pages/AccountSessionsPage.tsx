import { Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { AccountSessions } from '@/auth/AccountSessions';

export function AccountSessionsPage() {
  return (
    <div style={{ maxWidth: '850px', display: 'flex', flexDirection: 'column', gap: '14px' }}>
      <div>
        <Link
          className="hr-back"
          to="/account"
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: '6px',
            fontSize: '12px',
            fontWeight: 500,
            color: '#64748b',
            textDecoration: 'none',
            marginBottom: '6px',
          }}
        >
          <ArrowLeft size={14} />
          <span>Back to Account settings</span>
        </Link>
        <div className="hr-page-heading" style={{ marginBottom: 0 }}>
          <div>
            <p className="hr-kicker" style={{ margin: 0 }}>
              SECURITY & ACCESS
            </p>
            <h1 style={{ margin: '2px 0 0' }}>Sessions and devices</h1>
          </div>
        </div>
      </div>
      <AccountSessions />
    </div>
  );
}
