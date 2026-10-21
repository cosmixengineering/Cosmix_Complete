import { Link } from 'react-router-dom';
import {
  Users,
  Building2,
  ClipboardCheck,
  Bell,
  Calendar,
  ArrowRight,
  TrendingUp,
  User,
  BarChart3,
  Package,
  ShoppingCart,
  Settings2,
  ListTodo,
  FileText,
  UserCheck,
  Network,
  Settings,
  Clock,
  Megaphone,
  CreditCard,
  Briefcase,
} from 'lucide-react';
import { useAuth } from '@/auth/use-auth';
import '@/styles/admin-dashboard.css';

export function AdminDashboardPage() {
  const { user } = useAuth();
  const displayName = user?.account?.displayName || 'Admin';

  const erpModules = [
    {
      to: '/admin/panel',
      title: 'My Panel',
      desc: 'Your personal dashboard and quick actions',
      icon: User,
      bg: '#eff6ff',
      color: '#2563eb',
    },
    {
      to: '/admin/employees',
      title: 'Employees',
      desc: 'Manage employees, profiles & directory',
      icon: Users,
      bg: '#fdf2f8',
      color: '#db2777',
    },
    {
      to: '/hr/attendance',
      title: 'Attendance & Biometrics',
      desc: 'Live punches, face verification & geofencing',
      icon: Clock,
      bg: '#eff6ff',
      color: '#2563eb',
    },
    {
      to: '/hr/leaves',
      title: 'Leave Management',
      desc: 'Annual, casual, sick leaves & balances',
      icon: Calendar,
      bg: '#fef3c7',
      color: '#d97706',
    },
    {
      to: '/admin/finance',
      title: 'Accounts & Finance',
      desc: 'Financial management, ledgers & reports',
      icon: BarChart3,
      bg: '#f0fdf4',
      color: '#16a34a',
    },
    {
      to: '/admin/inventory',
      title: 'Inventory',
      desc: 'Stock, assets & warehouse management',
      icon: Package,
      bg: '#faf5ff',
      color: '#9333ea',
    },
    {
      to: '/admin/procurement',
      title: 'Procurement',
      desc: 'Purchase orders, vendors & sourcing',
      icon: ShoppingCart,
      bg: '#fffbeb',
      color: '#d97706',
    },
    {
      to: '/admin/sales',
      title: 'Sales & CRM',
      desc: 'Leads, opportunities & customer management',
      icon: TrendingUp,
      bg: '#eff6ff',
      color: '#3b82f6',
    },
    {
      to: '/admin/operations',
      title: 'Operations',
      desc: 'Day-to-day operations & workflows',
      icon: Settings2,
      bg: '#f0fdfa',
      color: '#0d9488',
    },
    {
      to: '/admin/projects',
      title: 'Projects',
      desc: 'Project planning, tasks & tracking',
      icon: ListTodo,
      bg: '#f5f3ff',
      color: '#6366f1',
    },
    {
      to: '/admin/reports',
      title: 'Reports',
      desc: 'Insights & analytical reports',
      icon: FileText,
      bg: '#fef2f2',
      color: '#e11d48',
    },
    {
      to: '/admin/users',
      title: 'Users & Roles',
      desc: 'Manage users, permissions & access control',
      icon: UserCheck,
      bg: '#eff6ff',
      color: '#1d4ed8',
    },
    {
      to: '/admin/departments',
      title: 'Departments',
      desc: 'Organizational structure & department management',
      icon: Network,
      bg: '#f0fdf4',
      color: '#15803d',
    },
    {
      to: '/admin/settings',
      title: 'Settings',
      desc: 'System configuration & preferences',
      icon: Settings,
      bg: '#f8fafc',
      color: '#475569',
    },
  ];

  const todayFormatted = new Intl.DateTimeFormat('en-GB', {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  }).format(new Date());

  return (
    <div className="admin-dash-container">
      {/* 1. Welcome Section */}
      <div className="admin-welcome-section">
        <div className="admin-welcome-text">
          <h1>Good Morning, {displayName}!</h1>
          <p>Here's what's happening across your organization today.</p>
        </div>
        <div className="admin-date-pill-container">
          <div className="admin-date-pill">
            <Calendar size={15} style={{ color: '#2563eb' }} />
            <span>{todayFormatted}</span>
          </div>
          <span className="admin-date-subtitle">Make it a productive day!</span>
        </div>
      </div>

      {/* 2. Top 4 Stat Cards */}
      <div className="admin-stat-grid">
        {/* Total Employees */}
        <Link to="/admin/employees" style={{ textDecoration: 'none', color: 'inherit' }}>
          <div className="admin-stat-card admin-stat-card--blue">
            <div className="admin-stat-card-icon">
              <Users size={22} />
            </div>
            <div className="admin-stat-card-content">
              <p className="admin-stat-card-title">Total Employees</p>
              <div className="admin-stat-card-value">248</div>
              <div className="admin-stat-card-trend trend--positive">
                <span>↑ 12%</span>
                <span style={{ color: '#64748b' }}>vs last month</span>
              </div>
            </div>
          </div>
        </Link>

        {/* Active Departments */}
        <div className="admin-stat-card admin-stat-card--green">
          <div className="admin-stat-card-icon">
            <Building2 size={22} />
          </div>
          <div className="admin-stat-card-content">
            <p className="admin-stat-card-title">Active Departments</p>
            <div className="admin-stat-card-value">11</div>
            <div className="admin-stat-card-trend trend--neutral">
              <span>No change vs last month</span>
            </div>
          </div>
        </div>

        {/* Pending Approvals */}
        <div className="admin-stat-card admin-stat-card--amber">
          <div className="admin-stat-card-icon">
            <ClipboardCheck size={22} />
          </div>
          <div className="admin-stat-card-content">
            <p className="admin-stat-card-title">Pending Approvals</p>
            <div className="admin-stat-card-value">17</div>
            <div className="admin-stat-card-trend trend--alert">
              <span>↑ 6%</span>
              <span style={{ color: '#64748b' }}>vs last month</span>
            </div>
          </div>
        </div>

        {/* Notifications */}
        <div className="admin-stat-card admin-stat-card--purple">
          <div className="admin-stat-card-icon">
            <Bell size={22} />
          </div>
          <div className="admin-stat-card-content">
            <p className="admin-stat-card-title">Notifications</p>
            <div className="admin-stat-card-value">5</div>
            <div className="admin-stat-card-trend trend--alert">
              <span>↑ 25%</span>
              <span style={{ color: '#64748b' }}>vs last month</span>
            </div>
          </div>
        </div>
      </div>

      {/* 3. ERP Modules Section */}
      <div className="admin-modules-section">
        <div className="admin-section-header">
          <h2 className="admin-section-title">ERP Modules</h2>
          <Link to="/admin/approvals" className="admin-section-link">
            <span>View All Modules</span>
            <ArrowRight size={14} />
          </Link>
        </div>

        <div className="admin-modules-grid">
          {erpModules.map((mod) => (
            <Link key={mod.title} to={mod.to} className="admin-module-card">
              <div
                className="admin-module-card-icon"
                style={{ background: mod.bg, color: mod.color }}
              >
                <mod.icon size={22} />
              </div>
              <div className="admin-module-card-body">
                <h3>{mod.title}</h3>
                <p>{mod.desc}</p>
              </div>
              <ArrowRight size={16} className="admin-module-card-arrow" />
            </Link>
          ))}
        </div>
      </div>

      {/* 4. Bottom 3 Cards: Recent Activity, Pending Approvals, Announcements */}
      <div className="admin-bottom-grid">
        {/* Recent Activity */}
        <div className="admin-card">
          <div className="admin-card-header">
            <div className="admin-card-title-group">
              <Clock size={16} style={{ color: '#64748b' }} />
              <h2 className="admin-card-title">Recent Activity</h2>
            </div>
            <Link to="/admin/reports" className="admin-card-action-link">
              <span>View All</span>
              <ArrowRight size={13} />
            </Link>
          </div>

          <div className="admin-activity-list">
            <div className="admin-activity-row">
              <div className="admin-activity-left">
                <span className="admin-status-dot" style={{ background: '#22c55e' }} />
                <span className="admin-activity-text">New employee John Doe added in HR</span>
              </div>
              <span className="admin-activity-time">10:24 AM</span>
            </div>

            <div className="admin-activity-row">
              <div className="admin-activity-left">
                <span className="admin-status-dot" style={{ background: '#f59e0b' }} />
                <span className="admin-activity-text">Purchase order #PO-7782 submitted</span>
              </div>
              <span className="admin-activity-time">09:15 AM</span>
            </div>

            <div className="admin-activity-row">
              <div className="admin-activity-left">
                <span className="admin-status-dot" style={{ background: '#3b82f6' }} />
                <span className="admin-activity-text">Invoice #INV-4421 approved</span>
              </div>
              <span className="admin-activity-time">Yesterday</span>
            </div>

            <div className="admin-activity-row">
              <div className="admin-activity-left">
                <span className="admin-status-dot" style={{ background: '#3b82f6' }} />
                <span className="admin-activity-text">New project 'Website Revamp' created</span>
              </div>
              <span className="admin-activity-time">Yesterday</span>
            </div>

            <div className="admin-activity-row">
              <div className="admin-activity-left">
                <span className="admin-status-dot" style={{ background: '#ef4444' }} />
                <span className="admin-activity-text">Stock level low for item STK-1001</span>
              </div>
              <span className="admin-activity-time">14 Apr 2025</span>
            </div>
          </div>
        </div>

        {/* Pending Approvals */}
        <div className="admin-card">
          <div className="admin-card-header">
            <div className="admin-card-title-group">
              <Clock size={16} style={{ color: '#64748b' }} />
              <h2 className="admin-card-title">Pending Approvals</h2>
            </div>
            <Link to="/admin/approvals" className="admin-card-action-link">
              <span>View All</span>
              <ArrowRight size={13} />
            </Link>
          </div>

          <div className="admin-approvals-list">
            <Link to="/admin/approvals?type=leave" className="admin-approval-row">
              <div className="admin-approval-left">
                <div
                  className="admin-approval-icon"
                  style={{ background: '#fdf2f8', color: '#db2777' }}
                >
                  <Users size={15} />
                </div>
                <span className="admin-approval-name">5 Leave Requests</span>
              </div>
              <span className="admin-awaiting-badge">Awaiting approval</span>
            </Link>

            <Link to="/admin/approvals?type=po" className="admin-approval-row">
              <div className="admin-approval-left">
                <div
                  className="admin-approval-icon"
                  style={{ background: '#fffbeb', color: '#d97706' }}
                >
                  <ShoppingCart size={15} />
                </div>
                <span className="admin-approval-name">3 Purchase Orders</span>
              </div>
              <span className="admin-awaiting-badge">Awaiting approval</span>
            </Link>

            <Link to="/admin/approvals?type=expense" className="admin-approval-row">
              <div className="admin-approval-left">
                <div
                  className="admin-approval-icon"
                  style={{ background: '#eff6ff', color: '#2563eb' }}
                >
                  <CreditCard size={15} />
                </div>
                <span className="admin-approval-name">4 Expense Claims</span>
              </div>
              <span className="admin-awaiting-badge">Awaiting approval</span>
            </Link>

            <Link to="/admin/approvals?type=project" className="admin-approval-row">
              <div className="admin-approval-left">
                <div
                  className="admin-approval-icon"
                  style={{ background: '#f0fdf4', color: '#16a34a' }}
                >
                  <Briefcase size={15} />
                </div>
                <span className="admin-approval-name">2 Project Proposals</span>
              </div>
              <span className="admin-awaiting-badge">Awaiting approval</span>
            </Link>
          </div>
        </div>

        {/* Announcements */}
        <div className="admin-card">
          <div className="admin-card-header">
            <div className="admin-card-title-group">
              <Megaphone size={16} style={{ color: '#64748b' }} />
              <h2 className="admin-card-title">Announcements</h2>
            </div>
            <Link to="/admin/reports" className="admin-card-action-link">
              <span>View All</span>
              <ArrowRight size={13} />
            </Link>
          </div>

          <div className="admin-announcements-list">
            <div className="admin-announcement-item">
              <div className="admin-announcement-header">
                <div className="admin-announcement-title-group">
                  <span className="admin-status-dot" style={{ background: '#3b82f6' }} />
                  <span className="admin-announcement-title">System Maintenance</span>
                </div>
                <span className="admin-announcement-date">15 Apr 2025</span>
              </div>
              <p className="admin-announcement-desc">
                Scheduled maintenance on 20 Apr 2025, 10:00 PM - 12:00 AM.
              </p>
            </div>

            <div className="admin-announcement-item">
              <div className="admin-announcement-header">
                <div className="admin-announcement-title-group">
                  <span className="admin-status-dot" style={{ background: '#22c55e' }} />
                  <span className="admin-announcement-title">New HR Policy</span>
                </div>
                <span className="admin-announcement-date">12 Apr 2025</span>
              </div>
              <p className="admin-announcement-desc">
                Updated remote work policy is now effective.
              </p>
            </div>

            <div className="admin-announcement-item">
              <div className="admin-announcement-header">
                <div className="admin-announcement-title-group">
                  <span className="admin-status-dot" style={{ background: '#f59e0b' }} />
                  <span className="admin-announcement-title">Finance Year Closing</span>
                </div>
                <span className="admin-announcement-date">10 Apr 2025</span>
              </div>
              <p className="admin-announcement-desc">
                Please complete your pending entries before 30 Apr 2025.
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
