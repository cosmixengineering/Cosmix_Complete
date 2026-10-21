import { useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import {
  ClipboardCheck,
  Briefcase,
  FileText,
  UserCheck,
  Banknote,
  ShieldCheck,
  CreditCard,
  HeartPulse,
  Layers,
  Clock,
  CheckCircle2,
  RotateCcw,
  XCircle,
  Search,
  ChevronRight,
} from 'lucide-react';
import { useHrAccess } from '../hooks/use-hr-access';
import { sections } from '../types/updates';
import { ChangeRequests } from '../components/requests/ChangeRequests';
import { WorkflowRequests } from '../components/requests/WorkflowRequests';
import { Denied } from '../components/Feedback';
import { ReferenceField } from '../components/ReferenceField';
import '@/styles/hr-requests.css';

export function HrRequestsPage() {
  const access = useHrAccess();
  const [params, setParams] = useSearchParams();
  const navigate = useNavigate();

  const tab = params.get('type') ?? 'employment';
  const state = params.get('state') ?? '';
  const [searchTerm, setSearchTerm] = useState('');

  const tabs = [
    ...sections
      .filter((s) => s.name !== 'basic' && access.can(...s.read))
      .map((s) => ({ key: s.name, label: s.label })),
    ...(access.can('hr.employees.compensation.read')
      ? [{ key: 'compensation', label: 'Salary' }]
      : []),
    ...(access.can('hr.employees.lifecycle.read')
      ? [{ key: 'lifecycle', label: 'Employment status' }]
      : []),
  ];

  if (!access.can('hr.employees.read')) return <Denied />;

  const section = sections.find((s) => s.name === tab && s.name !== 'basic');

  const getTabIcon = (key: string) => {
    switch (key) {
      case 'employment':
        return <Briefcase size={15} />;
      case 'documents':
        return <FileText size={15} />;
      case 'lifecycle':
        return <UserCheck size={15} />;
      case 'compensation':
        return <Banknote size={15} />;
      case 'identity':
        return <ShieldCheck size={15} />;
      case 'bank':
        return <CreditCard size={15} />;
      case 'medical':
        return <HeartPulse size={15} />;
      default:
        return <FileText size={15} />;
    }
  };

  const handleStateFilter = (newState: string) => {
    const next = new URLSearchParams(params);
    if (newState === '') {
      next.delete('state');
    } else {
      next.set('state', newState);
    }
    setParams(next);
  };

  return (
    <div className="hr-requests-shell">
      {/* Page Header */}
      <div className="hr-requests-header">
        <div className="hr-requests-breadcrumb">
          <Link to="/hr/dashboard">Dashboard</Link>
          <ChevronRight size={13} />
          <span>Human Resources</span>
          <ChevronRight size={13} />
          <span style={{ color: '#0f172a', fontWeight: 600 }}>Workflow Requests & Approvals</span>
        </div>

        <div className="hr-requests-header-main">
          <div className="hr-requests-title-group">
            <div className="hr-requests-icon-badge">
              <ClipboardCheck size={26} />
            </div>
            <div>
              <div className="hr-requests-tagline">HR OPERATIONS &bull; APPROVALS ENGINE</div>
              <h1 className="hr-requests-title">HR Requests & Approvals Queue</h1>
              <p className="hr-requests-subtitle">
                Review, audit and authorize employee record modifications, employment status
                transitions, document submissions, and compensation adjustments.
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* KPI Status Filter Cards Grid */}
      <div className="hr-requests-kpi-grid">
        <div
          className={`hr-requests-kpi-card ${state === '' ? 'is-active' : ''}`}
          onClick={() => handleStateFilter('')}
          title="Show all requests in current category"
        >
          <div className="hr-requests-kpi-icon hr-requests-kpi-icon--total">
            <Layers size={22} />
          </div>
          <div className="hr-requests-kpi-content">
            <span className="hr-requests-kpi-value">All</span>
            <span className="hr-requests-kpi-label">Total Queue</span>
          </div>
        </div>

        <div
          className={`hr-requests-kpi-card ${state === '0' ? 'is-active' : ''}`}
          onClick={() => handleStateFilter('0')}
          title="Filter pending requests requiring review"
        >
          <div className="hr-requests-kpi-icon">
            <Clock size={20} />
          </div>
          <div className="hr-requests-kpi-content">
            <span className="hr-requests-kpi-value">Pending</span>
            <span className="hr-requests-kpi-label">Needs Approval</span>
          </div>
        </div>

        <div
          className={`hr-requests-kpi-card ${state === '1' ? 'is-active' : ''}`}
          onClick={() => handleStateFilter('1')}
          title="Filter approved requests"
        >
          <div className="hr-requests-kpi-icon">
            <CheckCircle2 size={20} />
          </div>
          <div className="hr-requests-kpi-content">
            <span className="hr-requests-kpi-value">Approved</span>
            <span className="hr-requests-kpi-label">Active Records</span>
          </div>
        </div>

        <div
          className={`hr-requests-kpi-card ${state === '2' ? 'is-active' : ''}`}
          onClick={() => handleStateFilter('2')}
          title="Filter requests returned for corrections"
        >
          <div className="hr-requests-kpi-icon">
            <RotateCcw size={20} />
          </div>
          <div className="hr-requests-kpi-content">
            <span className="hr-requests-kpi-value">Returned</span>
            <span className="hr-requests-kpi-label">Needs Revision</span>
          </div>
        </div>

        <div
          className={`hr-requests-kpi-card ${state === '3' ? 'is-active' : ''}`}
          onClick={() => handleStateFilter('3')}
          title="Filter rejected requests"
        >
          <div className="hr-requests-kpi-icon">
            <XCircle size={20} />
          </div>
          <div className="hr-requests-kpi-content">
            <span className="hr-requests-kpi-value">Rejected</span>
            <span className="hr-requests-kpi-label">Declined</span>
          </div>
        </div>
      </div>

      {/* Segmented Navigation Tabs */}
      <nav className="hr-requests-tabs-bar" aria-label="Request categories">
        {tabs.map((t) => {
          const isActive = tab === t.key;
          return (
            <Link
              key={t.key}
              className={`hr-requests-tab-btn ${isActive ? 'is-active' : ''}`}
              to={`?type=${t.key}${state ? `&state=${state}` : ''}`}
            >
              {getTabIcon(t.key)}
              <span>{t.label}</span>
            </Link>
          );
        })}
      </nav>

      {/* Modern Filter & Search Bar */}
      <div className="hr-requests-filter-card">
        <div className="hr-requests-filter-row">
          <div className="hr-requests-search-box">
            <Search size={16} className="hr-requests-search-icon" />
            <input
              type="text"
              className="hr-requests-search-input"
              placeholder="Search by Request ID, employee name, or ID..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
            />
          </div>

          <div className="hr-requests-status-pills">
            <button
              type="button"
              className={`hr-requests-pill-btn ${state === '' ? 'is-active' : ''}`}
              onClick={() => handleStateFilter('')}
            >
              All States
            </button>
            <button
              type="button"
              className={`hr-requests-pill-btn ${state === '0' ? 'is-active' : ''}`}
              onClick={() => handleStateFilter('0')}
            >
              Pending
            </button>
            <button
              type="button"
              className={`hr-requests-pill-btn ${state === '1' ? 'is-active' : ''}`}
              onClick={() => handleStateFilter('1')}
            >
              Approved
            </button>
            <button
              type="button"
              className={`hr-requests-pill-btn ${state === '2' ? 'is-active' : ''}`}
              onClick={() => handleStateFilter('2')}
            >
              Returned
            </button>
            <button
              type="button"
              className={`hr-requests-pill-btn ${state === '3' ? 'is-active' : ''}`}
              onClick={() => handleStateFilter('3')}
            >
              Rejected
            </button>
          </div>

          <div className="hr-requests-emp-picker">
            <ReferenceField
              label="Jump to Employee"
              kind="employees"
              value={null}
              onChange={(v) => {
                if (v) void navigate(`/hr/employees/${v}?section=${encodeURIComponent(tab)}`);
              }}
            />
          </div>

          {(state || searchTerm) && (
            <button
              type="button"
              className="hr-requests-empty-btn"
              onClick={() => {
                handleStateFilter('');
                setSearchTerm('');
              }}
              title="Reset all filters"
            >
              <RotateCcw size={13} />
              Reset
            </button>
          )}
        </div>
      </div>

      {/* Main Request Queue Body */}
      {!tabs.some((t) => t.key === tab) ? (
        <Denied />
      ) : section ? (
        <ChangeRequests key={tab + state} section={section} state={state} search={searchTerm} />
      ) : (
        <WorkflowRequests
          key={tab + state}
          kind={tab === 'compensation' ? 'compensation' : 'lifecycle'}
          state={state}
          search={searchTerm}
        />
      )}
    </div>
  );
}
