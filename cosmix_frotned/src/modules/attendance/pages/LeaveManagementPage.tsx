import React, { useState, useMemo, useEffect } from 'react';
import { Link } from 'react-router-dom';
import {
  Calendar,
  Plus,
  Search,
  FileSpreadsheet,
  FileText,
  Upload,
  CheckCircle2,
  Clock,
  UserCheck,
  Plane,
  Stethoscope,
  Coffee,
  Hourglass,
  Eye,
  Check,
  X,
  Building2,
  FileCheck,
  Settings,
  Receipt,
  ClipboardCheck,
  Printer,
  ChevronRight,
  Shield,
  AlertTriangle,
  Inbox,
  CalendarCheck2,
  Paperclip,
} from 'lucide-react';
import { useAuth } from '@/auth/use-auth';
import { useHrQuery } from '@/modules/hr/hooks/use-hr-queries';
import { employeeArchivePageSchema } from '@/modules/hr/components/employee/employee-archive-directory';
import { queryString } from '../api/attendance-api';
import '@/styles/leave-management.css';



export interface LeaveAppItem {
  id: string;
  appCode: string;
  leaveType: 'SICK' | 'ANNUAL' | 'CASUAL' | 'SHORT' | 'LWP';
  leaveTypeLabel: string;
  empId: string;
  empName: string;
  initials: string;
  role: string;
  dept: string;
  site: string;
  avatarBg: string;
  avatarColor: string;
  durationLabel: string;
  datesLabel: string;
  rawDays: number;
  isLong: boolean;
  quotaLeft: number;
  quotaTotal: number;
  reliever: string;
  relieverPhone: string;
  relieverNote?: string;
  reason: string;
  docVerified?: boolean;
  docName?: string;
  status: 'PENDING' | 'PENDING_CEO' | 'PENDING_PM' | 'APPROVED' | 'REJECTED';
  approvalStage: string;
  auditTrail: string;
  appliedOn: string;
}



const INITIAL_APPLICATIONS: LeaveAppItem[] = [
  {
    id: 'app-1',
    appCode: 'LV-2026-089',
    leaveType: 'SICK',
    leaveTypeLabel: 'Sick Leave',
    empId: 'EMP-1043',
    empName: 'Sara Ahmed',
    initials: 'SA',
    role: 'Sales Executive',
    dept: 'Sales & CRM',
    site: 'Lahore HQ',
    avatarBg: '#dbeafe',
    avatarColor: '#1d4ed8',
    durationLabel: '2 Days (Full Day)',
    datesLabel: 'Sep 8, 2026 - Sep 9, 2026',
    rawDays: 2,
    isLong: false,
    quotaLeft: 6,
    quotaTotal: 8,
    reliever: 'Zainab Ali (HR Asst)',
    relieverPhone: '0300-1234567',
    reason: 'Viral Fever & Doctor Advised Rest',
    docVerified: true,
    docName: 'Medical_Prescription_Dr_Asim.pdf',
    status: 'APPROVED',
    approvalStage: 'HR Verified & Approved',
    auditTrail: 'Approved by HR Operations Desk • 07 Sep 2026',
    appliedOn: '2026-09-07',
  },
  {
    id: 'app-2',
    appCode: 'LV-2026-090',
    leaveType: 'ANNUAL',
    leaveTypeLabel: 'Annual Leave',
    empId: 'EMP-1045',
    empName: 'Zainab Ali',
    initials: 'ZA',
    role: 'HR Assistant',
    dept: 'HR Operations',
    site: 'Lahore HQ',
    avatarBg: '#f3e8ff',
    avatarColor: '#7e22ce',
    durationLabel: '10 Days (Long Leave)',
    datesLabel: 'Oct 1, 2026 - Oct 10, 2026',
    rawDays: 10,
    isLong: true,
    quotaLeft: 12,
    quotaTotal: 14,
    reliever: 'Sara Ahmed (Desk Coverage)',
    relieverPhone: '0321-9876543',
    relieverNote: 'Handoff file prepared & shared on drive',
    reason: 'Family Wedding & Relocation Assistance',
    status: 'PENDING_CEO',
    approvalStage: 'Awaiting CEO Signature',
    auditTrail: 'Site PM Endorsed -> HR Verified -> Forwarded to CEO Level',
    appliedOn: '2026-09-06',
  },
  {
    id: 'app-3',
    appCode: 'LV-2026-085',
    leaveType: 'CASUAL',
    leaveTypeLabel: 'Casual Leave',
    empId: 'EMP-1042',
    empName: 'Engr. Ali Khan',
    initials: 'AK',
    role: 'HVAC Site Engineer',
    dept: 'MEP & Execution',
    site: 'Karachi High-Rise',
    avatarBg: '#dcfce7',
    avatarColor: '#15803d',
    durationLabel: '1 Day',
    datesLabel: 'Sep 5, 2026',
    rawDays: 1,
    isLong: false,
    quotaLeft: 8,
    quotaTotal: 10,
    reliever: 'Engr. Tariq Farooq (Project Manager)',
    relieverPhone: '0333-5551234',
    relieverNote: 'Site Chiller inspection shift covered',
    reason: 'Urgent Passport Office Documentation',
    status: 'APPROVED',
    approvalStage: 'Approved by Site PM',
    auditTrail: 'Approved by Site Project Manager • 04 Sep 2026',
    appliedOn: '2026-09-04',
  },
  {
    id: 'app-4',
    appCode: 'LV-2026-092',
    leaveType: 'SHORT',
    leaveTypeLabel: 'Short Permission',
    empId: 'EMP-1046',
    empName: 'Bilal Ahmed',
    initials: 'BA',
    role: 'Field Electrician',
    dept: 'MEP & Execution',
    site: 'Karachi Tower',
    avatarBg: '#e0e7ff',
    avatarColor: '#242b5f',
    durationLabel: '3 Hours (Early Leave)',
    datesLabel: 'Today: 02:00 PM - 05:00 PM',
    rawDays: 0.3,
    isLong: false,
    quotaLeft: 2,
    quotaTotal: 3,
    reliever: 'Usman Rafiq (Site Supervisor)',
    relieverPhone: '0345-9988776',
    relieverNote: 'Ducting cable pulling shift covered',
    reason: 'Family Emergency / Hospital Routine Checkup',
    status: 'PENDING_PM',
    approvalStage: 'Pending Site PM Review',
    auditTrail: 'Submitted via Mobile App • Awaiting Site PM Sign-off',
    appliedOn: '2026-09-08',
  },
];



export function LeaveManagementPage() {
  const { user } = useAuth();
  const isAdmin = Boolean(
    user?.roles?.some((r: string) => {
      const lower = r.toLowerCase();
      return (
        lower.includes('admin') ||
        lower.includes('executive') ||
        lower.includes('director') ||
        lower.includes('pm') ||
        lower.includes('manager')
      );
    })
  );
  const [activeTab, setActiveTab] = useState<'ALL' | 'PENDING' | 'APPROVED' | 'LONG'>('ALL');
  const [searchQuery, setSearchQuery] = useState('');
  const [typeFilter, setTypeFilter] = useState('ALL');



  // Real DB employees
  const directoryQuery = useHrQuery(
    'employees' + queryString({ page: 1, pageSize: 100 }),
    employeeArchivePageSchema,
    ['hr.employees.read']
  );
  const dbEmployees = directoryQuery.data?.items || [];



  const [applications, setApplications] = useState<LeaveAppItem[]>(INITIAL_APPLICATIONS);
  const [selectedApp, setSelectedApp] = useState<LeaveAppItem | null>(null);
  const [isApplyModalOpen, setIsApplyModalOpen] = useState(false);
  const [toast, setToast] = useState('');



  // Form states for Apply Modal
  const [newEmpCode, setNewEmpCode] = useState('SA');
  const [newType, setNewType] = useState<'Casual Leave' | 'Sick Leave' | 'Annual Leave' | 'Short Permission' | 'Leave Without Pay'>('Casual Leave');
  const [newFraction, setNewFraction] = useState('1');
  const [newFromDate, setNewFromDate] = useState('2026-09-10');
  const [newToDate, setNewToDate] = useState('2026-09-11');
  const [newReliever, setNewReliever] = useState('Engr. Tariq Farooq (Project Manager)');
  const [newPhone, setNewPhone] = useState('+92 321 4567890');
  const [newReason, setNewReason] = useState('');
  const [hasMedicalFile, setHasMedicalFile] = useState(false);



  // Auto-dismiss toast
  useEffect(() => {
    if (toast) {
      const timer = setTimeout(() => setToast(''), 3500);
      return () => clearTimeout(timer);
    }
  }, [toast]);



  // Working days calculation for modal
  const calculatedDuration = useMemo(() => {
    if (!newFromDate || !newToDate) return { days: 1, text: '1 Working Day', isLong: false };
    const d1 = new Date(newFromDate);
    const d2 = new Date(newToDate);
    const diff = Math.max(0, d2.getTime() - d1.getTime());
    const rawDays = Math.round(diff / (1000 * 60 * 60 * 24)) + 1;
    const frac = parseFloat(newFraction) || 1;
    const total = (rawDays * frac).toFixed(frac === 1 ? 0 : 1);
    const num = parseFloat(total);
    return {
      days: num,
      text: `${total} Working Day${num > 1 ? 's' : ''}`,
      isLong: num > 3,
    };
  }, [newFromDate, newToDate, newFraction]);



  // Tab counts
  const counts = useMemo(() => {
    return {
      all: applications.length,
      pending: applications.filter((a) => a.status === 'PENDING' || a.status === 'PENDING_CEO' || a.status === 'PENDING_PM').length,
      approved: applications.filter((a) => a.status === 'APPROVED').length,
      long: applications.filter((a) => a.isLong).length,
    };
  }, [applications]);



  // Filtered applications
  const filteredApps = useMemo(() => {
    return applications.filter((app) => {
      // Tab filter
      if (activeTab === 'PENDING') {
        if (app.status !== 'PENDING' && app.status !== 'PENDING_CEO' && app.status !== 'PENDING_PM') return false;
      } else if (activeTab === 'APPROVED') {
        if (app.status !== 'APPROVED') return false;
      } else if (activeTab === 'LONG') {
        if (!app.isLong) return false;
      }



      // Type filter
      if (typeFilter !== 'ALL' && app.leaveType !== typeFilter) {
        return false;
      }



      // Search query
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase();
        return (
          app.empName.toLowerCase().includes(q) ||
          app.empId.toLowerCase().includes(q) ||
          app.appCode.toLowerCase().includes(q) ||
          app.leaveTypeLabel.toLowerCase().includes(q) ||
          app.site.toLowerCase().includes(q) ||
          app.status.toLowerCase().includes(q)
        );
      }
      return true;
    });
  }, [applications, activeTab, typeFilter, searchQuery]);



  // Approve action
  const handleApprove = (id: string, name: string) => {
    setApplications((prev) =>
      prev.map((a) => (a.id === id ? { ...a, status: 'APPROVED', approvalStage: 'Approved by HR Desk' } : a)),
    );
    if (selectedApp && selectedApp.id === id) {
      setSelectedApp((prev) => (prev ? { ...prev, status: 'APPROVED', approvalStage: 'Approved by HR Desk' } : null));
    }
    setToast(`Approved leave application for ${name}! Attendance register updated.`);
  };



  // Reject action
  const handleReject = (id: string, name: string) => {
    setApplications((prev) =>
      prev.map((a) => (a.id === id ? { ...a, status: 'REJECTED', approvalStage: 'Rejected by Reviewer' } : a)),
    );
    if (selectedApp && selectedApp.id === id) {
      setSelectedApp((prev) => (prev ? { ...prev, status: 'REJECTED', approvalStage: 'Rejected by Reviewer' } : null));
    }
    setToast(`Leave application for ${name} rejected. Notification sent to employee.`);
  };



  // Submit Apply Modal Form
  const handleSubmitApply = (e: React.FormEvent) => {
    e.preventDefault();
    const empProfiles: Record<string, { name: string; code: string; role: string; dept: string; site: string; bg: string; color: string }> = {
      AK: { name: 'Engr. Ali Khan', code: 'EMP-1042', role: 'HVAC Site Engr', dept: 'MEP & Execution', site: 'Karachi Tower', bg: '#dcfce7', color: '#15803d' },
      SA: { name: 'Sara Ahmed', code: 'EMP-1043', role: 'Sales Executive', dept: 'Sales & CRM', site: 'Lahore HQ', bg: '#dbeafe', color: '#1d4ed8' },
      UT: { name: 'Usman Tariq', code: 'EMP-1044', role: 'MEP Supervisor', dept: 'Field Maintenance', site: 'Islamabad Complex', bg: '#ffedd5', color: '#c2410c' },
      ZA: { name: 'Zainab Ali', code: 'EMP-1045', role: 'HR Assistant', dept: 'HR Operations', site: 'Lahore HQ', bg: '#f3e8ff', color: '#7e22ce' },
      BA: { name: 'Bilal Ahmed', code: 'EMP-1046', role: 'Field Tech', dept: 'MEP & Execution', site: 'Karachi Tower', bg: '#f1f5f9', color: '#475569' },
    };



    // Match DB employee or fallback
    const defaultProfile = {
      name: 'Sara Ahmed',
      code: 'EMP-1043',
      role: 'Sales Executive',
      dept: 'Sales & CRM',
      site: 'Lahore HQ',
      bg: '#dbeafe',
      color: '#1d4ed8',
    };
    const matchedDb = dbEmployees.find((e) => e.id === newEmpCode);
    const p = (matchedDb
      ? {
          name: matchedDb.name || matchedDb.email || 'Employee',
          code: matchedDb.identityNumber || `EMP-${matchedDb.employeeNumber || '1040'}`,
          role: 'Operations Engineer',
          dept: 'Engineering & Field',
          site: 'Head Office (Lahore)',
          bg: '#e0e7ff',
          color: '#242b5f',
        }
      : empProfiles[newEmpCode]) || defaultProfile;
    const isLong = calculatedDuration.isLong;
    const randomNum = Math.floor(100 + Math.random() * 899);
    const appCode = `LV-2026-${randomNum}`;



    let typeKey: 'SICK' | 'ANNUAL' | 'CASUAL' | 'SHORT' | 'LWP' = 'CASUAL';
    if (newType === 'Sick Leave') typeKey = 'SICK';
    else if (newType === 'Annual Leave') typeKey = 'ANNUAL';
    else if (newType === 'Short Permission') typeKey = 'SHORT';
    else if (newType === 'Leave Without Pay') typeKey = 'LWP';



    const newApp: LeaveAppItem = {
      id: `app-${Date.now()}`,
      appCode,
      leaveType: typeKey,
      leaveTypeLabel: newType,
      empId: p.code,
      empName: p.name,
      initials: p.name.split(' ').map((n) => n[0]).slice(0, 2).join(''),
      role: p.role,
      dept: p.dept,
      site: p.site,
      avatarBg: p.bg,
      avatarColor: p.color,
      durationLabel: calculatedDuration.text,
      datesLabel: `${newFromDate} to ${newToDate}`,
      rawDays: calculatedDuration.days,
      isLong,
      quotaLeft: 8,
      quotaTotal: 12,
      reliever: newReliever,
      relieverPhone: newPhone,
      reason: newReason.trim() || 'Personal Leave Application',
      docVerified: hasMedicalFile,
      docName: hasMedicalFile ? 'Medical_Doctor_Cert.pdf' : undefined,
      status: isLong ? 'PENDING_CEO' : 'PENDING',
      approvalStage: isLong ? 'Forwarded to CEO (Long Leave)' : 'Pending HR Verification',
      auditTrail: 'Directly logged via HR Portal • Pending review',
      appliedOn: new Date().toISOString().slice(0, 10),
    };



    setApplications([newApp, ...applications]);
    setIsApplyModalOpen(false);
    setNewReason('');
    setToast(`Application ${appCode} submitted successfully for ${p.name}!`);
  };



  return (
    <div className="leave-v4-shell">
      {/* Toast Notice */}
      {toast && (
        <div className="leave-toast-float">
          <CheckCircle2 size={16} color="#4ade80" />
          <span>{toast}</span>
        </div>
      )}



      {/* 1. Header & Navigation Links */}
      <div className="leave-v4-header">
        <div>
          <div className="leave-v4-title-row">
            <h1 className="leave-v4-title">Leave Management &amp; Approvals</h1>
            <span className="leave-v4-breadcrumb">
              <Building2 size={12} color="#64748b" />
              Human Resources
            </span>
          </div>
          <p className="leave-v4-sub">
            Review employee leave applications, monitor site coverage, and manage multi-tier approvals (Site PM &gt; HR &gt; CEO).
          </p>
        </div>



        <div className="leave-v4-nav-links">
          {isAdmin && (
            <Link to="/admin/approvals?type=leave" className="leave-v4-btn-nav" title="Admin Approvals Queue">
              <Shield size={12} color="#2563eb" />
              Admin Approvals
            </Link>
          )}
          <Link to="/hr/attendance/leave-settings" className="leave-v4-btn-nav" title="Configure policies">
            <Settings size={12} color="#64748b" />
            Leave Settings
          </Link>
          <Link to="/hr/leave-encashment" className="leave-v4-btn-nav" title="Encashment review">
            <Receipt size={12} color="#64748b" />
            Encashment Review
          </Link>
          <Link to="/hr/attendance/recovery-work" className="leave-v4-btn-nav" title="Recovery work review">
            <ClipboardCheck size={12} color="#64748b" />
            Recovery Work
          </Link>
        </div>
      </div>



      {/* 2. Top 4 KPI Summary Cards - Calm Enterprise Styling */}
      <div className="leave-v4-kpis-grid">
        {/* KPI 1 */}
        <div className="leave-v4-kpi-card" onClick={() => setActiveTab('PENDING')} style={{ cursor: 'pointer' }}>
          <div className="leave-v4-kpi-left">
            <span className="leave-v4-kpi-label">Pending Leave Queue</span>
            <div className="leave-v4-kpi-val">
              {counts.pending} <span className="leave-v4-kpi-tag">Need Review</span>
            </div>
            <span className="leave-v4-kpi-sub">
              2 Site PM • 2 CEO Level
            </span>
          </div>
          <div className="leave-v4-kpi-icon">
            <Inbox size={15} />
          </div>
        </div>



        {/* KPI 2 */}
        <div className="leave-v4-kpi-card" onClick={() => setActiveTab('APPROVED')} style={{ cursor: 'pointer' }}>
          <div className="leave-v4-kpi-left">
            <span className="leave-v4-kpi-label">Approved This Month</span>
            <div className="leave-v4-kpi-val">
              28 <span className="leave-v4-kpi-tag">Leaves</span>
            </div>
            <span className="leave-v4-kpi-sub">
              <CheckCircle2 size={11} /> 96% SLA clearance speed
            </span>
          </div>
          <div className="leave-v4-kpi-icon">
            <CalendarCheck2 size={15} />
          </div>
        </div>



        {/* KPI 3 */}
        <div className="leave-v4-kpi-card" onClick={() => setActiveTab('LONG')} style={{ cursor: 'pointer' }}>
          <div className="leave-v4-kpi-left">
            <span className="leave-v4-kpi-label">Awaiting CEO Sign-off</span>
            <div className="leave-v4-kpi-val">
              2 <span className="leave-v4-kpi-tag">Long Leaves</span>
            </div>
            <span className="leave-v4-kpi-sub">
              &gt; 3 Days requires CEO approval
            </span>
          </div>
          <div className="leave-v4-kpi-icon">
            <Clock size={15} />
          </div>
        </div>



        {/* KPI 4 */}
        <div className="leave-v4-kpi-card">
          <div className="leave-v4-kpi-left">
            <span className="leave-v4-kpi-label">On Leave Today</span>
            <div className="leave-v4-kpi-val">
              3 <span className="leave-v4-kpi-tag">Workers Away</span>
            </div>
            <span className="leave-v4-kpi-sub">
              1 HVAC Engr • 2 Techs
            </span>
          </div>
          <div className="leave-v4-kpi-icon">
            <UserCheck size={15} />
          </div>
        </div>
      </div>



      {/* 3. Action Toolbar: Filters + Search + Excel/PDF + Apply Leave */}
      <div className="leave-v4-toolbar">
        <div className="leave-v4-toolbar-left">
          <div className="leave-v4-search-box">
            <Search size={13} className="leave-v4-search-icon" />
            <input
              type="text"
              className="leave-v4-search-input"
              placeholder="Search by employee, leave type, site, status..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
            />
          </div>



          <select
            className="leave-v4-select"
            value={typeFilter}
            onChange={(e) => setTypeFilter(e.target.value)}
          >
            <option value="ALL">All Categories</option>
            <option value="SICK">Sick Leave</option>
            <option value="CASUAL">Casual Leave</option>
            <option value="ANNUAL">Annual Leave</option>
            <option value="SHORT">Short Leave / Half-Day</option>
            <option value="LWP">Leave Without Pay (LWP)</option>
          </select>
        </div>



        <div className="leave-v4-toolbar-right">
          <button
            type="button"
            className="leave-v4-btn-action"
            onClick={() => setToast('Leave register exported to Excel successfully!')}
            title="Export Excel"
          >
            <FileSpreadsheet size={13} color="#475569" />
            Excel
          </button>
          <button
            type="button"
            className="leave-v4-btn-action"
            onClick={() => window.print()}
            title="Export PDF / Print"
          >
            <FileText size={13} color="#475569" />
            PDF
          </button>
          <button
            type="button"
            className="leave-v4-btn-action"
            onClick={() => setToast('Leave balances quota import template ready.')}
            title="Import balances"
          >
            <Upload size={13} color="#475569" />
            Import
          </button>
          <button
            type="button"
            className="leave-v4-btn-primary"
            onClick={() => setIsApplyModalOpen(true)}
          >
            <Plus size={13} />
            Apply Leave
          </button>
        </div>
      </div>



      {/* 4. Category Filter Tabs */}
      <div className="leave-v4-tabs-bar">
        <button
          type="button"
          className={`leave-v4-tab ${activeTab === 'ALL' ? 'active' : ''}`}
          onClick={() => setActiveTab('ALL')}
        >
          All Applications
          <span className="leave-v4-tab-pill">
            {counts.all}
          </span>
        </button>



        <button
          type="button"
          className={`leave-v4-tab ${activeTab === 'PENDING' ? 'active' : ''}`}
          onClick={() => setActiveTab('PENDING')}
        >
          <Clock size={12} color="#64748b" />
          Pending Review
          <span className="leave-v4-tab-pill">
            {counts.pending}
          </span>
        </button>



        <button
          type="button"
          className={`leave-v4-tab ${activeTab === 'APPROVED' ? 'active' : ''}`}
          onClick={() => setActiveTab('APPROVED')}
        >
          <CheckCircle2 size={12} color="#64748b" />
          Approved
          <span className="leave-v4-tab-pill">
            {counts.approved}
          </span>
        </button>



        <button
          type="button"
          className={`leave-v4-tab ${activeTab === 'LONG' ? 'active' : ''}`}
          onClick={() => setActiveTab('LONG')}
        >
          <Plane size={12} color="#64748b" />
          Long Leaves (&gt; 3 Days)
          <span className="leave-v4-tab-pill">
            {counts.long}
          </span>
        </button>
      </div>



      {/* 5. Active Leave Requests Table */}
      <div className="leave-v4-card">
        <div className="leave-v4-card-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <span style={{ width: '6px', height: '6px', borderRadius: '50%', background: '#64748b', display: 'inline-block' }}></span>
            <h3 style={{ fontSize: '11.5px', fontWeight: 700, color: '#0f172a', textTransform: 'uppercase', letterSpacing: '0.04em', margin: 0 }}>
              Active Leave Applications &amp; Absence Register
            </h3>
          </div>
          <span style={{ fontSize: '10.5px', color: '#94a3b8', fontWeight: 500 }}>
            Automatic Attendance Register integration
          </span>
        </div>



        <div className="leave-v4-table-wrap">
          <table className="leave-v4-table">
            <thead>
              <tr>
                <th>App ID &amp; Type</th>
                <th>Employee Information</th>
                <th>Leave Dates &amp; Duration</th>
                <th>Annual Quota Balance</th>
                <th>Site Reliever / Handover</th>
                <th>Reason &amp; Documents</th>
                <th style={{ textAlign: 'center' }}>Status</th>
                <th style={{ textAlign: 'right' }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {filteredApps.length === 0 ? (
                <tr>
                  <td colSpan={8} style={{ textAlign: 'center', padding: '36px', color: '#94a3b8' }}>
                    No leave applications matching the selected criteria.
                  </td>
                </tr>
              ) : (
                filteredApps.map((app) => (
                  <tr key={app.id}>
                    {/* App ID & Type */}
                    <td>
                      <div style={{ fontWeight: 700, color: '#0f172a', fontSize: '11.5px' }}>{app.appCode}</div>
                      <div style={{ marginTop: '2px' }}>
                        {app.leaveType === 'SICK' && (
                          <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px', background: '#f8fafc', color: '#334155', border: '1px solid #cbd5e1', padding: '1px 6px', borderRadius: '4px', fontSize: '9.5px', fontWeight: 600 }}>
                            <Stethoscope size={10} color="#64748b" /> Sick Leave
                          </span>
                        )}
                        {app.leaveType === 'ANNUAL' && (
                          <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px', background: '#f8fafc', color: '#334155', border: '1px solid #cbd5e1', padding: '1px 6px', borderRadius: '4px', fontSize: '9.5px', fontWeight: 600 }}>
                            <Plane size={10} color="#64748b" /> Annual Leave
                          </span>
                        )}
                        {app.leaveType === 'CASUAL' && (
                          <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px', background: '#f8fafc', color: '#334155', border: '1px solid #cbd5e1', padding: '1px 6px', borderRadius: '4px', fontSize: '9.5px', fontWeight: 600 }}>
                            <Coffee size={10} color="#64748b" /> Casual Leave
                          </span>
                        )}
                        {app.leaveType === 'SHORT' && (
                          <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px', background: '#f8fafc', color: '#334155', border: '1px solid #cbd5e1', padding: '1px 6px', borderRadius: '4px', fontSize: '9.5px', fontWeight: 600 }}>
                            <Hourglass size={10} color="#64748b" /> Short Permission
                          </span>
                        )}
                        {app.leaveType === 'LWP' && (
                          <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px', background: '#f8fafc', color: '#334155', border: '1px solid #cbd5e1', padding: '1px 6px', borderRadius: '4px', fontSize: '9.5px', fontWeight: 600 }}>
                            Unpaid (LWP)
                          </span>
                        )}
                      </div>
                    </td>



                    {/* Employee Info */}
                    <td>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '7px' }}>
                        <div
                          style={{
                            width: '24px',
                            height: '24px',
                            borderRadius: '50%',
                            background: '#0b1a3d',
                            color: '#ffffff',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            fontWeight: 700,
                            fontSize: '9.5px',
                            flexShrink: 0,
                          }}
                        >
                          {app.initials}
                        </div>
                        <div>
                          <div style={{ fontWeight: 700, color: '#0f172a', fontSize: '11.5px' }}>{app.empName}</div>
                          <div style={{ fontSize: '10px', color: '#64748b' }}>
                            {app.empId} • {app.role} ({app.site})
                          </div>
                        </div>
                      </div>
                    </td>



                    {/* Leave Dates & Duration */}
                    <td>
                      <div style={{ fontWeight: 650, color: '#0f172a', fontSize: '11.5px' }}>{app.durationLabel}</div>
                      <div style={{ fontSize: '10px', color: '#64748b' }}>{app.datesLabel}</div>
                    </td>



                    {/* Quota Balance */}
                    <td>
                      <div style={{ fontWeight: 650, color: '#0f172a', fontSize: '11.5px' }}>
                        {app.quotaLeft} Days Left
                      </div>
                      <div style={{ fontSize: '10px', color: '#64748b' }}>
                        Total Quota: {app.quotaTotal} Days
                      </div>
                    </td>



                    {/* Site Reliever / Handover */}
                    <td>
                      <div style={{ fontWeight: 600, color: '#0f172a', fontSize: '11.5px' }}>{app.reliever}</div>
                      <div style={{ fontSize: '10px', color: '#64748b' }}>Phone: {app.relieverPhone}</div>
                    </td>



                    {/* Reason & Documents */}
                    <td>
                      <div style={{ color: '#334155', maxWidth: '240px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', fontSize: '11px' }} title={app.reason}>
                        {app.reason}
                      </div>
                      {app.docVerified ? (
                        <span style={{ display: 'inline-flex', alignItems: 'center', gap: '3px', color: '#475569', fontSize: '10px', fontWeight: 500 }}>
                          <FileCheck size={11} color="#64748b" /> Doctor Slip Verified
                        </span>
                      ) : (
                        <span style={{ fontSize: '9.5px', color: '#94a3b8' }}>Standard 1-Day Notice</span>
                      )}
                    </td>



                    {/* Status */}
                    <td style={{ textAlign: 'center' }}>
                      {app.status === 'APPROVED' && (
                        <span style={{ display: 'inline-flex', alignItems: 'center', gap: '3px', background: '#f0fdf4', color: '#166534', border: '1px solid #dcfce7', padding: '1px 6px', borderRadius: '3px', fontSize: '9.5px', fontWeight: 600, textTransform: 'uppercase' }}>
                          <Check size={10} /> Approved
                        </span>
                      )}
                      {app.status === 'PENDING_CEO' && (
                        <span style={{ display: 'inline-flex', alignItems: 'center', gap: '3px', background: '#fffbeb', color: '#92400e', border: '1px solid #fef3c7', padding: '1px 6px', borderRadius: '3px', fontSize: '9.5px', fontWeight: 600, textTransform: 'uppercase' }}>
                          <Clock size={10} /> Pending CEO
                        </span>
                      )}
                      {app.status === 'PENDING_PM' && (
                        <span style={{ display: 'inline-flex', alignItems: 'center', gap: '3px', background: '#fffbeb', color: '#92400e', border: '1px solid #fef3c7', padding: '1px 6px', borderRadius: '3px', fontSize: '9.5px', fontWeight: 600, textTransform: 'uppercase' }}>
                          <Clock size={10} /> Pending PM
                        </span>
                      )}
                      {app.status === 'PENDING' && (
                        <span style={{ display: 'inline-flex', alignItems: 'center', gap: '3px', background: '#fffbeb', color: '#92400e', border: '1px solid #fef3c7', padding: '1px 6px', borderRadius: '3px', fontSize: '9.5px', fontWeight: 600, textTransform: 'uppercase' }}>
                          <Clock size={10} /> Pending Review
                        </span>
                      )}
                      {app.status === 'REJECTED' && (
                        <span style={{ display: 'inline-flex', alignItems: 'center', gap: '3px', background: '#fef2f2', color: '#991b1b', border: '1px solid #fee2e2', padding: '1px 6px', borderRadius: '3px', fontSize: '9.5px', fontWeight: 600, textTransform: 'uppercase' }}>
                          <X size={10} /> Rejected
                        </span>
                      )}
                    </td>



                    {/* Actions */}
                    <td style={{ textAlign: 'right' }}>
                      <div style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                        {app.status !== 'APPROVED' && (
                          <button
                            type="button"
                            onClick={() => handleApprove(app.id, app.empName)}
                            style={{
                              padding: '2px 7px',
                              background: '#0f172a',
                              color: '#ffffff',
                              border: 'none',
                              borderRadius: '4px',
                              fontSize: '10px',
                              fontWeight: 600,
                              cursor: 'pointer',
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: '3px',
                              height: '24px',
                            }}
                            title="Approve immediately"
                          >
                            <Check size={10} /> Approve
                          </button>
                        )}



                        <button
                          type="button"
                          onClick={() => setSelectedApp(app)}
                          style={{
                            padding: '2px 7px',
                            background: '#ffffff',
                            color: '#334155',
                            border: '1px solid #cbd5e1',
                            borderRadius: '4px',
                            fontSize: '10px',
                            fontWeight: 500,
                            cursor: 'pointer',
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: '3px',
                            height: '24px',
                          }}
                          title="Open detailed modal"
                        >
                          <Eye size={10} /> View Details
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>



      {/* 6. DETAILED MODAL WINDOW FOR LEAVE APPLICATION */}
      {selectedApp && (
        <div className="leave-v4-modal-overlay" onClick={() => setSelectedApp(null)}>
          <div className="leave-v4-modal-box" onClick={(e) => e.stopPropagation()}>
            {/* Modal Header */}
            <div className="leave-v4-modal-header">
              <div>
                <h3 style={{ fontSize: '14px', fontWeight: 750, color: '#0f172a', margin: 0, display: 'flex', alignItems: 'center', gap: '6px' }}>
                  <Calendar size={15} color="#242b5f" />
                  {selectedApp.appCode}: {selectedApp.leaveTypeLabel}
                </h3>
                <p style={{ fontSize: '11px', color: '#64748b', margin: '2px 0 0 0' }}>
                  Applicant: <strong>{selectedApp.empName}</strong> ({selectedApp.empId}) • {selectedApp.role}
                </p>
              </div>
              <button
                type="button"
                onClick={() => setSelectedApp(null)}
                style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#94a3b8', padding: '4px' }}
              >
                <X size={16} />
              </button>
            </div>



            {/* Modal Body */}
            <div className="leave-v4-modal-body">
              {/* Applicant Card */}
              <div style={{ background: '#f8fafc', padding: '10px 14px', borderRadius: '8px', border: '1px solid #e2e8f0', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div>
                  <div style={{ fontWeight: 750, color: '#0f172a', fontSize: '13px' }}>{selectedApp.empName}</div>
                  <div style={{ fontSize: '11px', color: '#64748b' }}>
                    {selectedApp.role} • {selectedApp.dept} • Site: <strong>{selectedApp.site}</strong>
                  </div>
                </div>
                <div style={{ textAlign: 'right' }}>
                  <div style={{ fontSize: '10px', textTransform: 'uppercase', color: '#94a3b8', fontWeight: 700 }}>Quota Available</div>
                  <div style={{ fontSize: '13px', fontWeight: 750, color: '#0f172a' }}>{selectedApp.quotaLeft} / {selectedApp.quotaTotal} Days</div>
                </div>
              </div>



              {/* Schedule Details Strip */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                <div style={{ background: '#f8fafc', padding: '10px', borderRadius: '6px', border: '1px solid #e2e8f0' }}>
                  <span style={{ fontSize: '9.5px', color: '#64748b', fontWeight: 700, textTransform: 'uppercase' }}>Duration &amp; Dates</span>
                  <div style={{ fontWeight: 700, color: '#0f172a', fontSize: '12px', marginTop: '2px' }}>{selectedApp.durationLabel}</div>
                  <div style={{ fontSize: '10.5px', color: '#64748b' }}>{selectedApp.datesLabel}</div>
                </div>



                <div style={{ background: '#f8fafc', padding: '10px', borderRadius: '6px', border: '1px solid #e2e8f0' }}>
                  <span style={{ fontSize: '9.5px', color: '#64748b', fontWeight: 700, textTransform: 'uppercase' }}>Reliever Handover</span>
                  <div style={{ fontWeight: 700, color: '#0f172a', fontSize: '12px', marginTop: '2px' }}>{selectedApp.reliever}</div>
                  <div style={{ fontSize: '10.5px', color: '#64748b' }}>Phone: {selectedApp.relieverPhone}</div>
                </div>
              </div>



              {/* Stated Reason */}
              <div style={{ background: '#ffffff', padding: '10px 12px', borderRadius: '6px', border: '1px solid #e2e8f0' }}>
                <span style={{ fontSize: '9.5px', color: '#94a3b8', fontWeight: 700, textTransform: 'uppercase' }}>Stated Reason for Absence</span>
                <p style={{ margin: '4px 0 0 0', color: '#1e293b', fontSize: '11.5px', lineHeight: 1.4 }}>
                  {selectedApp.reason}
                </p>
                {selectedApp.relieverNote && (
                  <div style={{ marginTop: '5px', fontSize: '10.5px', color: '#64748b', fontStyle: 'italic' }}>
                    Coverage Note: "{selectedApp.relieverNote}"
                  </div>
                )}
              </div>



              {/* Evidence / Medical document */}
              {selectedApp.docVerified && (
                <div style={{ background: '#f8fafc', padding: '8px 12px', borderRadius: '6px', border: '1px solid #e2e8f0', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                    <FileCheck size={13} color="#475569" />
                    <div>
                      <div style={{ fontSize: '11px', fontWeight: 700, color: '#0f172a' }}>Medical Slip Verified</div>
                      <div style={{ fontSize: '10px', color: '#64748b' }}>{selectedApp.docName || 'Prescription_slip.pdf'}</div>
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={() => setToast('Opening verified medical certificate document...')}
                    style={{ background: '#ffffff', border: '1px solid #cbd5e1', borderRadius: '4px', padding: '2px 8px', fontSize: '10px', fontWeight: 600, color: '#334155', cursor: 'pointer' }}
                  >
                    View Slip
                  </button>
                </div>
              )}



              {/* Approval Workflow Trail */}
              <div style={{ background: '#f8fafc', padding: '10px 12px', borderRadius: '6px', border: '1px solid #e2e8f0' }}>
                <span style={{ fontSize: '9.5px', color: '#94a3b8', fontWeight: 700, textTransform: 'uppercase' }}>Multi-Tier Approval Trail</span>
                <div style={{ display: 'flex', alignItems: 'center', gap: '6px', marginTop: '6px' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '4px', fontSize: '10.5px', color: '#166534', fontWeight: 600 }}>
                    <CheckCircle2 size={11} /> Site PM
                  </div>
                  <ChevronRight size={11} color="#94a3b8" />
                  <div style={{ display: 'flex', alignItems: 'center', gap: '4px', fontSize: '10.5px', color: selectedApp.status === 'APPROVED' ? '#166534' : '#0f172a', fontWeight: 700 }}>
                    <Shield size={11} /> HR Review Desk
                  </div>
                  <ChevronRight size={11} color="#94a3b8" />
                  <div style={{ display: 'flex', alignItems: 'center', gap: '4px', fontSize: '10.5px', color: selectedApp.isLong ? (selectedApp.status === 'APPROVED' ? '#166534' : '#92400e') : '#94a3b8', fontWeight: selectedApp.isLong ? 700 : 500 }}>
                    <Clock size={11} /> CEO Executive
                  </div>
                </div>
                <div style={{ fontSize: '10px', color: '#64748b', marginTop: '4px' }}>
                  Status: <strong>{selectedApp.approvalStage}</strong>
                </div>
              </div>
            </div>



            {/* Modal Footer */}
            <div className="leave-v4-modal-footer">
              <button
                type="button"
                onClick={() => {
                  window.print();
                  setToast('Printing leave approval voucher...');
                }}
                className="leave-v4-btn-action"
                style={{ marginRight: 'auto' }}
              >
                <Printer size={13} />
                Print Voucher
              </button>



              <button
                type="button"
                onClick={() => setSelectedApp(null)}
                className="leave-v4-btn-action"
              >
                Close
              </button>



              {isAdmin && selectedApp.status !== 'REJECTED' && (
                <button
                  type="button"
                  onClick={() => {
                    handleReject(selectedApp.id, selectedApp.empName);
                    setSelectedApp(null);
                  }}
                  style={{
                    height: '30px',
                    padding: '0 12px',
                    background: '#fee2e2',
                    color: '#b91c1c',
                    border: '1px solid #fecaca',
                    borderRadius: '6px',
                    fontSize: '11.5px',
                    fontWeight: 700,
                    cursor: 'pointer',
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '4px',
                  }}
                >
                  <X size={13} />
                  Reject
                </button>
              )}



              {isAdmin && selectedApp.status !== 'APPROVED' && (
                <button
                  type="button"
                  onClick={() => {
                    handleApprove(selectedApp.id, selectedApp.empName);
                    setSelectedApp(null);
                  }}
                  style={{
                    height: '30px',
                    padding: '0 14px',
                    background: '#15803d',
                    color: '#ffffff',
                    border: 'none',
                    borderRadius: '6px',
                    fontSize: '11.5px',
                    fontWeight: 700,
                    cursor: 'pointer',
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '5px',
                    boxShadow: '0 1px 2px rgba(0,0,0,0.05)',
                  }}
                >
                  <Check size={13} />
                  Approve Application
                </button>
              )}
            </div>
          </div>
        </div>
      )}



      {/* 7. APPLY LEAVE APPLICATION MODAL */}
      {isApplyModalOpen && (
        <div className="leave-v4-modal-overlay" onClick={() => setIsApplyModalOpen(false)}>
          <div className="leave-v4-modal-box" onClick={(e) => e.stopPropagation()}>
            {/* Modal Header */}
            <div className="leave-v4-modal-header">
              <div>
                <h3 style={{ fontSize: '13px', fontWeight: 700, color: '#0f172a', margin: 0, display: 'flex', alignItems: 'center', gap: '6px' }}>
                  <Plus size={15} color="#475569" />
                  Submit Leave Application
                </h3>
                <p style={{ fontSize: '10.5px', color: '#64748b', margin: '2px 0 0 0' }}>
                  Multi-tier approval workflow (Site PM &gt; HR &gt; CEO)
                </p>
              </div>
              <button
                type="button"
                onClick={() => setIsApplyModalOpen(false)}
                style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#94a3b8' }}
              >
                <X size={15} />
              </button>
            </div>



            {/* Modal Form */}
            <form onSubmit={handleSubmitApply}>
              <div className="leave-v4-modal-body">
                {/* 1. Select Employee & Quotas */}
                <div style={{ background: '#f8fafc', padding: '10px 12px', borderRadius: '6px', border: '1px solid #e2e8f0', display: 'flex', flexDirection: 'column', gap: '8px' }}>
                  <label style={{ fontSize: '11px', fontWeight: 700, color: '#334155' }}>
                    Select Employee <span style={{ color: '#ef4444' }}>*</span>
                  </label>
                  <select
                    className="leave-v4-select"
                    style={{ width: '100%', height: '30px' }}
                    value={newEmpCode}
                    onChange={(e) => setNewEmpCode(e.target.value)}
                  >
                    {dbEmployees.length > 0 ? (
                      dbEmployees.map((emp) => (
                        <option key={emp.id} value={emp.id}>
                          {emp.name || emp.email || 'Employee'} ({emp.identityNumber || `EMP-${emp.employeeNumber || '1000'}`})
                        </option>
                      ))
                    ) : (
                      <>
                        <option value="AK">Engr. Ali Khan (EMP-1042) - HVAC Site Engr (Karachi Tower)</option>
                        <option value="SA">Sara Ahmed (EMP-1043) - Sales Executive (Lahore HQ)</option>
                        <option value="UT">Usman Tariq (EMP-1044) - MEP Supervisor (Islamabad Complex)</option>
                        <option value="ZA">Zainab Ali (EMP-1045) - HR Assistant (Lahore HQ)</option>
                        <option value="BA">Bilal Ahmed (EMP-1046) - Field Tech (Karachi Tower)</option>
                      </>
                    )}
                  </select>



                  {/* Quota Strip */}
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '6px', paddingTop: '4px' }}>
                    <div style={{ background: '#ffffff', padding: '6px', borderRadius: '5px', border: '1px solid #e2e8f0', textAlign: 'center' }}>
                      <span style={{ fontSize: '9px', color: '#94a3b8', textTransform: 'uppercase', fontWeight: 700 }}>Casual Leaves</span>
                      <div style={{ fontSize: '11.5px', fontWeight: 700, color: '#0f172a' }}>8 Left <span style={{ fontSize: '9px', color: '#94a3b8' }}>/ 10</span></div>
                    </div>
                    <div style={{ background: '#ffffff', padding: '6px', borderRadius: '5px', border: '1px solid #e2e8f0', textAlign: 'center' }}>
                      <span style={{ fontSize: '9px', color: '#94a3b8', textTransform: 'uppercase', fontWeight: 700 }}>Sick Leaves</span>
                      <div style={{ fontSize: '11.5px', fontWeight: 700, color: '#0f172a' }}>7 Left <span style={{ fontSize: '9px', color: '#94a3b8' }}>/ 8</span></div>
                    </div>
                    <div style={{ background: '#ffffff', padding: '6px', borderRadius: '5px', border: '1px solid #e2e8f0', textAlign: 'center' }}>
                      <span style={{ fontSize: '9px', color: '#94a3b8', textTransform: 'uppercase', fontWeight: 700 }}>Annual Paid</span>
                      <div style={{ fontSize: '11.5px', fontWeight: 700, color: '#0f172a' }}>10 Left <span style={{ fontSize: '9px', color: '#94a3b8' }}>/ 14</span></div>
                    </div>
                  </div>
                </div>



                {/* 2. Leave Category & Day Fraction */}
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                  <div>
                    <label style={{ fontSize: '11px', fontWeight: 700, color: '#334155', display: 'block', marginBottom: '4px' }}>
                      Leave Type <span style={{ color: '#ef4444' }}>*</span>
                    </label>
                    <select
                      className="leave-v4-select"
                      style={{ width: '100%', height: '32px' }}
                      value={newType}
                      onChange={(e) => setNewType(e.target.value as any)}
                    >
                      <option value="Casual Leave">Casual Leave (Urgent Chore)</option>
                      <option value="Sick Leave">Sick / Medical Leave</option>
                      <option value="Annual Leave">Annual Paid Leave (Long Vacation)</option>
                      <option value="Short Permission">Short Leave / 3h Permission</option>
                      <option value="Leave Without Pay">Leave Without Pay (LWP)</option>
                    </select>
                  </div>



                  <div>
                    <label style={{ fontSize: '11px', fontWeight: 700, color: '#334155', display: 'block', marginBottom: '4px' }}>
                      Day Fraction / Schedule
                    </label>
                    <select
                      className="leave-v4-select"
                      style={{ width: '100%', height: '32px' }}
                      value={newFraction}
                      onChange={(e) => setNewFraction(e.target.value)}
                    >
                      <option value="1">Full Day Schedule</option>
                      <option value="0.5">Half Day (First Half: 08:00 - 01:00)</option>
                      <option value="0.5">Half Day (Second Half: 01:00 - 05:00)</option>
                      <option value="0.3">Short Permission (2-3 Hours)</option>
                    </select>
                  </div>
                </div>



                {/* 3. Dates & Auto-Calculated Working Days */}
                <div style={{ background: '#eff6ff', padding: '10px 12px', borderRadius: '8px', border: '1px solid #bfdbfe', display: 'flex', flexDirection: 'column', gap: '8px' }}>
                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '8px' }}>
                    <div>
                      <label style={{ fontSize: '10.5px', fontWeight: 700, color: '#1e293b', display: 'block', marginBottom: '2px' }}>From Date</label>
                      <input
                        type="date"
                        className="leave-v4-search-input"
                        style={{ height: '30px' }}
                        value={newFromDate}
                        onChange={(e) => setNewFromDate(e.target.value)}
                        required
                      />
                    </div>



                    <div>
                      <label style={{ fontSize: '10.5px', fontWeight: 700, color: '#1e293b', display: 'block', marginBottom: '2px' }}>To Date</label>
                      <input
                        type="date"
                        className="leave-v4-search-input"
                        style={{ height: '30px' }}
                        value={newToDate}
                        onChange={(e) => setNewToDate(e.target.value)}
                        required
                      />
                    </div>



                    <div>
                      <label style={{ fontSize: '10.5px', fontWeight: 700, color: '#1e293b', display: 'block', marginBottom: '2px' }}>Duration</label>
                      <div style={{ background: '#ffffff', border: '1px solid #bfdbfe', borderRadius: '6px', height: '30px', display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 750, color: '#1d4ed8', fontSize: '11.5px' }}>
                        {calculatedDuration.text}
                      </div>
                    </div>
                  </div>



                  {/* Warning if > 3 days */}
                  {calculatedDuration.isLong ? (
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', fontSize: '10.5px', color: '#c2410c', background: '#fff7ed', padding: '4px 8px', borderRadius: '4px', border: '1px solid #fed7aa' }}>
                      <span style={{ display: 'flex', alignItems: 'center', gap: '4px', fontWeight: 700 }}>
                        <AlertTriangle size={12} /> &gt; 3 Days requires CEO Approval
                      </span>
                      <span style={{ background: '#ffedd5', color: '#9a3412', padding: '1px 5px', borderRadius: '3px', fontWeight: 700 }}>CEO Loop</span>
                    </div>
                  ) : (
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', fontSize: '10.5px', color: '#15803d' }}>
                      <span><Shield size={11} style={{ display: 'inline', marginRight: '4px' }} /> Routine leave (&le; 3 days) approved by Site PM &amp; HR</span>
                      <span style={{ fontWeight: 700 }}>Standard Approval</span>
                    </div>
                  )}
                </div>



                {/* 4. Site Handover & Emergency Contact */}
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                  <div>
                    <label style={{ fontSize: '11px', fontWeight: 700, color: '#334155', display: 'block', marginBottom: '4px' }}>
                      Site Work Handover To (Reliever)
                    </label>
                    <select
                      className="leave-v4-select"
                      style={{ width: '100%', height: '32px' }}
                      value={newReliever}
                      onChange={(e) => setNewReliever(e.target.value)}
                    >
                      <option>Engr. Tariq Farooq (Project Manager)</option>
                      <option>Usman Rafiq (Site Supervisor)</option>
                      <option>Engr. Bilal Farooq (Sr. HVAC Engr)</option>
                      <option>Hamza Tariq (Junior Tech)</option>
                      <option>Sara Ahmed (Sales / Desk)</option>
                    </select>
                  </div>



                  <div>
                    <label style={{ fontSize: '11px', fontWeight: 700, color: '#334155', display: 'block', marginBottom: '4px' }}>
                      Emergency Contact Phone
                    </label>
                    <input
                      type="text"
                      className="leave-v4-search-input"
                      style={{ height: '32px' }}
                      value={newPhone}
                      onChange={(e) => setNewPhone(e.target.value)}
                      placeholder="+92 300 0000000"
                    />
                  </div>
                </div>



                {/* 5. Medical file for Sick Leave */}
                {newType === 'Sick Leave' && (
                  <div style={{ background: '#fff1f2', padding: '10px 12px', borderRadius: '8px', border: '1px solid #fecdd3', display: 'flex', flexDirection: 'column', gap: '4px' }}>
                    <label style={{ fontSize: '11px', fontWeight: 700, color: '#9f1239', display: 'flex', alignItems: 'center', gap: '4px' }}>
                      <Paperclip size={12} /> Attach Medical Certificate / Prescription Slip
                    </label>
                    <input
                      type="file"
                      onChange={() => setHasMedicalFile(true)}
                      style={{ fontSize: '11px', color: '#475569' }}
                    />
                    <span style={{ fontSize: '10px', color: '#be123c' }}>
                      Doctor's certificate required for medical leaves exceeding 2 days.
                    </span>
                  </div>
                )}



                {/* 6. Reason for Absence */}
                <div>
                  <label style={{ fontSize: '11px', fontWeight: 700, color: '#334155', display: 'block', marginBottom: '4px' }}>
                    Reason for Absence <span style={{ color: '#ef4444' }}>*</span>
                  </label>
                  <textarea
                    rows={2}
                    className="leave-v4-search-input"
                    style={{ height: '56px', paddingTop: '6px', resize: 'none' }}
                    placeholder="Provide detailed reason for absence and handover notes..."
                    value={newReason}
                    onChange={(e) => setNewReason(e.target.value)}
                    required
                  />
                </div>
              </div>



              {/* Form Footer */}
              <div className="leave-v4-modal-footer">
                <button
                  type="button"
                  onClick={() => setIsApplyModalOpen(false)}
                  className="leave-v4-btn-action"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="leave-v4-btn-primary"
                >
                  <Plus size={13} />
                  Submit Application
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

