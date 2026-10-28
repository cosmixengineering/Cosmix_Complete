import { useState, useMemo } from 'react';
import { Link, useLocation } from 'react-router-dom';
import {
  ClipboardCheck,
  Users,
  Calendar,
  Coins,
  Search,
  RefreshCw,
  Eye,
  Clock,
  Shield,
  UserRound,
  ArrowUpRight,
  Inbox,
  KeyRound,
} from 'lucide-react';
import { Dialog } from '@/components/ui/Dialog';
import { Button } from '@/components/ui/Button';
import { useHrAccess } from '../hooks/use-hr-access';
import { useHrQuery } from '../hooks/use-hr-queries';
import { pageOf, employeeSchema } from '../schemas/contracts';
import { submissionSummarySchema, type SubmissionSummary } from '../schemas/approvals';
import { labelDate } from '../utils/messages';
import { HiringSubmissionReview } from '../components/approvals/HiringSubmissionReview';
import { EmployeeAccessSetup } from '../components/approvals/EmployeeAccessSetup';
import '../components/approvals/hiring-review.css';
import '@/styles/hr-requests.css';
import '@/styles/hr-dashboard.css';

interface GenericApprovalItem {
  id: string;
  category: 'onboarding' | 'leave' | 'advance' | 'update';
  categoryLabel: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: number;
  avatarText: string;
  requestType: string;
  details: string;
  submittedAt: string;
  statusText: string;
  statusVariant: 'pending' | 'review' | 'draft';
  approverRole: string;
  phone?: string | null;
  identityNumber?: string | null;
  email?: string | null;
  submissionRef?: SubmissionSummary;
}

export function HiringApprovalsPage() {
  const access = useHrAccess();
  const location = useLocation();
  const basePath = location.pathname.startsWith('/admin') ? '/admin' : '/hr';
  const isAdminReviewer = access.can('hr.employees.submissions.review');

  const [activeTab, setActiveTab] = useState<'all' | 'onboarding' | 'leave' | 'advance'>('all');
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedItem, setSelectedItem] = useState<GenericApprovalItem | null>(null);

  // Admin decision modal state
  const [adminSubmission, setAdminSubmission] = useState<SubmissionSummary | null>(null);
  const [reviewDirty, setReviewDirty] = useState(false);
  const [reviewBusy, setReviewBusy] = useState(false);
  const [adminMessage, setAdminMessage] = useState('');
  const [accessEmployee, setAccessEmployee] = useState<{
    id: string;
    name: string;
    number: number;
  } | null>(null);

  // 1. Fetch Draft Employees (14 drafts awaiting onboarding review/completion)
  const draftsQuery = useHrQuery('employees?status=0&pageSize=50&page=1', pageOf(employeeSchema), [
    'hr.employees.read',
  ]);

  // 2. Fetch Formal Hiring Submissions (if permitted)
  const submissionsQuery = useHrQuery(
    'employees/submissions?pageSize=50&page=1&status=1',
    pageOf(submissionSummarySchema),
    ['hr.employees.read', 'hr.employees.submissions.review'],
  );

  const refreshing = draftsQuery.isFetching || submissionsQuery.isFetching;

  // Build Unified Approvals List
  const approvalItems: GenericApprovalItem[] = useMemo(() => {
    const list: GenericApprovalItem[] = [];

    // Formal Submissions (if available)
    if (submissionsQuery.data?.items?.length) {
      for (const s of submissionsQuery.data.items) {
        const empName = s.name || `Employee #${s.employeeNumber}`;
        const initials =
          empName
            .split(' ')
            .filter(Boolean)
            .map((n) => n[0])
            .join('')
            .slice(0, 2)
            .toUpperCase() || 'EM';

        list.push({
          id: `sub-${s.id}`,
          category: 'onboarding',
          categoryLabel: 'Hiring Submission',
          employeeId: s.employeeId,
          employeeName: empName,
          employeeNumber: s.employeeNumber,
          avatarText: initials,
          requestType: 'Formal Hiring Sign-off',
          details: `Revision ${s.version} • Profile and compliance evidence submitted for final management authorization`,
          submittedAt: labelDate(s.submittedAt),
          statusText: 'Awaiting Admin Decision',
          statusVariant: 'review',
          approverRole: 'System Administrator',
          submissionRef: s,
        });
      }
    }

    // Draft Onboardings Pending Completion & Approval
    if (draftsQuery.data?.items?.length) {
      for (const emp of draftsQuery.data.items) {
        // Skip if already in formal submissions
        if (submissionsQuery.data?.items?.some((s) => s.employeeId === emp.id)) continue;

        const empName = emp.name || `Draft Employee #${emp.employeeNumber}`;
        const initials =
          empName
            .split(' ')
            .filter(Boolean)
            .map((n) => n[0])
            .join('')
            .slice(0, 2)
            .toUpperCase() || 'EM';

        list.push({
          id: `draft-${emp.id}`,
          category: 'onboarding',
          categoryLabel: 'Onboarding Draft',
          employeeId: emp.id,
          employeeName: empName,
          employeeNumber: emp.employeeNumber,
          avatarText: initials,
          requestType: 'New Hire Onboarding Setup',
          details: `CNIC: ${emp.identityNumber || 'Pending'} • Phone: ${emp.phone || 'Pending'} • Documents & verification queued`,
          submittedAt: emp.createdAt ? labelDate(emp.createdAt) : 'Recently created',
          statusText: 'Pending Admin Approval',
          statusVariant: 'pending',
          approverRole: 'System Administrator',
          phone: emp.phone,
          identityNumber: emp.identityNumber,
          email: emp.email,
        });
      }
    }

    return list;
  }, [submissionsQuery.data, draftsQuery.data]);

  // Counts
  const totalCount = approvalItems.length;
  const onboardingCount = approvalItems.filter((i) => i.category === 'onboarding').length;
  const leaveCount = approvalItems.filter((i) => i.category === 'leave').length;
  const advanceCount = approvalItems.filter((i) => i.category === 'advance').length;

  // Filtered List
  const filteredItems = useMemo(() => {
    return approvalItems.filter((item) => {
      // Category filter
      if (activeTab !== 'all' && item.category !== activeTab) return false;

      // Search term
      if (searchTerm.trim()) {
        const query = searchTerm.toLowerCase();
        const matchesName = item.employeeName.toLowerCase().includes(query);
        const matchesCode = String(item.employeeNumber).includes(query);
        const matchesType = item.requestType.toLowerCase().includes(query);
        const matchesCnic = item.identityNumber?.toLowerCase().includes(query);
        if (!matchesName && !matchesCode && !matchesType && !matchesCnic) return false;
      }

      return true;
    });
  }, [approvalItems, activeTab, searchTerm]);

  return (
    <div className="hr-requests-shell">
      {/* 1. Header with Breadcrumb & Title */}
      <header className="hr-requests-header">
        <div className="hr-requests-breadcrumb">
          <Link to="/hr/dashboard">Dashboard</Link>
          <span>/</span>
          <span>Human Resources</span>
          <span>/</span>
          <span style={{ color: '#0f172a', fontWeight: 600 }}>Pending Approvals</span>
        </div>

        <div className="hr-requests-header-main">
          <div className="hr-requests-title-group">
            <div className="hr-requests-icon-badge">
              <ClipboardCheck size={15} color="#0f172a" />
            </div>
            <div>
              <span className="hr-requests-tagline">HR Operations • Approvals Queue</span>
              <h1 className="hr-requests-title">Pending Approvals</h1>
              <p className="hr-requests-subtitle">
                Comprehensive tracking list of onboarding, leave, and request submissions awaiting
                management review.
              </p>
            </div>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
            <Button
              variant="secondary"
              style={{
                height: '26px',
                padding: '0 9px',
                fontSize: '11px',
                display: 'inline-flex',
                alignItems: 'center',
                gap: '5px',
              }}
              disabled={refreshing}
              onClick={() => {
                void draftsQuery.refetch();
                if (isAdminReviewer) void submissionsQuery.refetch();
              }}
            >
              <RefreshCw size={12} className={refreshing ? 'animate-spin' : ''} />
              <span>{refreshing ? 'Refreshing…' : 'Refresh'}</span>
            </Button>
          </div>
        </div>
      </header>

      {adminMessage && (
        <div className="hr-message" style={{ margin: '0 0 8px' }}>
          {adminMessage}
        </div>
      )}

      {/* 2. Top KPI Cards Grid */}
      <div className="hr-requests-kpi-grid">
        <div
          className={`hr-requests-kpi-card ${activeTab === 'all' ? 'is-active' : ''}`}
          onClick={() => setActiveTab('all')}
        >
          <div className="hr-requests-kpi-icon">
            <ClipboardCheck size={14} />
          </div>
          <div className="hr-requests-kpi-content">
            <span
              style={{
                fontSize: '9.5px',
                fontWeight: 600,
                color: '#64748b',
                textTransform: 'uppercase',
              }}
            >
              TOTAL PENDING
            </span>
            <div style={{ fontSize: '15px', fontWeight: 700, color: '#0f172a', lineHeight: 1.1 }}>
              {totalCount}
            </div>
            <span style={{ fontSize: '10px', color: '#64748b' }}>Across all categories</span>
          </div>
        </div>

        <div
          className={`hr-requests-kpi-card ${activeTab === 'onboarding' ? 'is-active' : ''}`}
          onClick={() => setActiveTab('onboarding')}
        >
          <div className="hr-requests-kpi-icon" style={{ background: '#eff6ff', color: '#2563eb' }}>
            <Users size={14} />
          </div>
          <div className="hr-requests-kpi-content">
            <span
              style={{
                fontSize: '9.5px',
                fontWeight: 600,
                color: '#64748b',
                textTransform: 'uppercase',
              }}
            >
              ONBOARDING &amp; HIRING
            </span>
            <div style={{ fontSize: '15px', fontWeight: 700, color: '#0f172a', lineHeight: 1.1 }}>
              {onboardingCount}
            </div>
            <span style={{ fontSize: '10px', color: '#64748b' }}>
              New hire submissions &amp; drafts
            </span>
          </div>
        </div>

        <div
          className={`hr-requests-kpi-card ${activeTab === 'leave' ? 'is-active' : ''}`}
          onClick={() => setActiveTab('leave')}
        >
          <div className="hr-requests-kpi-icon" style={{ background: '#f0fdf4', color: '#16a34a' }}>
            <Calendar size={14} />
          </div>
          <div className="hr-requests-kpi-content">
            <span
              style={{
                fontSize: '9.5px',
                fontWeight: 600,
                color: '#64748b',
                textTransform: 'uppercase',
              }}
            >
              LEAVE APPLICATIONS
            </span>
            <div style={{ fontSize: '15px', fontWeight: 700, color: '#0f172a', lineHeight: 1.1 }}>
              {leaveCount}
            </div>
            <span style={{ fontSize: '10px', color: '#64748b' }}>Awaiting manager action</span>
          </div>
        </div>

        <div
          className={`hr-requests-kpi-card ${activeTab === 'advance' ? 'is-active' : ''}`}
          onClick={() => setActiveTab('advance')}
        >
          <div className="hr-requests-kpi-icon" style={{ background: '#faf5ff', color: '#9333ea' }}>
            <Coins size={14} />
          </div>
          <div className="hr-requests-kpi-content">
            <span
              style={{
                fontSize: '9.5px',
                fontWeight: 600,
                color: '#64748b',
                textTransform: 'uppercase',
              }}
            >
              CLAIMS &amp; ADVANCES
            </span>
            <div style={{ fontSize: '15px', fontWeight: 700, color: '#0f172a', lineHeight: 1.1 }}>
              {advanceCount}
            </div>
            <span style={{ fontSize: '10px', color: '#64748b' }}>
              Salary advance &amp; expenses
            </span>
          </div>
        </div>
      </div>

      {/* 3. Filter Bar & Search */}
      <div className="hr-requests-filter-row">
        <div className="hr-requests-status-pills">
          <button
            type="button"
            className={`hr-requests-pill-btn ${activeTab === 'all' ? 'is-active' : ''}`}
            onClick={() => setActiveTab('all')}
          >
            <span>All Requests</span>
            <span style={{ opacity: 0.8, fontSize: '11px' }}>({totalCount})</span>
          </button>
          <button
            type="button"
            className={`hr-requests-pill-btn ${activeTab === 'onboarding' ? 'is-active' : ''}`}
            onClick={() => setActiveTab('onboarding')}
          >
            <span>Onboarding &amp; Hiring</span>
            <span style={{ opacity: 0.8, fontSize: '11px' }}>({onboardingCount})</span>
          </button>
          <button
            type="button"
            className={`hr-requests-pill-btn ${activeTab === 'leave' ? 'is-active' : ''}`}
            onClick={() => setActiveTab('leave')}
          >
            <span>Leave Requests</span>
            <span style={{ opacity: 0.8, fontSize: '11px' }}>({leaveCount})</span>
          </button>
          <button
            type="button"
            className={`hr-requests-pill-btn ${activeTab === 'advance' ? 'is-active' : ''}`}
            onClick={() => setActiveTab('advance')}
          >
            <span>Claims &amp; Advances</span>
            <span style={{ opacity: 0.8, fontSize: '11px' }}>({advanceCount})</span>
          </button>
        </div>

        <div className="hr-requests-search-box">
          <Search size={15} className="hr-requests-search-icon" />
          <input
            type="text"
            className="hr-requests-search-input"
            placeholder="Search by employee name, code, CNIC..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
          />
        </div>
      </div>

      {/* 4. Approvals List */}
      <div className="hr-requests-container">
        <div className="hr-requests-count-summary">
          <span>
            Showing {filteredItems.length} of {totalCount} pending approvals
          </span>
          <span style={{ fontSize: '11.5px', color: '#94a3b8' }}>
            Sorted by most recent submission
          </span>
        </div>

        {filteredItems.length === 0 ? (
          <div className="hr-requests-empty">
            <div
              className="hr-requests-empty-icon"
              style={{
                background: '#f8fafc',
                borderRadius: '50%',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                margin: '0 auto 12px',
              }}
            >
              <Inbox size={26} color="#94a3b8" />
            </div>
            <h3 style={{ fontSize: '15px', fontWeight: 600, color: '#0f172a', margin: '0 0 4px' }}>
              No pending approvals found
            </h3>
            <p
              style={{
                fontSize: '12.5px',
                color: '#64748b',
                margin: '0 0 16px',
                maxWidth: '360px',
              }}
            >
              {searchTerm
                ? 'No requests match your current search query. Try clearing the search filter.'
                : 'All requests in this category have been processed or none are currently pending review.'}
            </p>
            {searchTerm && (
              <Button variant="secondary" onClick={() => setSearchTerm('')}>
                Clear Search Filter
              </Button>
            )}
          </div>
        ) : (
          filteredItems.map((item) => (
            <article key={item.id} className="hr-request-card">
              {/* Left Column: Avatar + Employee Info */}
              <div className="hr-request-card-left">
                <div className="hr-request-avatar">{item.avatarText}</div>
                <div className="hr-request-info">
                  <div className="hr-request-emp-name">
                    <span>{item.employeeName}</span>
                    <span className="hr-request-emp-code">
                      #{String(item.employeeNumber).padStart(4, '0')}
                    </span>
                  </div>
                  <div className="hr-request-desc">
                    <span
                      style={{
                        fontSize: '9.5px',
                        fontWeight: 600,
                        color: '#2563eb',
                        background: '#eff6ff',
                        padding: '1px 5px',
                        borderRadius: '3px',
                      }}
                    >
                      {item.categoryLabel}
                    </span>
                    <span>&bull;</span>
                    <span>{item.requestType}</span>
                  </div>
                </div>
              </div>

              {/* Middle Column: Details & Evidence */}
              <div className="hr-request-card-mid" style={{ flex: 1, minWidth: '220px' }}>
                <span style={{ fontSize: '11px', color: '#334155' }}>{item.details}</span>
                <div className="hr-request-date" style={{ marginTop: '2px' }}>
                  <Clock size={11} color="#94a3b8" />
                  <span>Submitted: {item.submittedAt}</span>
                  <span style={{ margin: '0 4px', color: '#cbd5e1' }}>&bull;</span>
                  <span style={{ color: '#475569' }}>
                    Assigned: <strong>{item.approverRole}</strong>
                  </span>
                </div>
              </div>

              {/* Right Column: Status & Action Buttons */}
              <div className="hr-request-card-right">
                <span className="hr-status-pill hr-status-pill--pending">
                  <Clock size={11} />
                  <span>{item.statusText}</span>
                </span>

                {/* HR Read-Only Details Button */}
                <button
                  type="button"
                  className="hr-request-action-btn hr-request-action-btn--outline"
                  onClick={() => setSelectedItem(item)}
                  title="View full request details"
                >
                  <Eye size={12} />
                  <span>View Details</span>
                </button>

                {/* Direct Link to Employee Profile */}
                <Link
                  to={`${basePath}/employees/${item.employeeId}`}
                  className="hr-request-action-btn hr-request-action-btn--outline"
                  title="Open employee profile"
                >
                  <UserRound size={12} />
                  <span>Profile</span>
                </Link>

                {/* Roles & Access Setup button (Admin or reviewer) */}
                {item.category === 'onboarding' &&
                  access.can('hr.employees.submissions.review', 'access.accounts.read') && (
                    <Button
                      variant="secondary"
                      style={{
                        height: '24px',
                        padding: '0 8px',
                        fontSize: '10px',
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '4px',
                      }}
                      onClick={() =>
                        setAccessEmployee({
                          id: item.employeeId,
                          name: item.employeeName,
                          number: item.employeeNumber,
                        })
                      }
                      title="Configure roles and access credentials for this employee"
                    >
                      <KeyRound size={11} />
                      <span>Setup Access</span>
                    </Button>
                  )}

                {/* Admin Decision Button (Only shown if user has admin review rights) */}
                {isAdminReviewer && item.submissionRef && (
                  <Button
                    variant="primary"
                    style={{ height: '24px', padding: '0 8px', fontSize: '10px' }}
                    onClick={() => {
                      setAdminSubmission(item.submissionRef!);
                    }}
                  >
                    <span>Admin Review</span>
                  </Button>
                )}
              </div>
            </article>
          ))
        )}
      </div>

      {/* 5. HR Read-Only Details Dialog */}
      <Dialog
        open={!!selectedItem}
        onOpenChange={(open) => {
          if (!open) setSelectedItem(null);
        }}
        title="Pending Approval Details"
        description={
          selectedItem ? `${selectedItem.employeeName} • #${selectedItem.employeeNumber}` : ''
        }
      >
        {selectedItem && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', padding: '8px 0' }}>
            {/* Banner Notice */}
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '10px',
                background: '#f8fafc',
                border: '1px solid #e2e8f0',
                borderRadius: '6px',
                padding: '10px 14px',
                fontSize: '12.5px',
                color: '#475569',
              }}
            >
              <Shield size={16} color="#2563eb" style={{ flexShrink: 0 }} />
              <div>
                <strong>HR Notice:</strong> As HR Desk, this view is read-only. Final authorization
                decision is handled by <strong>{selectedItem.approverRole}</strong>.
              </div>
            </div>

            {/* Employee Profile Information */}
            <div
              style={{
                background: '#ffffff',
                border: '1px solid #e2e8f0',
                borderRadius: '8px',
                padding: '14px 16px',
              }}
            >
              <div
                style={{
                  fontSize: '11px',
                  fontWeight: 700,
                  color: '#64748b',
                  textTransform: 'uppercase',
                  marginBottom: '8px',
                }}
              >
                Employee Information
              </div>
              <div
                style={{
                  display: 'grid',
                  gridTemplateColumns: 'repeat(2, 1fr)',
                  gap: '10px',
                  fontSize: '13px',
                }}
              >
                <div>
                  <span style={{ color: '#64748b' }}>Full Name:</span>{' '}
                  <strong>{selectedItem.employeeName}</strong>
                </div>
                <div>
                  <span style={{ color: '#64748b' }}>Employee Code:</span>{' '}
                  <strong>#{selectedItem.employeeNumber}</strong>
                </div>
                <div>
                  <span style={{ color: '#64748b' }}>National ID (CNIC):</span>{' '}
                  <span>{selectedItem.identityNumber || 'Pending verification'}</span>
                </div>
                <div>
                  <span style={{ color: '#64748b' }}>Contact Phone:</span>{' '}
                  <span>{selectedItem.phone || 'Not provided'}</span>
                </div>
              </div>
            </div>

            {/* Request & Approval Workflow Details */}
            <div
              style={{
                background: '#ffffff',
                border: '1px solid #e2e8f0',
                borderRadius: '8px',
                padding: '14px 16px',
              }}
            >
              <div
                style={{
                  fontSize: '11px',
                  fontWeight: 700,
                  color: '#64748b',
                  textTransform: 'uppercase',
                  marginBottom: '8px',
                }}
              >
                Approval Workflow Summary
              </div>
              <div
                style={{ display: 'flex', flexDirection: 'column', gap: '8px', fontSize: '13px' }}
              >
                <div>
                  <span style={{ color: '#64748b' }}>Request Type:</span>{' '}
                  <strong>{selectedItem.requestType}</strong>
                </div>
                <div>
                  <span style={{ color: '#64748b' }}>Current Status:</span>{' '}
                  <span
                    className="hr-status-pill hr-status-pill--pending"
                    style={{ marginLeft: '4px' }}
                  >
                    {selectedItem.statusText}
                  </span>
                </div>
                <div>
                  <span style={{ color: '#64748b' }}>Submission Date:</span>{' '}
                  <span>{selectedItem.submittedAt}</span>
                </div>
                <div>
                  <span style={{ color: '#64748b' }}>Assigned Approver:</span>{' '}
                  <strong>{selectedItem.approverRole}</strong>
                </div>
                <div
                  style={{
                    marginTop: '4px',
                    padding: '8px',
                    background: '#f8fafc',
                    borderRadius: '4px',
                    color: '#475569',
                    fontSize: '12px',
                  }}
                >
                  {selectedItem.details}
                </div>
              </div>
            </div>

            {/* Actions */}
            <div
              style={{
                display: 'flex',
                justifyContent: 'flex-end',
                gap: '10px',
                marginTop: '8px',
                flexWrap: 'wrap',
              }}
            >
              <Button variant="secondary" onClick={() => setSelectedItem(null)}>
                Close
              </Button>
              {selectedItem.category === 'onboarding' &&
                access.can('hr.employees.submissions.review', 'access.accounts.read') && (
                  <Button
                    variant="primary"
                    style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}
                    onClick={() => {
                      const emp = {
                        id: selectedItem.employeeId,
                        name: selectedItem.employeeName,
                        number: selectedItem.employeeNumber,
                      };
                      setSelectedItem(null);
                      setAccessEmployee(emp);
                    }}
                  >
                    <KeyRound size={14} />
                    <span>Configure Roles &amp; Access</span>
                  </Button>
                )}
              <Link
                to={`${basePath}/employees/${selectedItem.employeeId}`}
                className="button button--secondary"
                style={{
                  textDecoration: 'none',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '6px',
                }}
                onClick={() => setSelectedItem(null)}
              >
                <ArrowUpRight size={14} />
                <span>Open Employee Profile</span>
              </Link>
            </div>
          </div>
        )}
      </Dialog>

      {/* 6. Admin Decision Dialog (Only for Admin Reviewer) */}
      {isAdminReviewer && (
        <Dialog
          open={!!adminSubmission}
          onOpenChange={(open) => {
            if (
              !open &&
              !reviewBusy &&
              (!reviewDirty || window.confirm('Discard unsaved hiring decision?'))
            ) {
              setAdminSubmission(null);
              setReviewDirty(false);
            }
          }}
          title="Hiring Decision Review"
          description={
            adminSubmission
              ? `${adminSubmission.name ?? 'Employee'} · #${adminSubmission.employeeNumber}`
              : ''
          }
        >
          {adminSubmission && (
            <HiringSubmissionReview
              key={adminSubmission.id}
              submission={adminSubmission}
              onDirty={setReviewDirty}
              onBusy={setReviewBusy}
              onSaved={(decision) => {
                setReviewDirty(false);
                setAdminMessage(`Decision recorded: ${decision}.`);
                const approvedEmp = adminSubmission
                  ? {
                      id: adminSubmission.employeeId,
                      name: adminSubmission.name ?? 'Employee',
                      number: adminSubmission.employeeNumber,
                    }
                  : null;
                setAdminSubmission(null);
                void draftsQuery.refetch();
                void submissionsQuery.refetch();
                if (decision.toLowerCase().includes('approve') && approvedEmp) {
                  setAccessEmployee(approvedEmp);
                }
              }}
            />
          )}
        </Dialog>
      )}

      {/* 7. Roles & Access Setup Dialog */}
      <Dialog
        open={!!accessEmployee}
        onOpenChange={(open) => {
          if (!open) setAccessEmployee(null);
        }}
        title="Employee Roles &amp; System Access Setup"
        description={
          accessEmployee
            ? `${accessEmployee.name} • #${String(accessEmployee.number).padStart(4, '0')}`
            : ''
        }
      >
        {accessEmployee && (
          <div style={{ maxHeight: '78vh', overflowY: 'auto', padding: '4px' }}>
            <EmployeeAccessSetup
              employeeId={accessEmployee.id}
              approved
              onSaved={() => {
                void draftsQuery.refetch();
                if (isAdminReviewer) void submissionsQuery.refetch();
              }}
            />
          </div>
        )}
      </Dialog>
    </div>
  );
}
