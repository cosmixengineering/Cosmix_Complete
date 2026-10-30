import { EmployeeReference } from '@/components/employees/EmployeeReference';
import { formatEmployeeCode } from '@/utils/employee-code';
import { useState, useEffect, type ComponentProps } from 'react';
import { Link, useParams, useSearchParams, useLocation } from 'react-router-dom';
import {
  ArrowLeft,
  ArrowRight,
  Briefcase,
  Building2,
  ChevronRight,
  Contact,
  FileText,
  Fingerprint,
  HeartPulse,
  History,
  Home,
  Info,
  Landmark,
  LayoutGrid,
  Mail,
  MapPin,
  Navigation,
  Pencil,
  Phone,
  RefreshCw,
  Receipt,
  User,
  Users,
  ShieldCheck,
  AlertTriangle,
} from 'lucide-react';
import { z } from 'zod';
import { ApiError } from '@/services/api/api-error';
import { Button } from '@/components/ui/Button';
import { useHrAccess } from '../hooks/use-hr-access';
import { useHrQuery } from '../hooks/use-hr-queries';
import { id, pageOf, employeeSchema, type EmployeeSummary } from '../schemas/contracts';
import { masterSchema } from '../schemas/organization';
import {
  useEmployeeProfile,
  employeeProfileEvidenceMessage,
  type EmployeeProfileValues,
} from '../hooks/use-employee-profile';
import { currentUpdateSchema, basicSchema, identitySchema } from '../schemas/updates';
import { sections, type Section } from '../types/updates';
import { labelDate } from '../utils/messages';
import { formatCnic } from '../utils/identity';
import { Denied, QueryState } from '../components/Feedback';
import { ChangeFormModal } from '../components/employee/ChangeForm';
import { ChangeHistory } from '../components/employee/ChangeHistory';
import { ChangeRequests } from '../components/requests/ChangeRequests';
import { EmployeeDocuments } from '../components/documents/EmployeeDocuments';
import { SalaryWorkspace, StatusWorkspace } from '../components/requests/EmployeeWorkflows';
import { EmployeeAccessSetup } from '../components/approvals/EmployeeAccessSetup';
import { EmployeeSiteAssignments } from '../components/employee/EmployeeSiteAssignments';
import { EmployeeMapTracking } from '../components/employee/EmployeeMapTracking';
import { EmployeeProfileHeader } from '../components/employee/EmployeeProfileHeader';
import '@/styles/hr-dashboard.css';
import { EmployeeBiometricSection } from '../components/employee/EmployeeBiometricSection';

function calculateAge(dobStr?: string | null): string {
  if (!dobStr || dobStr === '0001-01-01') return '—';
  const birth = new Date(dobStr);
  if (isNaN(birth.getTime()) || birth > new Date()) return '—';
  const now = new Date();
  let age = now.getFullYear() - birth.getFullYear();
  const m = now.getMonth() - birth.getMonth();
  if (m < 0 || (m === 0 && now.getDate() < birth.getDate())) {
    age--;
  }
  return `${dobStr} (${age} yrs)`;
}

export function EmployeeDetailPage() {
  const { employeeId = '' } = useParams();
  return id.safeParse(employeeId).success ? (
    <EmployeeWorkspace key={employeeId} employeeId={employeeId} />
  ) : (
    <p role="alert">Invalid employee reference.</p>
  );
}

function EmployeeWorkspace({ employeeId }: { employeeId: string }) {
  const access = useHrAccess();
  const location = useLocation();
  const basePath = location.pathname.startsWith('/admin') ? '/admin' : '/hr';
  const [params] = useSearchParams();
  const rawName = params.get('section');
  const name = rawName ?? 'overview';
  const section = sections.find((s) => s.name === name);

  const canReadProfile = access.can('hr.employees.read');
  const details = useEmployeeProfile(employeeId);

  const deptsQuery = useHrQuery('departments?pageSize=100', pageOf(masterSchema), [
    'hr.organization.read',
  ]);
  const desigsQuery = useHrQuery('designations?pageSize=100', pageOf(masterSchema), [
    'hr.organization.read',
  ]);
  const branchesQuery = useHrQuery('branches?pageSize=100', pageOf(masterSchema), [
    'hr.organization.read',
  ]);

  if (!canReadProfile) return <Denied />;

  const isOverview = !rawName || rawName === 'overview';
  const { profile, employee } = details;

  return (
    <div className="emp-page-container">
      {/* Top Breadcrumb Link */}
      <Link className="emp-back-link" to={`${basePath}/employees`}>
        <ArrowLeft size={13} />
        Back to employees
      </Link>

      {/* Main Profile Header Card */}
      <ProfileQueryState query={details.query} skeleton="card">
        {details.query.isSuccess && (
          <EmployeeProfileHeader
            employeeId={employeeId}
            employee={employee}
            profile={profile}
            approved={details.approved}
            directoryName={details.directoryName}
            designationName={
              desigsQuery.data?.items.find((item) => item.id === profile.designationId)?.name
            }
            onRefresh={details.refresh}
            refreshing={
              details.query.isFetching ||
              details.directory.isFetching ||
              details.identity.isFetching ||
              details.employment.isFetching
            }
          />
        )}
      </ProfileQueryState>
      {details.approved && (
        <>
          {access.can('hr.employees.identity.read') ? (
            <>
              <ProfileReadNotice label="Current identity" query={details.identity} />
              {details.identity.data?.evidenceIssue && (
                <p className="hr-message" role="status">
                  Saved identity details are shown below. The original identity document evidence is
                  unavailable for this record. Upload a replacement identity document, have it
                  verified, then submit an explicit evidence repair request for independent Admin
                  review. Original history is retained.
                </p>
              )}
            </>
          ) : (
            <p className="muted">Identity details are restricted by your current permissions.</p>
          )}
          <ProfileReadNotice label="Current employment" query={details.employment} />
          {details.directory.isError && !profile.fullLegalName && (
            <ProfileReadNotice label="Directory name" query={details.directory} />
          )}
        </>
      )}
      {details.unapproved && details.draft.isSuccess && (
        <section className="hr-notice">
          <h2>Employee onboarding</h2>
          <p>
            This employee has no approved current record. Continue in the hiring workspace to view
            or edit the draft.
          </p>
          <Link to={`${basePath}/employees/${employeeId}/hiring`}>Open hiring record</Link>
        </section>
      )}
      {details.approved && (
        <>
          {/* Modern Tabs Bar */}
          <nav className="emp-nav-tabs" aria-label="Employee navigation">
            <Link to="?section=overview" className={`emp-nav-tab ${isOverview ? 'active' : ''}`}>
              <User size={13} />
              Overview
            </Link>
            <Link
              to="?section=basic"
              className={`emp-nav-tab ${name === 'basic' || name === 'identity' ? 'active' : ''}`}
            >
              <FileText size={13} />
              Personal details
            </Link>
            <Link
              to="?section=employment"
              className={`emp-nav-tab ${name === 'employment' ? 'active' : ''}`}
            >
              <Briefcase size={13} />
              Employment
            </Link>
            <Link to="?section=bank" className={`emp-nav-tab ${name === 'bank' ? 'active' : ''}`}>
              <Landmark size={13} />
              Bank details
            </Link>
            {access.can('hr.employees.compensation.read') && (
              <Link
                to="?section=compensation"
                className={`emp-nav-tab ${name === 'compensation' ? 'active' : ''}`}
              >
                <Receipt size={13} />
                Salary
              </Link>
            )}
            <Link
              to="?section=files"
              className={`emp-nav-tab ${name === 'files' || name === 'medical-files' ? 'active' : ''}`}
            >
              <FileText size={13} />
              Documents
            </Link>
            <Link
              to="?section=assignments"
              className={`emp-nav-tab ${name === 'assignments' || name === 'tracking' || name === 'biometrics' ? 'active' : ''}`}
            >
              <MapPin size={13} />
              Attendance &amp; sites
            </Link>
            {access.can('hr.employees.submissions.review', 'access.accounts.read') && (
              <Link
                to="?section=access"
                className={`emp-nav-tab ${name === 'access' ? 'active' : ''}`}
              >
                <ShieldCheck size={13} />
                Roles &amp; Access
              </Link>
            )}
          </nav>

          {/* Workspace Area */}
          {isOverview ? (
            <EmployeeOverview profile={profile} asOf={details.basic.data?.asOf} />
          ) : (
            <div className="emp-workspace-layout">
              <main className="emp-workspace-main">
                {name === 'access' ? (
                  access.can('hr.employees.submissions.review', 'access.accounts.read') ? (
                    <CurrentAccess key={employeeId} employeeId={employeeId} />
                  ) : (
                    <Denied />
                  )
                ) : name === 'assignments' ? (
                  access.can('hr.sites.read') ? (
                    <EmployeeSiteAssignments key={employeeId} employeeId={employeeId} />
                  ) : (
                    <Denied />
                  )
                ) : name === 'tracking' ? (
                  <EmployeeMapTracking key={employeeId} employeeId={employeeId} />
                ) : name === 'biometrics' ? (
                  <EmployeeBiometricSection key={employeeId} employeeId={employeeId} />
                ) : section ? (
                  access.can(...section.read) ? (
                    <CurrentSection
                      key={section.name}
                      employeeId={employeeId}
                      section={section}
                      edit={params.get('edit') === '1'}
                      profile={profile}
                      currentQuery={
                        section.name === 'basic'
                          ? details.basic
                          : section.name === 'identity'
                            ? details.identity
                            : section.name === 'employment'
                              ? details.employment
                              : undefined
                      }
                      onSaved={details.refresh}
                    />
                  ) : (
                    <Denied />
                  )
                ) : name === 'compensation' ? (
                  <SalaryWorkspace
                    key={name}
                    employeeId={employeeId}
                    edit={params.get('edit') === '1'}
                    employeeName={employee?.name}
                    employeeNumber={employee?.employeeNumber}
                  />
                ) : name === 'lifecycle' ? (
                  <StatusWorkspace
                    key={name}
                    employeeId={employeeId}
                    edit={params.get('edit') === '1'}
                  />
                ) : name === 'files' || name === 'medical-files' ? (
                  <CurrentFiles
                    key={name}
                    employeeId={employeeId}
                    medical={name === 'medical-files'}
                  />
                ) : (
                  <p role="alert">Employee section not found.</p>
                )}
              </main>

              {/* Right Quick Section Rail */}
              <aside className="emp-quick-rail">
                <EmployeeQuickRail
                  currentSection={name}
                  employee={employee}
                  profile={profile}
                  departments={deptsQuery.data?.items}
                  designations={desigsQuery.data?.items}
                  branches={branchesQuery.data?.items}
                  canCompensation={access.can('hr.employees.compensation.read')}
                  canAccess={access.can('hr.employees.submissions.review', 'access.accounts.read')}
                />
              </aside>
            </div>
          )}
        </>
      )}
    </div>
  );
}

function ProfileQueryState(props: ComponentProps<typeof QueryState>) {
  const message = props.query.isError
    ? employeeProfileEvidenceMessage(props.query.error)
    : undefined;
  if (!message) return <QueryState {...props} />;
  return (
    <div className="hr-notice" role="alert">
      <p>{message}</p>
      <Button variant="secondary" onClick={() => void props.query.refetch()}>
        Check again
      </Button>
    </div>
  );
}

function ProfileReadNotice({
  label,
  query,
}: {
  label: string;
  query: { isPending: boolean; isError: boolean; error: unknown; refetch: () => unknown };
}) {
  if (query.isPending) return null;
  if (!query.isError) return null;

  const code = query.error instanceof ApiError ? query.error.code : undefined;
  const correlationId = query.error instanceof ApiError ? query.error.correlationId : undefined;

  return (
    <div
      role="alert"
      style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        flexWrap: 'wrap',
        gap: '8px',
        padding: '6px 12px',
        background: '#fffbeb',
        border: '1px solid #fef3c7',
        borderLeft: '3px solid #d97706',
        borderRadius: '4px',
        margin: '4px 0 10px 0',
        fontSize: '11px',
        color: '#92400e',
      }}
    >
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: '6px',
          flex: 1,
          minWidth: '220px',
        }}
      >
        <AlertTriangle size={13} style={{ color: '#d97706', flexShrink: 0 }} />
        <span>
          <strong>{label} evidence notice:</strong> Record verification pending{' '}
          {code && (
            <code
              style={{
                fontSize: '10px',
                background: 'rgba(217,119,6,0.1)',
                padding: '1px 4px',
                borderRadius: '3px',
              }}
            >
              {code}
            </code>
          )}
          {correlationId && <span style={{ opacity: 0.8 }}> &middot; Ref: {correlationId}</span>}
        </span>
      </div>
      <Button
        variant="secondary"
        onClick={() => void query.refetch()}
        style={{
          height: '22px',
          fontSize: '10.5px',
          padding: '0 8px',
          borderColor: '#fcd34d',
          background: '#ffffff',
          color: '#92400e',
        }}
      >
        Check again
      </Button>
    </div>
  );
}

function EmployeeQuickRail({
  currentSection,
  employee,
  profile,
  departments,
  designations,
  branches,
  canCompensation,
  canAccess,
}: {
  currentSection: string;
  employee?: EmployeeSummary;
  profile?: EmployeeProfileValues;
  departments?: { id: string; name: string }[];
  designations?: { id: string; name: string }[];
  branches?: { id: string; name: string }[];
  canCompensation: boolean;
  canAccess: boolean;
}) {
  const access = useHrAccess();
  const canIdentity = access.can('hr.employees.identity.read');
  const deptName =
    departments?.find((d) => d.id === profile?.departmentId)?.name || profile?.departmentId || '—';
  const desigName =
    designations?.find((d) => d.id === profile?.designationId)?.name ||
    profile?.designationId ||
    '—';
  const branchName =
    branches?.find((b) => b.id === profile?.branchId)?.name || profile?.branchId || '—';

  const navItems = [
    { key: 'overview', label: 'Overview', icon: User, path: '?section=overview' },
    { key: 'basic', label: 'Personal & Family', icon: FileText, path: '?section=basic' },
    { key: 'identity', label: 'CNIC & Identity', icon: Contact, path: '?section=identity' },
    { key: 'employment', label: 'Employment & Org', icon: Briefcase, path: '?section=employment' },
    { key: 'bank', label: 'Bank & Accounts', icon: Landmark, path: '?section=bank' },
    ...(canCompensation
      ? [
          {
            key: 'compensation',
            label: 'Salary & Compensation',
            icon: Receipt,
            path: '?section=compensation',
          },
        ]
      : []),
    { key: 'files', label: 'Documents & KYC', icon: FileText, path: '?section=files' },
    {
      key: 'assignments',
      label: 'Site Assignments',
      icon: Building2,
      path: '?section=assignments',
    },
    {
      key: 'biometrics',
      label: 'Biometrics & Face ID',
      icon: Fingerprint,
      path: '?section=biometrics',
    },
    { key: 'tracking', label: 'Live Map Tracking', icon: Navigation, path: '?section=tracking' },
    ...(canAccess
      ? [{ key: 'access', label: 'Roles & Access', icon: ShieldCheck, path: '?section=access' }]
      : []),
  ];

  return (
    <>
      {/* Navigator Card */}
      <div className="emp-rail-card">
        <div className="emp-rail-header">
          <span className="emp-rail-title">
            <LayoutGrid size={12} />
            Quick Sections
          </span>
          <span className="emp-rail-badge">{navItems.length}</span>
        </div>
        <div className="emp-rail-nav-list">
          {navItems.map((item) => {
            const Icon = item.icon;
            const isActive =
              currentSection === item.key ||
              (item.key === 'overview' && (!currentSection || currentSection === 'overview')) ||
              (item.key === 'basic' && currentSection === 'identity') ||
              (item.key === 'files' && currentSection === 'medical-files');
            return (
              <Link
                key={item.key}
                to={item.path}
                className={`emp-rail-nav-btn ${isActive ? 'active' : ''}`}
                title={`Switch to ${item.label}`}
              >
                <span className="emp-rail-nav-left">
                  <Icon size={13} className="emp-rail-nav-icon" />
                  <span>{item.label}</span>
                </span>
                <ChevronRight size={13} className="emp-rail-nav-chevron" />
              </Link>
            );
          })}
        </div>
      </div>

      {/* Quick Actions & Verification Shortcuts */}
      <div className="emp-rail-card">
        <div className="emp-rail-header">
          <span className="emp-rail-title">
            <LayoutGrid size={12} />
            Quick Actions &amp; Records
          </span>
          <span className="emp-rail-badge">4</span>
        </div>
        <div className="emp-rail-actions-stack">
          <Link
            to="?section=assignments"
            className={`emp-action-card ${currentSection === 'assignments' ? 'active' : ''}`}
            title="View Site Assignments & Shifts"
          >
            <div className="emp-action-card-left">
              <Building2 size={13} className="emp-action-card-icon" />
              <span>Site Assignments &amp; Shifts</span>
            </div>
            <ArrowRight size={12} className="emp-action-card-arrow" />
          </Link>
          <Link
            to="?section=biometrics"
            className={`emp-action-card ${currentSection === 'biometrics' ? 'active' : ''}`}
            title="Manage Biometrics & Face ID"
          >
            <div className="emp-action-card-left">
              <Fingerprint size={13} className="emp-action-card-icon" />
              <span>Biometrics &amp; Face ID</span>
            </div>
            <ArrowRight size={12} className="emp-action-card-arrow" />
          </Link>
          <Link
            to="?section=files"
            className={`emp-action-card ${currentSection === 'files' ? 'active' : ''}`}
            title="View Uploaded Documents & KYC"
          >
            <div className="emp-action-card-left">
              <FileText size={13} className="emp-action-card-icon" />
              <span>Uploaded Documents &amp; KYC</span>
            </div>
            <ArrowRight size={12} className="emp-action-card-arrow" />
          </Link>
          <Link
            to="?section=medical-files"
            className={`emp-action-card ${currentSection === 'medical-files' ? 'active' : ''}`}
            title="View Medical Records & Fitness"
          >
            <div className="emp-action-card-left">
              <HeartPulse size={13} className="emp-action-card-icon red" />
              <span>Medical Records &amp; Fitness</span>
            </div>
            <ArrowRight size={12} className="emp-action-card-arrow" />
          </Link>
        </div>
      </div>

      {/* Profile Snapshot Card */}
      <div className="emp-rail-card">
        <div className="emp-rail-header">
          <span className="emp-rail-title">
            <Info size={12} />
            Current profile
          </span>
          {employee?.employeeNumber && (
            <span className="emp-rail-badge">{formatEmployeeCode(employee.employeeNumber)}</span>
          )}
        </div>
        <div className="emp-rail-snapshot-box">
          <p className="muted">
            Values from authorized current records. Unavailable fields are shown as —.
          </p>
          <div className="emp-rail-snapshot-row">
            <span className="emp-rail-snapshot-lbl">Designation</span>
            <span className="emp-rail-snapshot-val" title={desigName}>
              {desigName}
            </span>
          </div>
          <div className="emp-rail-snapshot-row">
            <span className="emp-rail-snapshot-lbl">Department</span>
            <span className="emp-rail-snapshot-val" title={deptName}>
              {deptName}
            </span>
          </div>
          {canIdentity && (
            <div className="emp-rail-snapshot-row">
              <span className="emp-rail-snapshot-lbl">CNIC</span>
              <span className="emp-rail-snapshot-val">
                {profile?.identityDocumentNumber ? formatCnic(profile.identityDocumentNumber) : '—'}
              </span>
            </div>
          )}
          {canIdentity && (
            <div className="emp-rail-snapshot-row">
              <span className="emp-rail-snapshot-lbl">Mobile</span>
              <span className="emp-rail-snapshot-val">
                {profile?.personalPhone || profile?.loginPhoneNumber || '—'}
              </span>
            </div>
          )}
          {canIdentity && (
            <div className="emp-rail-snapshot-row">
              <span className="emp-rail-snapshot-lbl">Work Email</span>
              <span className="emp-rail-snapshot-val" title={profile?.officialEmail || ''}>
                {profile?.officialEmail || '—'}
              </span>
            </div>
          )}
          <div className="emp-rail-snapshot-row">
            <span className="emp-rail-snapshot-lbl">Office</span>
            <span className="emp-rail-snapshot-val" title={branchName}>
              {branchName}
            </span>
          </div>

          <Link to="?section=basic&edit=1" className="emp-rail-action-btn">
            <Pencil size={11} />
            Edit Profile Details
          </Link>
        </div>
      </div>
    </>
  );
}

function EmployeeOverview({ profile, asOf }: { profile: EmployeeProfileValues; asOf?: string }) {
  const currentAddress = profile?.currentAddress;
  const currentAddressStr = currentAddress?.line1
    ? `${currentAddress.line1}${currentAddress.city ? `, ${currentAddress.city}` : ''}${currentAddress.province ? `, ${currentAddress.province}` : ''}`
    : null;

  const permanentAddress = profile?.permanentAddress;
  const permanentAddressStr = permanentAddress?.line1
    ? `${permanentAddress.line1}${permanentAddress.city ? `, ${permanentAddress.city}` : ''}${permanentAddress.province ? `, ${permanentAddress.province}` : ''}`
    : null;

  const emergency = profile?.emergencyContact;

  return (
    <div className="emp-overview-container">
      <div className="emp-overview-timestamp">Current basic details as of {asOf ?? '—'}.</div>

      <div className="emp-overview-grid">
        {/* Left Column: Personal Information Card */}
        <div className="emp-overview-left">
          <div className="emp-card">
            {/* Header */}
            <div className="emp-card-header">
              <div className="emp-card-title">
                <User size={15} className="emp-card-title-icon" />
                <span>Personal information</span>
              </div>
              <Link to="?section=basic&edit=1" className="emp-card-edit-link">
                <Pencil size={11} />
                Edit
              </Link>
            </div>

            {/* Section: Addresses */}
            <div className="emp-section-heading">Addresses</div>
            <div className="emp-two-col-grid">
              {/* Current address */}
              <div className="emp-info-block">
                <div className="emp-block-title">
                  <MapPin size={13} className="emp-block-icon" />
                  <span>Current address</span>
                </div>
                <div className="emp-block-value">{currentAddressStr || 'Address not added'}</div>
                <Link to="?section=basic&edit=1" className="emp-block-action">
                  + Add address
                </Link>
              </div>

              {/* Permanent address */}
              <div className="emp-info-block">
                <div className="emp-block-title">
                  <Home size={13} className="emp-block-icon" />
                  <span>Permanent address</span>
                </div>
                <div className="emp-block-value">{permanentAddressStr || 'Address not added'}</div>
                <Link to="?section=basic&edit=1" className="emp-block-action">
                  + Add address
                </Link>
              </div>
            </div>

            {/* Section: Contact details */}
            <div className="emp-section-heading">Contact details</div>
            <div className="emp-two-col-grid">
              {/* Personal email */}
              <div className="emp-info-block">
                <div className="emp-block-title">
                  <Mail size={13} className="emp-block-icon" />
                  <span>Personal email</span>
                </div>
                <div className="emp-block-value">{profile?.personalEmail || 'Not added'}</div>
                <Link to="?section=basic&edit=1" className="emp-block-action">
                  + Add email
                </Link>
              </div>

              {/* Emergency contact */}
              <div className="emp-info-block">
                <div className="emp-block-title">
                  <Users size={13} className="emp-block-icon" />
                  <span>Emergency contact</span>
                </div>
                <div className="emp-emergency-table">
                  <div className="emp-emergency-row">
                    <span className="emp-emergency-label">Name</span>
                    <span className="emp-emergency-val">{emergency?.name || 'Not added'}</span>
                  </div>
                  <div className="emp-emergency-row">
                    <span className="emp-emergency-label">Relationship</span>
                    <span className="emp-emergency-val">
                      {emergency?.relationship || 'Not added'}
                    </span>
                  </div>
                </div>
                <Link to="?section=basic&edit=1" className="emp-block-action">
                  <Pencil size={11} />
                  Edit contact
                </Link>
              </div>
            </div>

            {/* Section: More personal details */}
            <div className="emp-more-heading">More personal details</div>
            <div className="emp-action-cards-grid">
              <Link to="?section=identity" className="emp-action-card">
                <div className="emp-action-card-left">
                  <Contact size={14} className="emp-action-card-icon" />
                  <span>Identity details</span>
                </div>
                <ArrowRight size={13} className="emp-action-card-arrow" />
              </Link>

              <Link to="?section=medical-files" className="emp-action-card">
                <div className="emp-action-card-left">
                  <HeartPulse size={14} className="emp-action-card-icon red" />
                  <span>Medical information</span>
                </div>
                <ArrowRight size={13} className="emp-action-card-arrow" />
              </Link>
            </div>
          </div>
        </div>

        {/* Right Column: Work & attendance + Profile records (THE SIDE MENU) */}
        <div className="emp-overview-right">
          {/* Card 1: Work & attendance */}
          <div className="emp-card">
            <div className="emp-card-header simple">
              <div className="emp-card-title">
                <MapPin size={14} className="emp-card-title-icon" />
                <span>Work &amp; attendance</span>
              </div>
            </div>

            <div className="emp-menu-list">
              <Link to="?section=assignments" className="emp-menu-item">
                <div className="emp-menu-item-left">
                  <Building2 size={15} className="emp-menu-icon" />
                  <div className="emp-menu-texts">
                    <div className="emp-menu-title">Site assignments</div>
                    <div className="emp-menu-subtitle">Assigned work locations</div>
                  </div>
                </div>
                <ChevronRight size={14} className="emp-menu-chevron" />
              </Link>

              <Link to="?section=biometrics" className="emp-menu-item">
                <div className="emp-menu-item-left">
                  <Fingerprint size={15} className="emp-menu-icon" />
                  <div className="emp-menu-texts">
                    <div className="emp-menu-title">Biometrics &amp; Face ID</div>
                    <div className="emp-menu-subtitle">Enrollment and verification</div>
                  </div>
                </div>
                <ChevronRight size={14} className="emp-menu-chevron" />
              </Link>

              <Link to="?section=tracking" className="emp-menu-item">
                <div className="emp-menu-item-left">
                  <Navigation size={15} className="emp-menu-icon" />
                  <div className="emp-menu-texts">
                    <div className="emp-menu-title">Live map tracking</div>
                    <div className="emp-menu-subtitle">Employee location</div>
                  </div>
                </div>
                <ChevronRight size={14} className="emp-menu-chevron" />
              </Link>
            </div>
          </div>

          {/* Card 2: Profile records */}
          <div className="emp-card">
            <div className="emp-card-header simple">
              <div className="emp-card-title">
                <FileText size={14} className="emp-card-title-icon" />
                <span>Profile records</span>
              </div>
            </div>

            <div className="emp-menu-list">
              <Link to="?section=employment" className="emp-menu-item">
                <div className="emp-menu-item-left">
                  <Briefcase size={15} className="emp-menu-icon" />
                  <div className="emp-menu-texts">
                    <div className="emp-menu-title">Employment</div>
                    <div className="emp-menu-subtitle">Work and employment details</div>
                  </div>
                </div>
                <ChevronRight size={14} className="emp-menu-chevron" />
              </Link>

              <Link to="?section=bank" className="emp-menu-item">
                <div className="emp-menu-item-left">
                  <Landmark size={15} className="emp-menu-icon" />
                  <div className="emp-menu-texts">
                    <div className="emp-menu-title">Bank details</div>
                    <div className="emp-menu-subtitle">Employee bank information</div>
                  </div>
                </div>
                <ChevronRight size={14} className="emp-menu-chevron" />
              </Link>

              <Link to="?section=files" className="emp-menu-item">
                <div className="emp-menu-item-left">
                  <FileText size={15} className="emp-menu-icon" />
                  <div className="emp-menu-texts">
                    <div className="emp-menu-title">Documents</div>
                    <div className="emp-menu-subtitle">Records and uploaded files</div>
                  </div>
                </div>
                <ChevronRight size={14} className="emp-menu-chevron" />
              </Link>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

function CurrentAccess({ employeeId }: { employeeId: string }) {
  const query = useHrQuery(`employees/${employeeId}/current/basic`, currentUpdateSchema, [
    'hr.employees.read',
  ]);
  return (
    <ProfileQueryState query={query}>
      {query.data && <EmployeeAccessSetup employeeId={employeeId} approved />}
    </ProfileQueryState>
  );
}

function StructuredSectionDisplay({
  section,
  data,
  canWrite,
  onEdit,
  departments,
  designations,
  branches,
  staffCategories,
  employmentTypes,
  employees,
  profile,
}: {
  section: Section;
  data: Record<string, unknown> | null | undefined;
  canWrite: boolean;
  onEdit: () => void;
  departments?: { id: string; name: string; code?: string }[];
  designations?: { id: string; name: string; code?: string }[];
  branches?: { id: string; name: string; code?: string }[];
  staffCategories?: { id: string; name: string; code?: string }[];
  employmentTypes?: { id: string; name: string; code?: string }[];
  employees?: EmployeeSummary[];
  profile?: EmployeeProfileValues;
}) {
  const access = useHrAccess();
  const canIdentity = access.can('hr.employees.identity.read');
  if (!data && !profile) {
    return (
      <div className="emp-card">
        <p className="muted" style={{ margin: 0, fontSize: '11.5px' }}>
          No records found for {section.label}.
        </p>
      </div>
    );
  }

  if (section.name === 'basic') {
    const basic = data as Partial<z.infer<typeof basicSchema>>;
    const currentAddr = basic?.currentAddress || profile?.currentAddress;
    const permAddr = basic?.permanentAddress || profile?.permanentAddress;
    const emergency = basic?.emergencyContact || profile?.emergencyContact;

    const renderAddr = (addr: typeof currentAddr) => {
      if (!addr || (!addr.line1 && !addr.city && !addr.province)) {
        return <span className="emp-empty-badge">Not provided</span>;
      }
      return (
        <div style={{ lineHeight: '1.35' }}>
          {addr.line1 && <div style={{ fontWeight: 600, color: '#0f172a' }}>{addr.line1}</div>}
          {addr.line2 && <div style={{ color: '#475569', fontSize: '11px' }}>{addr.line2}</div>}
          <div style={{ color: '#64748b', fontSize: '11px', marginTop: '1px' }}>
            {[addr.city, addr.province, addr.postalCode].filter(Boolean).join(', ')}
          </div>
        </div>
      );
    };

    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
        {/* Card 1: Personal & Family Information */}
        <div className="emp-card">
          <div className="emp-card-header">
            <div className="emp-card-title">
              <User size={14} className="emp-card-title-icon" />
              <span>Personal &amp; Family Information</span>
            </div>
            {canWrite && (
              <button type="button" className="emp-card-edit-btn" onClick={onEdit}>
                <Pencil size={11} />
                Edit details
              </button>
            )}
          </div>

          <div className="emp-kv-grid">
            {canIdentity && (
              <div className="emp-kv-item">
                <span className="emp-kv-label">Full Legal Name</span>
                <span className="emp-kv-value">{profile?.fullLegalName || '—'}</span>
              </div>
            )}
            {canIdentity && (
              <div className="emp-kv-item">
                <span className="emp-kv-label">Father / Spouse Name</span>
                <span className="emp-kv-value">{profile?.fatherOrSpouseName || '—'}</span>
              </div>
            )}
            {canIdentity && (
              <div className="emp-kv-item">
                <span className="emp-kv-label">Date of Birth &amp; Age</span>
                <span className="emp-kv-value">{calculateAge(profile?.dateOfBirth)}</span>
              </div>
            )}
            {canIdentity && (
              <div className="emp-kv-item">
                <span className="emp-kv-label">Gender</span>
                <span className="emp-kv-value">{profile?.gender || '—'}</span>
              </div>
            )}
            <div className="emp-kv-item">
              <span className="emp-kv-label">Marital Status</span>
              <span className="emp-kv-value">
                {basic?.maritalStatus || profile?.maritalStatus || '—'}
              </span>
            </div>
            {canIdentity && (
              <div className="emp-kv-item">
                <span className="emp-kv-label">CNIC / ID Document</span>
                <span className="emp-kv-value">
                  {profile?.identityDocumentNumber
                    ? formatCnic(profile.identityDocumentNumber)
                    : '—'}
                </span>
              </div>
            )}
            {canIdentity && (
              <div className="emp-kv-item">
                <span className="emp-kv-label">Document Expiry</span>
                <span className="emp-kv-value">{profile?.identityDocumentExpiresOn || '—'}</span>
              </div>
            )}
          </div>
        </div>

        {/* Card 2: Contact Details & Emergency */}
        <div className="emp-card">
          <div className="emp-card-header simple">
            <div className="emp-card-title">
              <Phone size={14} className="emp-card-title-icon" />
              <span>Contact &amp; Emergency Details</span>
            </div>
          </div>

          <div className="emp-two-col-grid">
            {/* Communication channels */}
            <div className="emp-info-block">
              <div className="emp-block-title">
                <Mail size={13} className="emp-block-icon" />
                <span>Communication Channels</span>
              </div>
              <div className="emp-emergency-table" style={{ marginLeft: 0, marginTop: '4px' }}>
                {canIdentity && (
                  <div className="emp-emergency-row">
                    <span className="emp-emergency-label">Work Email</span>
                    <span className="emp-emergency-val">{profile?.officialEmail || '—'}</span>
                  </div>
                )}
                <div className="emp-emergency-row">
                  <span className="emp-emergency-label">Personal Email</span>
                  <span className="emp-emergency-val">
                    {basic?.personalEmail || profile?.personalEmail || '—'}
                  </span>
                </div>
                {canIdentity && (
                  <div className="emp-emergency-row">
                    <span className="emp-emergency-label">Personal Mobile</span>
                    <span className="emp-emergency-val">
                      {profile?.personalPhone || profile?.loginPhoneNumber || '—'}
                    </span>
                  </div>
                )}
                {canIdentity && (
                  <div className="emp-emergency-row">
                    <span className="emp-emergency-label">Company Phone</span>
                    <span className="emp-emergency-val">{profile?.companyPhone || '—'}</span>
                  </div>
                )}
              </div>
            </div>

            {/* Emergency contact */}
            <div className="emp-info-block">
              <div className="emp-block-title">
                <Users size={13} className="emp-block-icon" />
                <span>Emergency Contact Person</span>
              </div>
              <div className="emp-emergency-table" style={{ marginLeft: 0, marginTop: '4px' }}>
                <div className="emp-emergency-row">
                  <span className="emp-emergency-label">Name</span>
                  <span className="emp-emergency-val">{emergency?.name || 'Not provided'}</span>
                </div>
                <div className="emp-emergency-row">
                  <span className="emp-emergency-label">Relationship</span>
                  <span className="emp-emergency-val">
                    {emergency?.relationship || 'Not provided'}
                  </span>
                </div>
                <div className="emp-emergency-row">
                  <span className="emp-emergency-label">Primary Phone</span>
                  <span className="emp-emergency-val">{emergency?.phone || 'Not provided'}</span>
                </div>
                {emergency?.alternatePhone && (
                  <div className="emp-emergency-row">
                    <span className="emp-emergency-label">Alt Phone</span>
                    <span className="emp-emergency-val">{emergency.alternatePhone}</span>
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>

        {/* Card 3: Addresses */}
        <div className="emp-card">
          <div className="emp-card-header simple">
            <div className="emp-card-title">
              <MapPin size={14} className="emp-card-title-icon" />
              <span>Residential &amp; Permanent Addresses</span>
            </div>
          </div>

          <div className="emp-two-col-grid">
            <div className="emp-info-block">
              <div className="emp-block-title">
                <Home size={13} className="emp-block-icon" />
                <span>Current Residential Address</span>
              </div>
              <div className="emp-block-value" style={{ marginTop: '3px' }}>
                {renderAddr(currentAddr)}
              </div>
            </div>

            <div className="emp-info-block">
              <div className="emp-block-title">
                <Building2 size={13} className="emp-block-icon" />
                <span>Permanent Domicile Address</span>
              </div>
              <div className="emp-block-value" style={{ marginTop: '3px' }}>
                {renderAddr(permAddr)}
              </div>
            </div>
          </div>
        </div>
      </div>
    );
  }

  if (section.name === 'identity') {
    const identity = (data || profile || {}) as Partial<z.infer<typeof identitySchema>>;
    return (
      <div className="emp-card">
        <div className="emp-card-header">
          <div className="emp-card-title">
            <Contact size={14} className="emp-card-title-icon" />
            <span>Identity &amp; Legal Records</span>
          </div>
          {canWrite && (
            <button type="button" className="emp-card-edit-btn" onClick={onEdit}>
              <Pencil size={11} />
              Propose change
            </button>
          )}
        </div>

        <div className="emp-kv-grid">
          <div className="emp-kv-item">
            <span className="emp-kv-label">Full Legal Name</span>
            <span className="emp-kv-value">
              {identity?.fullLegalName || profile?.fullLegalName || '—'}
            </span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Father / Spouse Name</span>
            <span className="emp-kv-value">
              {identity?.fatherOrSpouseName || profile?.fatherOrSpouseName || '—'}
            </span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Document Type</span>
            <span className="emp-kv-value">
              {(identity?.identityDocumentType ?? profile?.identityDocumentType) === 2
                ? 'Passport'
                : (identity?.identityDocumentType ?? profile?.identityDocumentType) === 1
                  ? 'CNIC / Smart Card'
                  : '—'}
            </span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Document Number</span>
            <span className="emp-kv-value">
              {identity?.identityDocumentNumber || profile?.identityDocumentNumber
                ? formatCnic(
                    identity?.identityDocumentNumber || profile?.identityDocumentNumber || '',
                  )
                : '—'}
            </span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Document Expiry</span>
            <span className="emp-kv-value">
              {identity?.identityDocumentExpiresOn || profile?.identityDocumentExpiresOn || '—'}
            </span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Date of Birth &amp; Age</span>
            <span className="emp-kv-value">
              {calculateAge(identity?.dateOfBirth || profile?.dateOfBirth)}
            </span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Gender</span>
            <span className="emp-kv-value">{identity?.gender || profile?.gender || '—'}</span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Personal Phone</span>
            <span className="emp-kv-value">
              {identity?.personalPhone ||
                profile?.personalPhone ||
                profile?.loginPhoneNumber ||
                '—'}
            </span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Company Phone</span>
            <span className="emp-kv-value">
              {identity?.companyPhone || profile?.companyPhone || '—'}
            </span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Official Email</span>
            <span className="emp-kv-value">
              {identity?.officialEmail || profile?.officialEmail || '—'}
            </span>
          </div>
        </div>
      </div>
    );
  }

  if (section.name === 'employment') {
    const rawEmp = data && !('status' in data) ? data : {};
    const empData: Partial<EmployeeProfileValues> = {
      ...profile,
      ...rawEmp,
    };

    const dept = departments?.find(
      (d) => d.id.toLowerCase() === empData.departmentId?.toLowerCase(),
    );
    const deptName =
      dept?.name ||
      (empData.departmentId
        ? empData.departmentId.length > 20
          ? `Dept (${empData.departmentId.slice(0, 8)})`
          : empData.departmentId
        : 'Not assigned');

    const desig = designations?.find(
      (d) => d.id.toLowerCase() === empData.designationId?.toLowerCase(),
    );
    const desigName =
      desig?.name ||
      (empData.designationId
        ? empData.designationId.length > 20
          ? `Desig (${empData.designationId.slice(0, 8)})`
          : empData.designationId
        : 'Not assigned');

    const branch = branches?.find((b) => b.id.toLowerCase() === empData.branchId?.toLowerCase());
    const branchName =
      branch?.name ||
      (empData.branchId
        ? empData.branchId.length > 20
          ? `Branch (${empData.branchId.slice(0, 8)})`
          : empData.branchId
        : 'Not provided');

    const manager = employees?.find(
      (e) => e.id.toLowerCase() === empData.reportingManagerEmployeeId?.toLowerCase(),
    );

    const empType = employmentTypes?.find(
      (t) => t.id.toLowerCase() === empData.employmentTypeId?.toLowerCase(),
    );

    const staffCat = staffCategories?.find(
      (c) => c.id.toLowerCase() === empData.staffCategoryId?.toLowerCase(),
    );

    return (
      <div className="emp-card">
        <div className="emp-card-header">
          <div className="emp-card-title">
            <Briefcase size={14} className="emp-card-title-icon" />
            <span>Employment &amp; Organization Assignment</span>
          </div>
          {canWrite && (
            <button type="button" className="emp-card-edit-btn" onClick={onEdit}>
              <Pencil size={11} />
              Propose change
            </button>
          )}
        </div>

        <div className="emp-kv-grid">
          <div className="emp-kv-item">
            <span className="emp-kv-label">Department</span>
            <span
              className="emp-kv-value"
              style={{ display: 'flex', alignItems: 'center', gap: '5px', flexWrap: 'wrap' }}
            >
              <span>{deptName}</span>
              {dept?.code && (
                <span className="emp-short-id-chip" title={`Department Code: ${dept.code}`}>
                  {dept.code}
                </span>
              )}
              {empData.departmentId && (
                <span className="emp-short-id-chip" title={`UUID: ${empData.departmentId}`}>
                  {empData.departmentId.slice(0, 8)}
                </span>
              )}
            </span>
          </div>

          <div className="emp-kv-item">
            <span className="emp-kv-label">Designation</span>
            <span
              className="emp-kv-value"
              style={{ display: 'flex', alignItems: 'center', gap: '5px', flexWrap: 'wrap' }}
            >
              <span>{desigName}</span>
              {desig?.code && (
                <span className="emp-short-id-chip" title={`Designation Code: ${desig.code}`}>
                  {desig.code}
                </span>
              )}
              {empData.designationId && (
                <span className="emp-short-id-chip" title={`UUID: ${empData.designationId}`}>
                  {empData.designationId.slice(0, 8)}
                </span>
              )}
            </span>
          </div>

          <div className="emp-kv-item">
            <span className="emp-kv-label">Work Office / Branch</span>
            <span
              className="emp-kv-value"
              style={{ display: 'flex', alignItems: 'center', gap: '5px', flexWrap: 'wrap' }}
            >
              <span>{branchName}</span>
              {branch?.code && (
                <span className="emp-short-id-chip" title={`Branch Code: ${branch.code}`}>
                  {branch.code}
                </span>
              )}
              {empData.branchId && (
                <span className="emp-short-id-chip" title={`UUID: ${empData.branchId}`}>
                  {empData.branchId.slice(0, 8)}
                </span>
              )}
            </span>
          </div>

          <div className="emp-kv-item">
            <span className="emp-kv-label">Reporting Manager</span>
            <span className="emp-kv-value">
              {manager ? (
                <div
                  style={{ display: 'flex', alignItems: 'center', gap: '5px', flexWrap: 'wrap' }}
                >
                  <Link
                    to={`/hr/employees/${manager.id}`}
                    className="emp-manager-link"
                    title="Click to view manager profile"
                  >
                    {manager.name}
                  </Link>
                  <span className="emp-code-chip" title="Employee Number">
                    {formatEmployeeCode(manager.employeeNumber)}
                  </span>
                </div>
              ) : empData.reportingManagerEmployeeId ? (
                <div
                  style={{ display: 'flex', alignItems: 'center', gap: '5px', flexWrap: 'wrap' }}
                >
                  <EmployeeReference employeeId={empData.reportingManagerEmployeeId} showName />
                </div>
              ) : (
                <span className="muted" style={{ fontStyle: 'italic', fontWeight: 500 }}>
                  Not assigned
                </span>
              )}
            </span>
          </div>

          <div className="emp-kv-item">
            <span className="emp-kv-label">Employment Type</span>
            <span className="emp-kv-value">
              {empType ? (
                <div
                  style={{ display: 'flex', alignItems: 'center', gap: '5px', flexWrap: 'wrap' }}
                >
                  <span className="emp-type-pill">{empType.name}</span>
                  {empType.code && (
                    <span className="emp-short-id-chip" title={`Type Code: ${empType.code}`}>
                      {empType.code}
                    </span>
                  )}
                  <span className="emp-short-id-chip" title={`Type UUID: ${empType.id}`}>
                    {empType.id.slice(0, 8)}
                  </span>
                </div>
              ) : empData.employmentTypeId ? (
                <div
                  style={{ display: 'flex', alignItems: 'center', gap: '5px', flexWrap: 'wrap' }}
                >
                  <span className="emp-type-pill">Not provided</span>
                  <span
                    className="emp-short-id-chip"
                    title={`Type UUID: ${empData.employmentTypeId}`}
                  >
                    {empData.employmentTypeId.slice(0, 8)}
                  </span>
                </div>
              ) : (
                <span className="emp-type-pill">Not provided</span>
              )}
            </span>
          </div>

          <div className="emp-kv-item">
            <span className="emp-kv-label">Staff Category</span>
            <span className="emp-kv-value">
              {staffCat ? (
                <div
                  style={{ display: 'flex', alignItems: 'center', gap: '5px', flexWrap: 'wrap' }}
                >
                  <span className="emp-category-pill">{staffCat.name}</span>
                  {staffCat.code && (
                    <span className="emp-short-id-chip" title={`Category Code: ${staffCat.code}`}>
                      {staffCat.code}
                    </span>
                  )}
                  <span className="emp-short-id-chip" title={`Category UUID: ${staffCat.id}`}>
                    {staffCat.id.slice(0, 8)}
                  </span>
                </div>
              ) : empData.staffCategoryId ? (
                <div
                  style={{ display: 'flex', alignItems: 'center', gap: '5px', flexWrap: 'wrap' }}
                >
                  <span className="emp-category-pill">Not provided</span>
                  <span
                    className="emp-short-id-chip"
                    title={`Category UUID: ${empData.staffCategoryId}`}
                  >
                    {empData.staffCategoryId.slice(0, 8)}
                  </span>
                </div>
              ) : (
                <span className="emp-category-pill">Not provided</span>
              )}
            </span>
          </div>

          <div className="emp-kv-item">
            <span className="emp-kv-label">Joining Date</span>
            <span className="emp-kv-value">
              {empData.joinedOn ? labelDate(empData.joinedOn) : '—'}
            </span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Probation Period Ends</span>
            <span className="emp-kv-value">
              {empData.probationEndsOn ? labelDate(empData.probationEndsOn) : '—'}
            </span>
          </div>
        </div>
      </div>
    );
  }

  if (section.name === 'bank') {
    const rawBank = (data && !('status' in data) ? data : {}) as Record<string, unknown>;
    const profileBank =
      (profile as unknown as { financial?: { bank?: Record<string, unknown> } })?.financial?.bank ??
      {};
    const bank = {
      ...profileBank,
      ...rawBank,
    } as Record<string, string | undefined>;
    return (
      <div className="emp-card">
        <div className="emp-card-header">
          <div className="emp-card-title">
            <Landmark size={14} className="emp-card-title-icon" />
            <span>Bank &amp; Payment Details</span>
          </div>
          {canWrite && (
            <button type="button" className="emp-card-edit-btn" onClick={onEdit}>
              <Pencil size={11} />
              Propose change
            </button>
          )}
        </div>

        <div className="emp-kv-grid">
          <div className="emp-kv-item">
            <span className="emp-kv-label">Bank Name</span>
            <span className="emp-kv-value">{bank.bankName || 'Not provided'}</span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Account Title</span>
            <span className="emp-kv-value">{bank.accountTitle || 'Not provided'}</span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Account Number</span>
            <span className="emp-kv-value">{bank.accountNumber || 'Not provided'}</span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">IBAN</span>
            <span className="emp-kv-value">{bank.iban || 'Not provided'}</span>
          </div>
        </div>
      </div>
    );
  }

  // Generic fallback for any other sections
  const entries = Object.entries(
    (data && typeof data === 'object' ? data : {}) as Record<string, unknown>,
  ).filter(([k]) => !['id', 'version', 'cityId'].includes(k));
  return (
    <div className="emp-card">
      <div className="emp-card-header">
        <div className="emp-card-title">
          <FileText size={14} className="emp-card-title-icon" />
          <span>{section.label}</span>
        </div>
        {canWrite && (
          <button type="button" className="emp-card-edit-btn" onClick={onEdit}>
            <Pencil size={11} />
            {(section.name as string) === 'basic' ? 'Edit details' : 'Propose change'}
          </button>
        )}
      </div>

      <div className="emp-kv-grid">
        {entries.map(([key, val]) => (
          <div className="emp-kv-item" key={key}>
            <span className="emp-kv-label">{key.replace(/([a-z0-9])([A-Z])/g, '$1 $2')}</span>
            <span className="emp-kv-value">
              {val === null || val === undefined || val === '' ? (
                '—'
              ) : key === 'employeeId' && typeof val === 'string' ? (
                <EmployeeReference employeeId={val} />
              ) : key === 'employeeNumber' &&
                (typeof val === 'number' || typeof val === 'string') ? (
                formatEmployeeCode(val)
              ) : typeof val === 'object' ? (
                JSON.stringify(val)
              ) : (
                String(val)
              )}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}

function CurrentSection({
  employeeId,
  section,
  edit,
  profile,
  currentQuery,
  onSaved,
}: {
  employeeId: string;
  section: Section;
  edit: boolean;
  profile: EmployeeProfileValues;
  currentQuery?: ReturnType<typeof useEmployeeProfile>['basic'];
  onSaved: () => void;
}) {
  const access = useHrAccess();
  const [params, setParams] = useSearchParams();
  const [revision, setRevision] = useState(0);
  const [historyRevision, setHistoryRevision] = useState(0);
  const [saved, setSaved] = useState(false);
  const [isEditModalOpen, setIsEditModalOpen] = useState(edit);

  useEffect(() => {
    setIsEditModalOpen(edit);
  }, [edit]);

  const handleCloseModal = () => {
    setIsEditModalOpen(false);
    if (params.get('edit') === '1') {
      const next = new URLSearchParams(params);
      next.delete('edit');
      setParams(next);
    }
  };

  const handleOpenModal = () => {
    setIsEditModalOpen(true);
  };

  const sectionQuery = useHrQuery(
    `employees/${employeeId}/current/${section.name}`,
    currentUpdateSchema,
    ['hr.employees.read', ...section.read],
    !currentQuery,
  );
  const query = currentQuery ?? sectionQuery;
  const canWrite = access.can(...section.read, ...section.write);

  const deptsQuery = useHrQuery('departments?pageSize=100', pageOf(masterSchema), [
    'hr.organization.read',
  ]);
  const desigsQuery = useHrQuery('designations?pageSize=100', pageOf(masterSchema), [
    'hr.organization.read',
  ]);
  const branchesQuery = useHrQuery('branches?pageSize=100', pageOf(masterSchema), [
    'hr.organization.read',
  ]);
  const staffCatsQuery = useHrQuery('staff-categories?pageSize=100', pageOf(masterSchema), [
    'hr.organization.read',
  ]);
  const empTypesQuery = useHrQuery('employment-types?pageSize=100', pageOf(masterSchema), [
    'hr.organization.read',
  ]);
  const employeesQuery = useHrQuery('employees?pageSize=100', pageOf(employeeSchema), [
    'hr.employees.read',
  ]);

  return (
    <div className="emp-section-wrapper">
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: '8px',
          marginBottom: '8px',
        }}
      >
        <div>
          <h2 style={{ margin: 0, fontSize: '13.5px', fontWeight: 700, color: '#0f172a' }}>
            {section.label} Records
          </h2>
          {query.data && (
            <p
              className="muted"
              style={{ margin: '2px 0 0 0', fontSize: '11px', color: '#64748b' }}
            >
              Current as of {query.data.asOf} &middot; Effective from{' '}
              {query.data.current.effectiveFrom}
            </p>
          )}
        </div>
        <Button
          variant="secondary"
          title="Reload current section"
          aria-label="Reload current section"
          onClick={() => void query.refetch()}
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: '4px',
            fontSize: '11px',
            height: '26px',
            padding: '0 8px',
            borderColor: '#e2e8f0',
          }}
        >
          <RefreshCw size={11} />
          Reload
        </Button>
      </div>
      {saved && (
        <p role="status" className="hr-message" style={{ margin: '0 0 8px 0', fontSize: '11.5px' }}>
          Saved successfully.
        </p>
      )}
      <ProfileQueryState query={query}>
        {query.data && (
          <>
            <StructuredSectionDisplay
              section={section}
              data={query.data.current.data[section.name]}
              profile={section.name === 'basic' ? profile : undefined}
              departments={deptsQuery.data?.items}
              designations={desigsQuery.data?.items}
              branches={branchesQuery.data?.items}
              staffCategories={staffCatsQuery.data?.items}
              employmentTypes={empTypesQuery.data?.items}
              employees={employeesQuery.data?.items}
              canWrite={
                canWrite &&
                !query.data.scheduled &&
                (!query.data.evidenceIssue ||
                  (section.name === 'identity' &&
                    query.data.evidenceIssue === 'identity_document_evidence_unavailable'))
              }
              onEdit={handleOpenModal}
            />

            {query.data.current.bankVerified !== null && (
              <div style={{ marginTop: '8px' }}>
                <span
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '4px',
                    padding: '3px 8px',
                    borderRadius: '4px',
                    fontSize: '11px',
                    fontWeight: 600,
                    background: query.data.current.bankVerified ? '#dcfce7' : '#fef3c7',
                    color: query.data.current.bankVerified ? '#15803d' : '#b45309',
                    border: `1px solid ${query.data.current.bankVerified ? '#bbf7d0' : '#fde68a'}`,
                  }}
                >
                  <ShieldCheck size={12} />
                  Bank Account Status:{' '}
                  {query.data.current.bankVerified
                    ? 'Verified by Finance'
                    : 'Verification In Review'}
                </span>
              </div>
            )}

            {query.data.scheduled && (
              <div
                className="emp-card"
                style={{ marginTop: '8px', borderLeft: '3px solid #0f172a' }}
              >
                <div className="emp-card-header simple">
                  <div className="emp-card-title">
                    <History size={13} className="emp-card-title-icon" />
                    <span>Scheduled for {query.data.scheduled.effectiveFrom}</span>
                  </div>
                </div>
                <StructuredSectionDisplay
                  section={section}
                  data={query.data.scheduled.data[section.name]}
                  departments={deptsQuery.data?.items}
                  designations={desigsQuery.data?.items}
                  branches={branchesQuery.data?.items}
                  staffCategories={staffCatsQuery.data?.items}
                  employmentTypes={empTypesQuery.data?.items}
                  employees={employeesQuery.data?.items}
                  canWrite={false}
                  onEdit={() => {}}
                />
              </div>
            )}

            {canWrite &&
              query.data &&
              (!query.data.evidenceIssue ||
                (section.name === 'identity' &&
                  query.data.evidenceIssue === 'identity_document_evidence_unavailable')) && (
                <ChangeFormModal
                  key={`${query.data.version}-${revision}`}
                  open={isEditModalOpen}
                  onClose={handleCloseModal}
                  current={query.data}
                  section={section}
                  onSaved={() => {
                    onSaved();
                    setSaved(true);
                    setRevision((r) => r + 1);
                    void query.refetch();
                  }}
                />
              )}
          </>
        )}
      </ProfileQueryState>
      <ChangeHistory
        key={`history-${revision}-${historyRevision}`}
        employeeId={employeeId}
        section={section}
      />
      {section.name !== 'basic' && (
        <ChangeRequests
          key={`requests-${revision}`}
          employeeId={employeeId}
          section={section}
          onDecided={() => {
            onSaved();
            void query.refetch();
            setHistoryRevision((v) => v + 1);
          }}
        />
      )}
    </div>
  );
}

function CurrentFiles({ employeeId, medical }: { employeeId: string; medical: boolean }) {
  const access = useHrAccess();
  const [saved, setSaved] = useState(false);
  const permissions = [
    'hr.employees.read',
    'hr.employees.documents.read',
    ...(medical ? ['hr.employees.medical.read'] : []),
  ];
  const query = useHrQuery(
    `employees/${employeeId}/current/basic`,
    currentUpdateSchema,
    permissions,
  );
  if (!access.can(...permissions)) return <Denied />;
  return (
    <div className="emp-section-wrapper" style={{ marginTop: '16px' }}>
      <ProfileQueryState query={query}>
        {query.data && (
          <>
            {saved && (
              <p role="status" className="hr-message">
                Document evidence updated.
              </p>
            )}
            <EmployeeDocuments
              employeeId={employeeId}
              version={query.data.version}
              editable={access.can('hr.employees.changes.request')}
              approved
              medical={medical}
              onSaved={() => {
                setSaved(true);
                void query.refetch();
              }}
            />
          </>
        )}
      </ProfileQueryState>
    </div>
  );
}
