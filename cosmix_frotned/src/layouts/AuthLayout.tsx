import type { ReactNode } from 'react';
import { LockKeyhole } from 'lucide-react';
import logo from '@/assets/images/cosmix-logo.png';
import engineeringBanner from '@/assets/images/cosmix-engineering-banner.png';

export function AuthLayout({
  children,
  variant = 'centered',
}: {
  children: ReactNode;
  variant?: 'centered' | 'split';
}) {
  if (variant === 'split') {
    return (
      <div className="auth-layout auth-layout--split">
        <aside className="auth-banner" aria-label="Cosmix Engineering">
          <div className="auth-banner-heading">
            <span className="auth-banner-heading-line" aria-hidden="true" />
            <span>Engineering excellence</span>
          </div>

          <div className="auth-banner-photo">
            <img src={engineeringBanner} alt="Cosmix Engineering Facility" fetchPriority="high" />
          </div>
        </aside>

        <div className="auth-panel">
          <header className="auth-panel-header">
            <span className="auth-security">
              <LockKeyhole size={15} aria-hidden="true" /> Employee access
            </span>
          </header>
          <main className="auth-main" id="main-content">
            {children}
          </main>
          <footer className="auth-footer">
            <span>Cosmix Engineering</span>
            <span>Authorized personnel only</span>
          </footer>
        </div>
      </div>
    );
  }
  return (
    <div className="auth-layout">
      <header className="auth-header">
        <img src={logo} alt="Cosmix Engineering" width="192" height="80" />
        <span className="auth-security">
          <LockKeyhole size={15} aria-hidden="true" /> Employee access
        </span>
      </header>
      <main className="auth-main" id="main-content">
        {children}
      </main>
      <footer className="auth-footer">
        <span>Cosmix Engineering</span>
        <span>Authorized personnel only</span>
      </footer>
    </div>
  );
}
