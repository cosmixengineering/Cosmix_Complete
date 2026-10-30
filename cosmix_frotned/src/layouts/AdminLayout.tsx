import { useRef, useState } from 'react';
import { Link, NavLink, Outlet, useNavigate, Navigate } from 'react-router-dom';
import {
  LayoutDashboard,
  User,
  Users,
  Fingerprint,
  BarChart3,
  Package,
  ShoppingCart,
  TrendingUp,
  Settings2,
  ListTodo,
  ClipboardCheck,
  FileText,
  UserCheck,
  ShieldCheck,
  Network,
  Settings,
  HelpCircle,
  Search,
  Menu,
  X,
  LogOut,
  PanelLeftClose,
  PanelLeftOpen,
  Clock,
  Calendar,
  CalendarDays,
  CalendarCheck,
} from 'lucide-react';
import { useAuth } from '@/auth/use-auth';
import { isSystemAdmin } from '@/auth/contracts';
import { canOpenWorkspace, workspaceHome } from '@/auth/workspace-access';
import { hasAttendancePermission } from '@/modules/attendance';
import { hrConfigurationNavigation } from '@/modules/hr/policy-navigation';
import { Button } from '@/components/ui/Button';
import { NotificationBellToggle } from '@/components/notifications/NotificationBellToggle';
import logo from '@/assets/images/cosmix-logo.png';
import '@/styles/admin-dashboard.css';

export function AdminLayout() {
  const { user, signOut, status } = useAuth();
  const navigate = useNavigate();
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
  const [error, setError] = useState('');
  const [searchTerm, setSearchTerm] = useState('');

  const isAdmin = isSystemAdmin(user);
  const canTrackAttendance =
    status === 'authenticated' &&
    hasAttendancePermission(user, 'hr.employees.read') &&
    hasAttendancePermission(user, 'attendance.summaries.read');

  // Keep non-admin deep links within the user's assigned workspace.
  if (status === 'authenticated' && user && !isAdmin) {
    return <Navigate to={workspaceHome(user)} replace />;
  }

  const displayName = user?.account?.displayName || 'Admin User';
  const roleName = 'System Administrator';

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (searchTerm.trim()) {
      void navigate(`/admin/approvals?search=${encodeURIComponent(searchTerm.trim())}`);
    }
  };

  const navItems = [
    { to: '/admin/dashboard', label: 'Dashboard', icon: LayoutDashboard },
    { to: '/admin/panel', label: 'My Panel', icon: User },
    { to: '/admin/employees', label: 'Employees', icon: Users },
    { to: '/hr/attendance', label: 'Attendance', icon: Clock },
    ...(canOpenWorkspace(user, '/hr/payroll-reconciliation')
      ? [{ to: '/hr/payroll-reconciliation', label: 'Payroll Comparison', icon: ClipboardCheck }]
      : []),
    ...(user?.grants.some(
      (g) => g.permission === 'attendance.face.manage' || g.permission === 'attendance.face.read',
    )
      ? [{ to: '/hr/biometrics', label: 'Biometric Registration', icon: Fingerprint }]
      : []),
    ...(canTrackAttendance
      ? [
          {
            to: '/hr/attendance/tracking',
            label: 'Attendance Tracking',
            icon: CalendarCheck,
          },
        ]
      : []),
    { to: '/hr/leaves', label: 'Leave Management', icon: Calendar },
    { to: '/hr/attendance/shifts', label: 'Shifts & Rosters', icon: CalendarDays },
    { to: '/admin/finance', label: 'Accounts & Finance', icon: BarChart3 },
    { to: '/admin/inventory', label: 'Inventory', icon: Package },
    { to: '/admin/procurement', label: 'Procurement', icon: ShoppingCart },
    { to: '/admin/sales', label: 'Sales & CRM', icon: TrendingUp },
    { to: '/admin/operations', label: 'Operations', icon: Settings2 },
    { to: '/admin/projects', label: 'Projects', icon: ListTodo },
    { to: '/admin/approvals', label: 'Approvals', icon: ClipboardCheck },
    { to: '/admin/reports', label: 'Reports', icon: FileText },
    { to: '/admin/roles', label: 'Roles & Permissions', icon: ShieldCheck },
    { to: '/admin/users', label: 'Users & Accounts', icon: UserCheck },
    { to: '/admin/departments', label: 'Departments', icon: Network },
    { to: '/admin/settings', label: 'Settings', icon: Settings },
    ...hrConfigurationNavigation(user, status)
      .filter((link) => link.allowed)
      .map((link) => ({ ...link, icon: Settings })),
  ];

  return (
    <div className={`admin-shell ${collapsed ? 'is-collapsed' : ''}`}>
      {open && (
        <button
          className="admin-sidebar-scrim"
          aria-label="Close navigation"
          onClick={() => setOpen(false)}
        />
      )}

      {/* Admin Sidebar */}
      <aside className={`admin-sidebar ${open ? 'is-open' : ''}`} aria-label="Admin Navigation">
        <div className="admin-sidebar-header">
          <Link to="/admin/dashboard" className="admin-brand" onClick={() => setOpen(false)}>
            <img src={logo} alt="Cosmix Engineering" className="admin-brand-logo" />
            {!collapsed && (
              <div className="admin-brand-text">
                <h2>Cosmix ERP</h2>
                <p>System Administrator</p>
              </div>
            )}
          </Link>
          <button
            type="button"
            className="admin-sidebar-toggle-btn"
            onClick={toggleCollapsed}
            title={collapsed ? 'Expand side menu' : 'Collapse side menu'}
            aria-label="Toggle side menu"
          >
            {collapsed ? <PanelLeftOpen size={14} /> : <PanelLeftClose size={14} />}
          </button>
        </div>

        <button
          className="icon-button admin-close-nav"
          onClick={() => setOpen(false)}
          aria-label="Close navigation"
        >
          <X size={18} />
        </button>

        <nav>
          {navItems.map((item) => (
            <NavLink
              key={item.label + item.to}
              to={item.to}
              className={({ isActive }) => `admin-nav-item ${isActive ? 'active' : ''}`}
              end={item.to === '/admin/dashboard' || item.to === '/hr/attendance'}
              onClick={() => setOpen(false)}
              title={collapsed ? item.label : undefined}
            >
              <item.icon size={15} />
              {!collapsed && <span>{item.label}</span>}
            </NavLink>
          ))}

          <div className="admin-nav-divider" />

          <NavLink
            to="/admin/help"
            className={({ isActive }) => `admin-nav-item ${isActive ? 'active' : ''}`}
            onClick={() => setOpen(false)}
            title={collapsed ? 'Help & Support' : undefined}
          >
            <HelpCircle size={15} />
            {!collapsed && <span>Help & Support</span>}
          </NavLink>
        </nav>

        {/* Bottom Company Card */}
        {!collapsed && (
          <div className="admin-sidebar-bottom-card">
            <img src={logo} alt="Cosmix" className="admin-bottom-logo" />
            <div className="admin-bottom-text">
              <strong>Cosmix Engineering</strong>
              <span>Administrative Console</span>
            </div>
          </div>
        )}
      </aside>

      {/* Main Workspace */}
      <div className="admin-workspace">
        <header className="admin-topbar">
          <div className="admin-topbar-left">
            <button
              className="icon-button admin-open-nav"
              ref={menuButton}
              aria-label="Open navigation"
              onClick={() => setOpen(true)}
            >
              <Menu size={18} />
            </button>

            {/* Desktop Open / Close Toggle Button */}
            <button
              type="button"
              className="admin-desktop-toggle-btn"
              onClick={toggleCollapsed}
              title={collapsed ? 'Open side menu' : 'Close side menu'}
              aria-label="Toggle side menu"
            >
              {collapsed ? <PanelLeftOpen size={15} /> : <PanelLeftClose size={15} />}
            </button>

            {/* Global Search Bar */}
            <form onSubmit={handleSearchSubmit} className="admin-search-bar">
              <Search size={14} style={{ color: '#94a3b8', flexShrink: 0 }} />
              <input
                type="text"
                placeholder="Search modules, employees, reports..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                aria-label="Search ERP"
              />
            </form>
          </div>

          <div className="admin-topbar-actions">
            {/* Notification Bell Dropdown Toggle */}
            <NotificationBellToggle />

            {/* User Chip */}
            <Link to="/admin/account" className="admin-user-chip">
              <div className="admin-user-avatar">{displayName.charAt(0).toUpperCase() || 'A'}</div>
              <div className="admin-user-info">
                <span className="admin-user-name">{displayName}</span>
                <span className="admin-user-role">{roleName}</span>
              </div>
            </Link>

            {/* Sign out */}
            <Button
              variant="ghost"
              disabled={busy}
              onClick={async () => {
                setBusy(true);
                setError('');
                try {
                  await signOut();
                } catch {
                  setError('Sign out could not be confirmed.');
                } finally {
                  setBusy(false);
                }
              }}
            >
              <LogOut size={15} />
              <span>Sign out</span>
            </Button>
          </div>
        </header>

        {error && (
          <p
            role="alert"
            className="hr-message hr-message--error"
            style={{ margin: '16px 36px 0' }}
          >
            {error}
          </p>
        )}

        <main className="admin-main">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
