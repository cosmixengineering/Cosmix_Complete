import { Link } from 'react-router-dom';
import {
  ArrowLeft,
  Plus,
  Download,
  Filter,
  TrendingUp,
  BarChart3,
  Package,
  ShoppingCart,
  Settings2,
  ListTodo,
  FileText,
  UserCheck,
  Network,
  Settings,
  HelpCircle,
  User,
} from 'lucide-react';
import { Button } from '@/components/ui/Button';

interface ModuleConfig {
  title: string;
  subtitle: string;
  icon: typeof BarChart3;
  color: string;
  bg: string;
  stats: { label: string; value: string; sub: string }[];
  columns: string[];
  records: { id: string; col1: string; col2: string; col3: string; status: string }[];
}

const MODULES_CONFIG: Record<string, ModuleConfig> = {
  finance: {
    title: 'Accounts & Finance',
    subtitle: 'Manage financial ledgers, revenue tracking, tax invoices, and balance sheets.',
    icon: BarChart3,
    color: '#16a34a',
    bg: '#f0fdf4',
    stats: [
      { label: 'Total Revenue (YTD)', value: 'PKR 142.8M', sub: '↑ 14% vs last year' },
      { label: 'Pending Receivables', value: 'PKR 18.4M', sub: '12 active invoices' },
      { label: 'Payables Due', value: 'PKR 6.2M', sub: 'Due in next 15 days' },
      { label: 'Net Cashflow', value: '+PKR 9.1M', sub: 'Healthy liquidity' },
    ],
    columns: ['Invoice / Voucher', 'Client / Vendor', 'Amount', 'Date', 'Status'],
    records: [
      {
        id: 'INV-2026-081',
        col1: 'Descon Engineering',
        col2: 'PKR 4,850,000',
        col3: '14 Oct 2026',
        status: 'Paid',
      },
      {
        id: 'INV-2026-082',
        col1: 'Frontier Works Org',
        col2: 'PKR 12,200,000',
        col3: '12 Oct 2026',
        status: 'Pending',
      },
      {
        id: 'VCH-7741',
        col1: 'Shell Pakistan Fuel',
        col2: 'PKR 340,000',
        col3: '11 Oct 2026',
        status: 'Approved',
      },
      {
        id: 'INV-2026-083',
        col1: 'National Highway Authority',
        col2: 'PKR 8,150,000',
        col3: '08 Oct 2026',
        status: 'Paid',
      },
    ],
  },
  inventory: {
    title: 'Inventory & Warehousing',
    subtitle:
      'Track physical raw materials, equipment spares, stock levels, and warehouse transfers.',
    icon: Package,
    color: '#9333ea',
    bg: '#faf5ff',
    stats: [
      { label: 'Total SKUs Tracked', value: '1,420 Items', sub: 'Across 3 warehouses' },
      { label: 'Low Stock Alerts', value: '8 Items', sub: 'Reorder triggered' },
      { label: 'Stock Asset Value', value: 'PKR 86.4M', sub: 'Current valuation' },
      { label: 'Dispatches Today', value: '24 Shipments', sub: '100% on schedule' },
    ],
    columns: ['Item Code & Description', 'Category', 'Quantity on Hand', 'Warehouse', 'Status'],
    records: [
      {
        id: 'STK-1001',
        col1: 'Grade 60 Steel Deformed Bar',
        col2: '45 Tons',
        col3: 'Central Yard',
        status: 'Low Stock',
      },
      {
        id: 'STK-1002',
        col1: 'Heavy Duty HDPE Conduit Pipes',
        col2: '1,800 Meters',
        col3: 'Site A Store',
        status: 'In Stock',
      },
      {
        id: 'STK-1003',
        col1: 'Industrial Hydraulic Oil 68',
        col2: '24 Barrels',
        col3: 'Workshop B',
        status: 'In Stock',
      },
      {
        id: 'STK-1004',
        col1: 'Armored 4-Core Copper Cable',
        col2: '850 Meters',
        col3: 'Central Yard',
        status: 'In Stock',
      },
    ],
  },
  procurement: {
    title: 'Procurement & Vendor Sourcing',
    subtitle: 'Manage supplier quotations, RFP bidding, purchase orders, and goods receipts.',
    icon: ShoppingCart,
    color: '#d97706',
    bg: '#fffbeb',
    stats: [
      { label: 'Active Purchase Orders', value: '28 Orders', sub: 'PKR 34.2M value' },
      { label: 'Pending Approvals', value: '3 POs', sub: 'Awaiting executive signoff' },
      { label: 'Registered Vendors', value: '146 Vendors', sub: '98% compliant' },
      { label: 'On-Time Delivery', value: '94.2%', sub: 'Vendor SLA rating' },
    ],
    columns: ['PO Reference', 'Vendor Name', 'Total Amount', 'Expected Delivery', 'Status'],
    records: [
      {
        id: 'PO-7782',
        col1: 'Dell Technologies Pakistan',
        col2: 'PKR 850,000',
        col3: '18 Oct 2026',
        status: 'Pending Approval',
      },
      {
        id: 'PO-7780',
        col1: 'Mughal Steel Mills Ltd',
        col2: 'PKR 6,400,000',
        col3: '20 Oct 2026',
        status: 'In Transit',
      },
      {
        id: 'PO-7778',
        col1: 'Engro Polymers & Chemicals',
        col2: 'PKR 1,250,000',
        col3: '14 Oct 2026',
        status: 'Delivered',
      },
      {
        id: 'PO-7775',
        col1: '3M Safety & Industrial',
        col2: 'PKR 450,000',
        col3: '10 Oct 2026',
        status: 'Delivered',
      },
    ],
  },
  sales: {
    title: 'Sales & CRM',
    subtitle: 'Track prospective leads, client proposals, project bids, and customer contracts.',
    icon: TrendingUp,
    color: '#3b82f6',
    bg: '#eff6ff',
    stats: [
      { label: 'Active Pipeline', value: 'PKR 284M', sub: '18 qualified bids' },
      { label: 'Won Deals (Q3)', value: 'PKR 92.5M', sub: '↑ 22% vs Q2' },
      { label: 'Win Rate', value: '38.4%', sub: 'Above industry avg' },
      { label: 'Key Accounts', value: '34 Clients', sub: 'Institutional & Govt' },
    ],
    columns: ['Proposal / Project', 'Client Entity', 'Contract Value', 'Stage', 'Status'],
    records: [
      {
        id: 'PROP-401',
        col1: 'Solar Farm Grid Integration Phase 1',
        col2: 'PKR 42,000,000',
        col3: 'Executive Signoff',
        status: 'In Review',
      },
      {
        id: 'PROP-398',
        col1: 'Lahore Smart Highway Substation',
        col2: 'PKR 68,000,000',
        col3: 'Technical BOQ',
        status: 'Proposal Sent',
      },
      {
        id: 'PROP-395',
        col1: 'Textile Mill Waste Heat Recovery',
        col2: 'PKR 18,500,000',
        col3: 'Contract Finalization',
        status: 'Won',
      },
    ],
  },
  operations: {
    title: 'Operations & Workflows',
    subtitle:
      'Daily operational coordination, machinery deployment, field maintenance, and dispatch.',
    icon: Settings2,
    color: '#0d9488',
    bg: '#f0fdfa',
    stats: [
      { label: 'Active Machinery', value: '64 Units', sub: '92% operational uptime' },
      { label: 'Deployments Today', value: '18 Sites', sub: 'All teams checked in' },
      { label: 'Maintenance Jobs', value: '4 In Progress', sub: 'Scheduled workshop work' },
      { label: 'Safety Compliance', value: '100%', sub: 'Zero accidents this month' },
    ],
    columns: ['Work Order', 'Assigned Site / Unit', 'Supervisor', 'Execution Window', 'Status'],
    records: [
      {
        id: 'WO-8812',
        col1: 'Main Plant Power Calibration',
        col2: 'Engr. Haris Khan',
        col3: '14-16 Oct 2026',
        status: 'Active',
      },
      {
        id: 'WO-8809',
        col1: 'Cranes Hydraulic Inspection',
        col2: 'Asim Qureshi',
        col3: '13-14 Oct 2026',
        status: 'Completed',
      },
      {
        id: 'WO-8805',
        col1: 'Site B Boundary Survey & Fencing',
        col2: 'Sajid Mehmood',
        col3: '10-12 Oct 2026',
        status: 'Completed',
      },
    ],
  },
  projects: {
    title: 'Projects & Tasks',
    subtitle:
      'Engineering project tracking, Gantt schedules, milestone deliverables, and client billing.',
    icon: ListTodo,
    color: '#6366f1',
    bg: '#f5f3ff',
    stats: [
      { label: 'Active Projects', value: '14 Projects', sub: 'Civil, Electrical & MEP' },
      { label: 'Milestones Due', value: '6 This Month', sub: 'PKR 48M billing linked' },
      { label: 'On Schedule Rate', value: '91.8%', sub: 'Project management index' },
      { label: 'Total Man-Hours', value: '18,400 Hrs', sub: 'Logged this month' },
    ],
    columns: [
      'Project Code & Name',
      'Lead Engineer',
      'Budget Allocation',
      'Target Completion',
      'Status',
    ],
    records: [
      {
        id: 'PRJ-2026-A',
        col1: 'Industrial Substation Expansion',
        col2: 'Engr. Haris Khan',
        col3: 'PKR 54,000,000',
        status: 'On Track',
      },
      {
        id: 'PRJ-2026-B',
        col1: 'Central Warehouse HVAC & MEP',
        col2: 'Dr. Munir Ahmed',
        col3: 'PKR 28,500,000',
        status: 'On Track',
      },
      {
        id: 'PRJ-2026-C',
        col1: 'High-Voltage Transmission Feeders',
        col2: 'Adnan Siddiqui',
        col3: 'PKR 76,000,000',
        status: 'Milestone Review',
      },
    ],
  },
  reports: {
    title: 'Analytics & Executive Reports',
    subtitle:
      'Comprehensive cross-departmental business intelligence, audits, and compliance exports.',
    icon: FileText,
    color: '#e11d48',
    bg: '#fef2f2',
    stats: [
      { label: 'Generated Reports', value: '48 Reports', sub: 'Available for download' },
      { label: 'Audit Logs', value: '12,480 Records', sub: '100% trace verified' },
      { label: 'Scheduled Exports', value: '8 Automated', sub: 'Weekly & Monthly cycles' },
      { label: 'Data Accuracy', value: '99.9%', sub: 'Validated by ledger' },
    ],
    columns: ['Report Name', 'Department', 'Frequency', 'Last Run', 'Format'],
    records: [
      {
        id: 'REP-01',
        col1: 'Monthly Financial Balance & P&L',
        col2: 'Finance & Accounts',
        col3: 'Monthly (01st)',
        status: 'PDF / Excel',
      },
      {
        id: 'REP-02',
        col1: 'Workforce Attendance & Overtime Summary',
        col2: 'Human Resources',
        col3: 'Weekly (Mondays)',
        status: 'PDF / CSV',
      },
      {
        id: 'REP-03',
        col1: 'Inventory Stock Valuation & Movement',
        col2: 'Supply Chain',
        col3: 'Bi-Weekly',
        status: 'Excel',
      },
    ],
  },
  users: {
    title: 'Users & Roles Management',
    subtitle:
      'System administrator access control, RBAC security matrices, and user account provisioning.',
    icon: UserCheck,
    color: '#1d4ed8',
    bg: '#eff6ff',
    stats: [
      { label: 'Total User Accounts', value: '84 Users', sub: 'Across 11 departments' },
      { label: 'Active System Roles', value: '8 Roles', sub: 'Admin, HR, Finance, Site...' },
      { label: '2FA Enabled Rate', value: '100%', sub: 'Enterprise policy enforced' },
      { label: 'Active Sessions Today', value: '38 Sessions', sub: 'Real-time telemetry' },
    ],
    columns: ['User Name', 'Email Address', 'Assigned Role', 'Department', 'Status'],
    records: [
      {
        id: 'USR-01',
        col1: 'System Administrator',
        col2: 'admin@cosmix.com.pk',
        col3: 'Super Administrator',
        status: 'Active',
      },
      {
        id: 'USR-02',
        col1: 'Ayesha Khan',
        col2: 'hr.ayesha@cosmix.com.pk',
        col3: 'HR Officer',
        status: 'Active',
      },
      {
        id: 'USR-03',
        col1: 'Engr. Haris Khan',
        col2: 'site.haris@cosmix.com.pk',
        col3: 'Site Supervisor',
        status: 'Active',
      },
      {
        id: 'USR-04',
        col1: 'Noman Siddiqui',
        col2: 'accounts@cosmix.com.pk',
        col3: 'Finance Manager',
        status: 'Active',
      },
    ],
  },
  departments: {
    title: 'Departments & Organizational Units',
    subtitle: 'Manage hierarchical branch networks, division trees, and department heads.',
    icon: Network,
    color: '#15803d',
    bg: '#f0fdf4',
    stats: [
      { label: 'Active Departments', value: '11 Divisions', sub: 'HQ & Site branches' },
      { label: 'Total Employees', value: '248 Staff', sub: 'Full-time & Contract' },
      { label: 'Operating Locations', value: '4 Sites', sub: 'Head Office, Site A, B, C' },
      { label: 'Department Heads', value: '11 Appointed', sub: '100% leadership assigned' },
    ],
    columns: [
      'Department Code & Name',
      'Department Head',
      'Staff Count',
      'Operating Base',
      'Status',
    ],
    records: [
      {
        id: 'DEP-01',
        col1: 'Human Resources (HR)',
        col2: 'Ayesha Khan',
        col3: '8 Members',
        status: 'Head Office',
      },
      {
        id: 'DEP-02',
        col1: 'Civil & Structural Engineering',
        col2: 'Engr. Haris Khan',
        col3: '54 Members',
        status: 'Site A & HQ',
      },
      {
        id: 'DEP-03',
        col1: 'Accounts & Corporate Finance',
        col2: 'Noman Siddiqui',
        col3: '12 Members',
        status: 'Head Office',
      },
      {
        id: 'DEP-04',
        col1: 'Supply Chain & Procurement',
        col2: 'Adnan Siddiqui',
        col3: '18 Members',
        status: 'Central Yard',
      },
    ],
  },
  settings: {
    title: 'System Settings & Configuration',
    subtitle:
      'Configure enterprise ERP parameters, audit schedules, API integrations, and branding.',
    icon: Settings,
    color: '#475569',
    bg: '#f8fafc',
    stats: [
      { label: 'System Version', value: 'NextERP v2.4', sub: 'Build 2026.10' },
      { label: 'Database Health', value: 'Healthy (0 ms)', sub: 'Cosmix_Testing online' },
      { label: 'Backup Status', value: 'Completed Daily', sub: 'Last backup: Today 04:00 AM' },
      { label: 'Security Protocols', value: 'TLS 1.3 / JWT', sub: 'AES-256 encrypted' },
    ],
    columns: [
      'Parameter / Setting',
      'Configuration Category',
      'Current Value',
      'Last Modified',
      'Status',
    ],
    records: [
      {
        id: 'CFG-01',
        col1: 'Company Legal Identity',
        col2: 'General Organization',
        col3: 'Cosmix Engineering (Pvt) Ltd',
        status: 'Active',
      },
      {
        id: 'CFG-02',
        col1: 'Attendance GPS Radius Validation',
        col2: 'Field Tracking Policy',
        col3: '200 Meters Allowed',
        status: 'Enforced',
      },
      {
        id: 'CFG-03',
        col1: 'Auto-Draft Employee Onboarding',
        col2: 'HR Automation',
        col3: 'Enabled with Direct Route',
        status: 'Active',
      },
    ],
  },
  panel: {
    title: 'My Admin Personal Panel',
    subtitle:
      'Quick shortcuts, personal pending tasks, personalized audit summaries, and productivity tools.',
    icon: User,
    color: '#2563eb',
    bg: '#eff6ff',
    stats: [
      { label: 'Assigned Action Items', value: '5 Tasks', sub: 'Due today' },
      { label: 'Approvals Awaiting You', value: '17 Requests', sub: 'Review pending items' },
      { label: 'System Alerts', value: '0 Critical', sub: 'All operations normal' },
      { label: 'Security Score', value: '98 / 100', sub: 'Excellent compliance' },
    ],
    columns: ['Task / Action', 'Associated Module', 'Due Date', 'Priority', 'Status'],
    records: [
      {
        id: 'TSK-01',
        col1: 'Review & Sign Off Solar Farm Proposal',
        col2: 'Projects & Contracts',
        col3: 'Today, 05:00 PM',
        status: 'Pending',
      },
      {
        id: 'TSK-02',
        col1: 'Approve Dell Laptops PO #PO-7782',
        col2: 'Procurement',
        col3: 'Today, 02:00 PM',
        status: 'Pending',
      },
      {
        id: 'TSK-03',
        col1: 'Review Monthly Attendance Roster Close',
        col2: 'Human Resources',
        col3: 'Tomorrow',
        status: 'Scheduled',
      },
    ],
  },
  help: {
    title: 'Help, Documentation & Support',
    subtitle:
      'Cosmix ERP user manuals, standard operating procedures (SOPs), and helpdesk tickets.',
    icon: HelpCircle,
    color: '#0284c7',
    bg: '#f0f9ff',
    stats: [
      { label: 'Help Articles', value: '42 Guides', sub: 'Step-by-step SOPs' },
      { label: 'Helpdesk Tickets', value: '0 Open', sub: 'All queries resolved' },
      { label: 'Support Hotline', value: '24/7 Live', sub: 'support@cosmix.com.pk' },
      { label: 'Knowledge Base', value: 'v2.4 Updated', sub: 'Includes HR & Admin' },
    ],
    columns: ['Guide Title', 'Module Section', 'Estimated Read', 'Version', 'Status'],
    records: [
      {
        id: 'DOC-01',
        col1: 'Administrator Cross-Module Approvals Guide',
        col2: 'Admin Center',
        col3: '4 min read',
        status: 'Available',
      },
      {
        id: 'DOC-02',
        col1: 'HR Employee Creation & Live Geofencing SOP',
        col2: 'Human Resources',
        col3: '6 min read',
        status: 'Available',
      },
      {
        id: 'DOC-03',
        col1: 'Managing Site Boundaries & Supervisor Assignment',
        col2: 'Attendance & Sites',
        col3: '5 min read',
        status: 'Available',
      },
    ],
  },
};

const DEFAULT_CONFIG: ModuleConfig = MODULES_CONFIG['finance']!;

export function AdminModulePage({ moduleKey }: { moduleKey: string }) {
  const config = MODULES_CONFIG[moduleKey] ?? DEFAULT_CONFIG;
  const Icon = config.icon;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '22px' }}>
      {/* Header */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '14px',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '14px' }}>
          <Link
            to="/admin/dashboard"
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              width: '38px',
              height: '38px',
              borderRadius: '8px',
              background: '#ffffff',
              border: '1px solid #e2e8f0',
              color: '#475569',
              textDecoration: 'none',
            }}
          >
            <ArrowLeft size={18} />
          </Link>
          <div
            style={{
              width: '42px',
              height: '42px',
              borderRadius: '10px',
              background: config.bg,
              color: config.color,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <Icon size={22} />
          </div>
          <div>
            <h1 style={{ fontSize: '24px', fontWeight: 750, color: '#0f172a', margin: 0 }}>
              {config.title}
            </h1>
            <p style={{ fontSize: '13px', color: '#64748b', margin: '2px 0 0' }}>
              {config.subtitle}
            </p>
          </div>
        </div>

        <div style={{ display: 'flex', gap: '10px' }}>
          <Link
            to="/admin/approvals"
            style={{
              background: '#ffffff',
              border: '1px solid #e2e8f0',
              borderRadius: '8px',
              padding: '8px 14px',
              fontSize: '13px',
              fontWeight: 600,
              color: '#334155',
              textDecoration: 'none',
              display: 'inline-flex',
              alignItems: 'center',
              gap: '6px',
            }}
          >
            <span>View Pending Approvals (17)</span>
          </Link>
          <Button
            style={{
              background: '#2563eb',
              color: '#ffffff',
              borderRadius: '8px',
              padding: '8px 16px',
              fontSize: '13px',
              fontWeight: 600,
            }}
          >
            <Plus size={16} />
            <span>New Action</span>
          </Button>
        </div>
      </div>

      {/* Stats Cards Grid */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '14px' }}>
        {config.stats.map((st) => (
          <div
            key={st.label}
            style={{
              background: '#ffffff',
              border: '1px solid #e2e8f0',
              borderRadius: '12px',
              padding: '16px 18px',
              display: 'flex',
              flexDirection: 'column',
              gap: '4px',
              boxShadow: '0 1px 3px rgba(0,0,0,0.02)',
            }}
          >
            <span style={{ fontSize: '12.5px', color: '#64748b', fontWeight: 500 }}>
              {st.label}
            </span>
            <span style={{ fontSize: '22px', fontWeight: 750, color: '#0f172a' }}>{st.value}</span>
            <span style={{ fontSize: '11.5px', color: '#16a34a', fontWeight: 500 }}>{st.sub}</span>
          </div>
        ))}
      </div>

      {/* Main Table Card */}
      <div
        style={{
          background: '#ffffff',
          border: '1px solid #e2e8f0',
          borderRadius: '12px',
          padding: '20px',
          boxShadow: '0 1px 3px rgba(0,0,0,0.02)',
        }}
      >
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            marginBottom: '16px',
          }}
        >
          <h2 style={{ fontSize: '16px', fontWeight: 700, color: '#0f172a', margin: 0 }}>
            Recent Records & Activity
          </h2>
          <div style={{ display: 'flex', gap: '8px' }}>
            <button
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                background: '#f8fafc',
                border: '1px solid #e2e8f0',
                padding: '6px 12px',
                borderRadius: '8px',
                fontSize: '12px',
                color: '#475569',
                cursor: 'pointer',
              }}
            >
              <Download size={14} />
              <span>Export</span>
            </button>
            <button
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                background: '#f8fafc',
                border: '1px solid #e2e8f0',
                padding: '6px 12px',
                borderRadius: '8px',
                fontSize: '12px',
                color: '#475569',
                cursor: 'pointer',
              }}
            >
              <Filter size={14} />
              <span>Filter</span>
            </button>
          </div>
        </div>

        <div style={{ overflowX: 'auto' }}>
          <table
            style={{
              width: '100%',
              borderCollapse: 'collapse',
              textAlign: 'left',
              fontSize: '13px',
            }}
          >
            <thead>
              <tr style={{ borderBottom: '1px solid #e2e8f0', background: '#f8fafc' }}>
                <th style={{ padding: '10px 14px', color: '#475569', fontWeight: 600 }}>ID</th>
                {config.columns.map((col) => (
                  <th key={col} style={{ padding: '10px 14px', color: '#475569', fontWeight: 600 }}>
                    {col}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {config.records.map((r) => (
                <tr key={r.id} style={{ borderBottom: '1px solid #f1f5f9' }}>
                  <td style={{ padding: '12px 14px', fontWeight: 600, color: '#2563eb' }}>
                    {r.id}
                  </td>
                  <td style={{ padding: '12px 14px', color: '#0f172a', fontWeight: 500 }}>
                    {r.col1}
                  </td>
                  <td style={{ padding: '12px 14px', color: '#475569' }}>{r.col2}</td>
                  <td style={{ padding: '12px 14px', color: '#64748b' }}>{r.col3}</td>
                  <td style={{ padding: '12px 14px' }}>
                    <span
                      style={{
                        background:
                          r.status === 'Paid' ||
                          r.status === 'In Stock' ||
                          r.status === 'Delivered' ||
                          r.status === 'On Track' ||
                          r.status === 'Active'
                            ? '#dcfce7'
                            : r.status === 'Low Stock' ||
                                r.status === 'Pending Approval' ||
                                r.status === 'Pending'
                              ? '#fef3c7'
                              : '#eff6ff',
                        color:
                          r.status === 'Paid' ||
                          r.status === 'In Stock' ||
                          r.status === 'Delivered' ||
                          r.status === 'On Track' ||
                          r.status === 'Active'
                            ? '#15803d'
                            : r.status === 'Low Stock' ||
                                r.status === 'Pending Approval' ||
                                r.status === 'Pending'
                              ? '#b45309'
                              : '#1d4ed8',
                        padding: '3px 8px',
                        borderRadius: '6px',
                        fontSize: '11.5px',
                        fontWeight: 600,
                      }}
                    >
                      {r.status}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
