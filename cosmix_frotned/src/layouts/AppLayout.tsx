import { useRef, useState } from 'react';
import { Link, Outlet, useNavigate, useLocation } from 'react-router-dom';
import {
  LayoutDashboard,
  Users,
  Network,
  MapPin,
  Clock,
  CalendarDays,
  Calendar,
  Banknote,
  Coins,
  Car,
  Receipt,
  FileText,
  BarChart2,
  Bell,
  Search,
  Menu,
  X,
  ChevronRight,
  LogOut,
  CalendarCheck,
  UserPlus,
  BarChart3,
  Building2,
} from 'lucide-react';
import { useAuth } from '@/auth/use-auth';
import logo from '@/assets/images/cosmix-logo.png';
import '@/styles/hr-dashboard.css';

export function AppLayout() {
  const { user, signOut } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [open, setOpen] = useState(false);
  const [collapsed, setCollapsed] = useState(() => {
    return localStorage.getItem('cosmix_sidebar_collapsed') === 'true';
  });

  const toggleCollapsed = () => {
    setCollapsed((prev) => {
      const next = !prev;
      localStorage.setItem('cosmix_sidebar_collapsed', String(next));
      return next;
    });
  };

  const menuButton = useRef<HTMLButtonElement>(null);
  const [busy, setBusy] = useState(false);
  const [error] = useState('');
  const [searchTerm, setSearchTerm] = useState('');

  const displayName = user?.account?.displayName || 'Engr. Tariq Mehmood';
  const roleName = user?.grants.some((g) => g.permission.startsWith('access.'))
    ? 'System Administrator'
    : user?.grants.some((g) => g.permission.startsWith('hr.'))
      ? 'HR Desk'
      : 'User Desk';

  // Get Initials for avatar
  const initials =
    displayName
      .split(' ')
      .filter(Boolean)
      .map((part) => part[0])
      .join('')
      .slice(0, 2)
      .toUpperCase() || 'ET';

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (searchTerm.trim()) {
      void navigate(`/hr/employees?search=${encodeURIComponent(searchTerm.trim())}`);
    }
  };

  // Dedicated single-active checker to prevent multiple simultaneous active links
  const isNavActive = (to: string, label: string): boolean => {
    const pathname = location.pathname;
    const isLeaveWorkspace = [
      '/hr/leaves',
      '/hr/leave-management',
      '/hr/leave-encashment',
      '/hr/attendance/leaves',
      '/hr/attendance/leave-settings',
      '/hr/attendance/leave-benefits',
      '/hr/attendance/recovery-work',
    ].some((path) => pathname === path || pathname.startsWith(`${path}/`));

    if (label === 'Dashboard' || to === '/hr/dashboard') {
      return pathname === '/hr/dashboard' || pathname === '/hr' || pathname === '/hr/';
    }

    // Shift Management:
    if (label === 'Shift Management' || to === '/hr/attendance/shifts') {
      return pathname.startsWith('/hr/attendance/shifts') || pathname.startsWith('/hr/shifts');
    }

    // Attendance: Must NEVER match shifts!
    if (label === 'Attendance' || label === 'Attendance & Punches' || to === '/hr/attendance') {
      if (
        isLeaveWorkspace ||
        pathname.startsWith('/hr/attendance/shifts') ||
        pathname.startsWith('/hr/shifts')
      ) {
        return false;
      }
      return pathname === '/hr/attendance' || pathname.startsWith('/hr/attendance/');
    }

    // Working Sites & Geofences:
    if (label === 'Working Sites & Geofences' || to === '/hr/sites') {
      return pathname.startsWith('/hr/sites');
    }

    // Employee Master:
    if (label === 'Employee Master' || to === '/hr/employees') {
      return pathname === '/hr/employees' || pathname.startsWith('/hr/employees/');
    }

    // Organization Structure:
    if (label === 'Organization Structure' || to === '/hr/organization') {
      return pathname === '/hr/organization' || pathname.startsWith('/hr/organization/');
    }

    // Leave & Absence:
    if (label === 'Leave & Absence' || to === '/hr/leaves') {
      return isLeaveWorkspace;
    }

    // Overtime & Allowances:
    if (label === 'Overtime & Allowances' || to === '/hr/allowances') {
      return pathname === '/hr/allowances' || pathname.startsWith('/hr/allowances/');
    }

    // Claims & Advances:
    if (label === 'Claims & Advances' || to === '/hr/advances-loans') {
      return (
        pathname === '/hr/advances-loans' ||
        pathname.startsWith('/hr/advances-loans/') ||
        pathname === '/hr/requests' ||
        pathname.startsWith('/hr/requests/')
      );
    }

    // Vehicles & Fuel:
    if (label === 'Vehicles & Fuel' || to === '/hr/vehicles') {
      return pathname === '/hr/vehicles' || pathname.startsWith('/hr/vehicles/');
    }

    // Payroll Handoff:
    if (label === 'Payroll Handoff' || to === '/hr/payroll-handoff') {
      return pathname === '/hr/payroll-handoff' || pathname.startsWith('/hr/payroll-handoff/');
    }

    if (label === 'Payroll Accounting' || to === '/hr/payroll-accounting') {
      return pathname === '/hr/payroll-accounting' || pathname.startsWith('/hr/payroll-accounting/');
    }

    if (label === 'Statutory Schemes' || to === '/hr/statutory-contributions') {
      return pathname === '/hr/statutory-contributions' || pathname.startsWith('/hr/statutory-contributions/');
    }

    // Reports & Audit:
    if (label === 'Reports & Audit' || to === '/hr/reports/document-expiry') {
      return pathname === '/hr/reports/document-expiry' || pathname === '/hr/reports';
    }

    // HR Reports:
    if (label === 'HR Reports' || to === '/hr/reports/hr') {
      return pathname.startsWith('/hr/reports/hr') || pathname === '/hr/reports/analytics';
    }

    if (to === '/account') {
      return pathname === '/account' || pathname === '/account/sessions';
    }

    return pathname === to;
  };

  const navLinks = [
    {
      to: '/hr/dashboard',
      label: 'Dashboard',
      icon: LayoutDashboard,
    },
    {
      to: '/hr/employees',
      label: 'Employee Master',
      icon: Users,
    },
    {
      to: '/hr/organization',
      label: 'Organization Structure',
      icon: Network,
    },
    {
      to: '/hr/sites',
      label: 'Working Sites & Geofences',
      icon: MapPin,
    },
    {
      to: '/hr/attendance',
      label: 'Attendance',
      icon: Clock,
      badge: 'Live',
      badgeType: 'live',
    },
    {
      to: '/hr/attendance/shifts',
      label: 'Shift Management',
      icon: CalendarDays,
    },
    {
      to: '/hr/leaves',
      label: 'Leave & Absence',
      icon: Calendar,
    },
    {
      to: '/hr/allowances',
      label: 'Overtime & Allowances',
      icon: Banknote,
    },
    {
      to: '/hr/advances-loans',
      label: 'Claims & Advances',
      icon: Coins,
    },
    {
      to: '/hr/vehicles',
      label: 'Vehicles & Fuel',
      icon: Car,
    },
    {
      to: '/hr/payroll-handoff',
      label: 'Payroll Handoff',
      icon: Receipt,
    },
    {
      to: '/hr/payroll-accounting',
      label: 'Payroll Accounting',
      icon: Banknote,
    },
    {
      to: '/hr/statutory-contributions',
      label: 'Statutory Schemes',
      icon: Coins,
    },
    {
      to: '/hr/reports/document-expiry',
      label: 'Reports & Audit',
      icon: FileText,
    },
    {
      to: '/hr/reports/hr',
      label: 'HR Reports',
      icon: BarChart2,
    },
  ];

  return (
    <div className={`hr-shell ${collapsed ? 'is-collapsed' : ''}`}>
      <a className="skip-link" href="#hr-main">
        Skip to content
      </a>
      {open && (
        <button
          className="hr-sidebar-scrim"
          aria-label="Close navigation"
          onClick={() => setOpen(false)}
        />
      )}
      <aside
        id="hr-navigation"
        className={`hr-sidebar hr-sidebar-light ${open ? 'is-open' : ''}`}
        aria-label="Workspace navigation"
        onKeyDown={(event) => {
          if (event.key === 'Escape' && open) {
            setOpen(false);
            menuButton.current?.focus();
          }
        }}
      >
        {/* Brand Header */}
        <div className="hr-brand-wrapper">
          <Link
            to="/hr/dashboard"
            className="hr-brand"
            onClick={() => setOpen(false)}
          >
            <img src={logo} alt="Cosmix Engineering" className="hr-brand-logo-img" />
          </Link>
        </div>
        <button
          className="icon-button hr-close-nav"
          onClick={() => setOpen(false)}
          aria-label="Close navigation"
        >
          <X size={18} />
        </button>

        {/* Section Header */}
        {!collapsed && (
          <div className="hr-nav-section-header">
            <span>HR OPERATIONS</span>
          </div>
        )}

        {/* Menu Items */}
        <nav className="hr-sidebar-nav-list">
          {navLinks.map((item) => {
            const active = isNavActive(item.to, item.label);
            return (
              <Link
                key={item.label + item.to}
                to={item.to}
                className={`hr-nav-link ${active ? 'active' : ''}`}
                onClick={() => setOpen(false)}
                title={collapsed ? item.label : undefined}
              >
                <item.icon size={14} className="hr-nav-icon" />
                <span className="hr-nav-item-text">{item.label}</span>
                {item.badge && (
                  <span
                    className={`hr-sidebar-badge ${item.badgeType ? `badge-${item.badgeType}` : ''}`}
                  >
                    {item.badge}
                  </span>
                )}
              </Link>
            );
          })}
        </nav>

        {/* Footer with User Console matching Screenshot */}
        {!collapsed ? (
          <div className="hr-sidebar-footer-compact">
            <Link to="/hr/dashboard" className="hr-sidebar-user-card" style={{ textDecoration: 'none' }}>
              <div className="hr-sidebar-avatar-circle">{initials}</div>
              <div className="hr-sidebar-user-text">
                <span className="hr-sidebar-user-heading">HR Console</span>
                <span className="hr-sidebar-user-sub">
                  <span className="hr-online-dot">●</span> Online
                </span>
              </div>
              <ChevronRight size={14} className="hr-sidebar-user-arrow" />
            </Link>
          </div>
        ) : (
          <div style={{ marginTop: 'auto', paddingTop: '6px', textAlign: 'center' }}>
            <button
              type="button"
              className="hr-sidebar-logout-link"
              style={{ justifyContent: 'center', padding: '6px 0' }}
              title="Sign Out"
              disabled={busy}
              onClick={async () => {
                setBusy(true);
                try {
                  await signOut();
                } finally {
                  setBusy(false);
                }
              }}
            >
              <LogOut size={16} />
            </button>
          </div>
        )}
      </aside>

      <div className="hr-workspace">
        <header className="hr-topbar hr-topbar-header">
          {/* Left: Hamburger + Breadcrumbs */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <button
              type="button"
              className="hr-nav-toggle-icon"
              ref={menuButton}
              aria-label="Toggle navigation"
              onClick={() => {
                if (window.innerWidth <= 768) {
                  setOpen((prev) => !prev);
                } else {
                  toggleCollapsed();
                }
              }}
              title={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
            >
              <Menu size={18} />
            </button>

            <div className="hr-breadcrumb-strip">
              <Link to="/hr/dashboard" className="hr-breadcrumb-item">
                Dashboard
              </Link>
              <span className="hr-breadcrumb-sep">/</span>
              <span className="hr-breadcrumb-active">Human Resources</span>
            </div>
          </div>

          {/* Center: Global Search Bar */}
          <form onSubmit={handleSearchSubmit} className="hr-search-bar">
            <Search size={15} style={{ color: '#94a3b8', flexShrink: 0 }} />
            <input
              type="text"
              placeholder="Search employees, sites, records..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              aria-label="Search HR System"
            />
          </form>

          {/* Quick Shortcuts (Right side of Search Bar) */}
          <div className="hr-topbar-quick-actions" aria-label="Quick Navigation Shortcuts">
            <Link
              to="/hr/attendance"
              className="hr-topbar-icon-btn"
              title="Quick Attendance & Punches"
              aria-label="Quick Attendance & Punches"
            >
              <CalendarCheck size={16} />
            </Link>
            <Link
              to="/hr/employees"
              className="hr-topbar-icon-btn"
              title="Employee Directory & Records"
              aria-label="Employee Directory & Records"
            >
              <UserPlus size={16} />
            </Link>
            <Link
              to="/hr/reports/hr"
              className="hr-topbar-icon-btn"
              title="HR Reports & Registers"
              aria-label="HR Reports & Registers"
            >
              <BarChart3 size={16} />
            </Link>
            <Link
              to="/hr/organization"
              className="hr-topbar-icon-btn"
              title="Organization & Sites"
              aria-label="Organization & Sites"
            >
              <Building2 size={16} />
            </Link>
          </div>

          {/* Right: Notification Bell + User Profile */}
          <div className="hr-topbar-actions" style={{ gap: '14px' }}>
            <Link
              to="/account/notifications"
              className="hr-notification-bell"
              aria-label="Open notifications"
              title="Notifications"
            >
              <Bell size={17} />
            </Link>

            {/* Current User Chip matching Screenshot */}
            <Link to="/account" className="hr-user-chip" style={{ textDecoration: 'none' }}>
              <div className="hr-user-avatar">{initials}</div>
              <div className="hr-user-info">
                <span className="hr-user-name">{displayName}</span>
                <span className="hr-user-role">{roleName}</span>
              </div>
            </Link>
          </div>
        </header>

        {error && (
          <p
            role="alert"
            className="hr-message hr-message--error"
            style={{ margin: '12px 20px 0' }}
          >
            {error}
          </p>
        )}

        <main id="hr-main" className="hr-main" key={user?.account?.accountId ?? 'restricted'}>
          <Outlet />
        </main>
      </div>
    </div>
  );
}
