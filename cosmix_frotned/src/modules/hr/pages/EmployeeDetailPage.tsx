import { useState, useEffect } from 'react';
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
} from 'lucide-react';
import { z } from 'zod';
import { ApiError } from '@/services/api/api-error';
import { Button } from '@/components/ui/Button';
import { useHrAccess } from '../hooks/use-hr-access';
import { useHrQuery } from '../hooks/use-hr-queries';
import { id, pageOf } from '../schemas/contracts';
import { masterSchema } from '../schemas/organization';
import { profileResponse } from '../schemas/hiring';
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
  if (!dobStr) return '—';
  const birth = new Date(dobStr);
  if (isNaN(birth.getTime())) return dobStr;
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
  const profileQuery = useHrQuery(`employees/${employeeId}`, profileResponse, [
    'hr.employees.read',
  ]);

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
  const profile = profileQuery.data?.profile;
  const employee = profileQuery.data?.employee;

  return (
    <div className="emp-page-container">
      {/* Top Breadcrumb Link */}
      <Link className="emp-back-link" to={`${basePath}/employees`}>
        <ArrowLeft size={13} />
        Back to employees
      </Link>

      {/* Main Profile Header Card */}
      <EmployeeProfileHeader employeeId={employeeId} />

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
          <Link to="?section=access" className={`emp-nav-tab ${name === 'access' ? 'active' : ''}`}>
            <ShieldCheck size={13} />
            Roles &amp; Access
          </Link>
        )}
      </nav>

      {/* Workspace Area */}
      {isOverview ? (
        <EmployeeOverview employeeId={employeeId} />
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
                />
              ) : (
                <Denied />
              )
            ) : name === 'compensation' ? (
              <SalaryWorkspace
                key={name}
                employeeId={employeeId}
                edit={params.get('edit') === '1'}
              />
            ) : name === 'lifecycle' ? (
              <StatusWorkspace
                key={name}
                employeeId={employeeId}
                edit={params.get('edit') === '1'}
              />
            ) : name === 'files' || name === 'medical-files' ? (
              <CurrentFiles key={name} employeeId={employeeId} medical={name === 'medical-files'} />
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
  employee?: z.infer<typeof profileResponse>['employee'];
  profile?: z.infer<typeof profileResponse>['profile'];
  departments?: { id: string; name: string }[];
  designations?: { id: string; name: string }[];
  branches?: { id: string; name: string }[];
  canCompensation: boolean;
  canAccess: boolean;
}) {
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
            Hiring Profile Snapshot
          </span>
          {employee?.employeeNumber && (
            <span className="emp-rail-badge">{employee.employeeNumber}</span>
          )}
        </div>
        <div className="emp-rail-snapshot-box">
          <p className="muted">Hiring values may differ from current approved records.</p>
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
          <div className="emp-rail-snapshot-row">
            <span className="emp-rail-snapshot-lbl">CNIC</span>
            <span className="emp-rail-snapshot-val">
              {profile?.identityDocumentNumber ? formatCnic(profile.identityDocumentNumber) : '—'}
            </span>
          </div>
          <div className="emp-rail-snapshot-row">
            <span className="emp-rail-snapshot-lbl">Mobile</span>
            <span className="emp-rail-snapshot-val">
              {profile?.personalPhone || profile?.loginPhoneNumber || '—'}
            </span>
          </div>
          <div className="emp-rail-snapshot-row">
            <span className="emp-rail-snapshot-lbl">Work Email</span>
            <span className="emp-rail-snapshot-val" title={profile?.officialEmail || ''}>
              {profile?.officialEmail || '—'}
            </span>
          </div>
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

function EmployeeOverview({
  employeeId,
  employee: _employee,
  profile: initialProfile,
  departments: _departments,
  designations: _designations,
  branches: _branches,
}: {
  employeeId: string;
  employee?: z.infer<typeof profileResponse>['employee'];
  profile?: z.infer<typeof profileResponse>['profile'];
  departments?: { id: string; name: string }[];
  designations?: { id: string; name: string }[];
  branches?: { id: string; name: string }[];
}) {
  const profileQuery = useHrQuery(`employees/${employeeId}`, profileResponse, [
    'hr.employees.read',
  ]);
  const profile = profileQuery.data?.profile || initialProfile;

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
      {/* The hiring profile is not the current-value endpoint. */}
      <div className="emp-overview-timestamp">
        Hiring profile snapshot — these values may differ from current approved records. Open
        Personal details, Identity or Employment for current values.
      </div>

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
    <QueryState query={query}>
      {query.data && <EmployeeAccessSetup employeeId={employeeId} approved />}
    </QueryState>
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
  profile,
}: {
  section: Section;
  data: Record<string, unknown> | null | undefined;
  canWrite: boolean;
  onEdit: () => void;
  departments?: { id: string; name: string }[];
  designations?: { id: string; name: string }[];
  branches?: { id: string; name: string }[];
  profile?: z.infer<typeof profileResponse>['profile'];
}) {
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
            <div className="emp-kv-item">
              <span className="emp-kv-label">Full Legal Name</span>
              <span className="emp-kv-value">{profile?.fullLegalName || '—'}</span>
            </div>
            <div className="emp-kv-item">
              <span className="emp-kv-label">Father / Spouse Name</span>
              <span className="emp-kv-value">{profile?.fatherOrSpouseName || '—'}</span>
            </div>
            <div className="emp-kv-item">
              <span className="emp-kv-label">Date of Birth &amp; Age</span>
              <span className="emp-kv-value">{calculateAge(profile?.dateOfBirth)}</span>
            </div>
            <div className="emp-kv-item">
              <span className="emp-kv-label">Gender</span>
              <span className="emp-kv-value">{profile?.gender || '—'}</span>
            </div>
            <div className="emp-kv-item">
              <span className="emp-kv-label">Marital Status</span>
              <span className="emp-kv-value">
                {basic?.maritalStatus || profile?.maritalStatus || '—'}
              </span>
            </div>
            <div className="emp-kv-item">
              <span className="emp-kv-label">CNIC / ID Document</span>
              <span className="emp-kv-value">
                {profile?.identityDocumentNumber ? formatCnic(profile.identityDocumentNumber) : '—'}
              </span>
            </div>
            <div className="emp-kv-item">
              <span className="emp-kv-label">Document Expiry</span>
              <span className="emp-kv-value">{profile?.identityDocumentExpiresOn || '—'}</span>
            </div>
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
                <div className="emp-emergency-row">
                  <span className="emp-emergency-label">Work Email</span>
                  <span className="emp-emergency-val">{profile?.officialEmail || '—'}</span>
                </div>
                <div className="emp-emergency-row">
                  <span className="emp-emergency-label">Personal Email</span>
                  <span className="emp-emergency-val">
                    {basic?.personalEmail || profile?.personalEmail || '—'}
                  </span>
                </div>
                <div className="emp-emergency-row">
                  <span className="emp-emergency-label">Personal Mobile</span>
                  <span className="emp-emergency-val">
                    {profile?.personalPhone || profile?.loginPhoneNumber || '—'}
                  </span>
                </div>
                <div className="emp-emergency-row">
                  <span className="emp-emergency-label">Company Phone</span>
                  <span className="emp-emergency-val">{profile?.companyPhone || '—'}</span>
                </div>
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
                : 'CNIC / Smart Card'}
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
    const empData: Partial<z.infer<typeof profileResponse>['profile']> = {
      ...profile,
      ...rawEmp,
    };

    const deptName =
      departments?.find((d) => d.id === empData.departmentId)?.name ||
      empData.departmentId ||
      'Not assigned';
    const desigName =
      designations?.find((d) => d.id === empData.designationId)?.name ||
      empData.designationId ||
      'Not assigned';
    const branchName =
      branches?.find((b) => b.id === empData.branchId)?.name || empData.branchId || 'Head Office';

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
            <span className="emp-kv-value">{deptName}</span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Designation</span>
            <span className="emp-kv-value">{desigName}</span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Work Office / Branch</span>
            <span className="emp-kv-value">{branchName}</span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Reporting Manager</span>
            <span className="emp-kv-value">
              {empData.reportingManagerEmployeeId || 'Not assigned'}
            </span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Employment Type</span>
            <span className="emp-kv-value">{empData.employmentTypeId || 'Full-Time'}</span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Staff Category</span>
            <span className="emp-kv-value">{empData.staffCategoryId || 'Standard Staff'}</span>
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
              {val === null || val === undefined || val === ''
                ? '—'
                : typeof val === 'object'
                  ? JSON.stringify(val)
                  : String(val)}
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
}: {
  employeeId: string;
  section: Section;
  edit: boolean;
}) {
  const access = useHrAccess();
  const location = useLocation();
  const basePath = location.pathname.startsWith('/admin') ? '/admin' : '/hr';
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

  const profileQuery = useHrQuery(`employees/${employeeId}`, profileResponse, [
    'hr.employees.read',
  ]);
  const query = useHrQuery(`employees/${employeeId}/current/${section.name}`, currentUpdateSchema, [
    'hr.employees.read',
    ...section.read,
  ]);
  const unapproved =
    query.error instanceof ApiError && query.error.code === 'employee_not_approved';
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

  if (unapproved) {
    const isDraft = profileQuery.data?.employee.onboardingStatus === 0;
    const statusLabel = isDraft ? 'Draft (Incomplete)' : 'Pending Approval';
    return (
      <div className="emp-section-wrapper">
        <div
          style={{
            padding: '8px 12px',
            background: '#fefce8',
            border: '1px solid #fef08a',
            borderRadius: '6px',
            marginBottom: '10px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            flexWrap: 'wrap',
            gap: '8px',
          }}
        >
          <div>
            <div
              style={{ fontWeight: 650, fontSize: '12px', color: '#854d0e', marginBottom: '1px' }}
            >
              Employee Onboarding in Progress ({statusLabel})
            </div>
            <div style={{ fontSize: '11px', color: '#a16207' }}>
              Formal version trees and live change requests become active after the employee
              onboarding is approved.
            </div>
          </div>
          <Link
            to={`${basePath}/employees/${employeeId}/hiring`}
            style={{
              padding: '4px 10px',
              borderRadius: '5px',
              background: '#0f172a',
              color: '#ffffff',
              fontSize: '11px',
              fontWeight: 600,
              textDecoration: 'none',
              display: 'inline-flex',
              alignItems: 'center',
              gap: '4px',
            }}
          >
            Edit / Continue Onboarding
          </Link>
        </div>
        {profileQuery.data?.profile ? (
          <StructuredSectionDisplay
            section={section}
            data={profileQuery.data.profile}
            profile={profileQuery.data.profile}
            departments={deptsQuery.data?.items}
            designations={desigsQuery.data?.items}
            branches={branchesQuery.data?.items}
            canWrite={false}
            onEdit={() => {}}
          />
        ) : (
          <div className="emp-card">
            <p className="muted" style={{ margin: 0, fontSize: '11.5px' }}>
              Draft record pending onboarding completion ({statusLabel})
            </p>
          </div>
        )}
        <ChangeHistory employeeId={employeeId} section={section} key={historyRevision} />
      </div>
    );
  }

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
      <QueryState query={query}>
        {query.data && (
          <>
            <StructuredSectionDisplay
              section={section}
              data={query.data.current.data[section.name]}
              profile={profileQuery.data?.profile}
              departments={deptsQuery.data?.items}
              designations={desigsQuery.data?.items}
              branches={branchesQuery.data?.items}
              canWrite={canWrite && !query.data.scheduled}
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
                  canWrite={false}
                  onEdit={() => {}}
                />
              </div>
            )}

            {canWrite && query.data && (
              <ChangeFormModal
                key={`${query.data.version}-${revision}`}
                open={isEditModalOpen}
                onClose={handleCloseModal}
                current={query.data}
                section={section}
                onSaved={() => {
                  setSaved(true);
                  setRevision((r) => r + 1);
                  void query.refetch();
                }}
              />
            )}
          </>
        )}
      </QueryState>
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
      <QueryState query={query}>
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
      </QueryState>
    </div>
  );
}
