import { Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { AccountSessions } from '@/auth/AccountSessions';

export function AccountSessionsPage() {
  return (
    <>
      <Link className="hr-back" to="/account">
        <ArrowLeft size={16} />
        Account settings
      </Link>
      <div className="hr-page-heading">
        <h1>Sessions and devices</h1>
      </div>
      <AccountSessions />
    </>
  );
}
